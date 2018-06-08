using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DeInterface;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Runtime.InteropServices;
using System.Net;
using HDF5DotNet;
using FileLoader;
using System.Timers;
using System.Drawing;
using System.Windows.Forms;

namespace DeExampleCSharpWPF
{

    public partial class MainWindow : Window, INotifyPropertyChanged
    {

        #region global variables

        private DeInterfaceNET _deInterface;
        private bool _liveModeEnabled;
        //private LiveModeView _liveView;   LiveViewWindow no longer a separate window
        private bool _closing;
        UInt16[] m_image_local;
        static Semaphore semaphore;
        Queue<UInt16[]> m_imageQueue = new Queue<ushort[]>();
        public int numpos;
        public int height;
        public int width;

        private int _imageCount = 0;
        private bool _firstImage = true;
        private DateTime _renderStart;
        private System.Timers.Timer _updateTimer;
        private WriteableBitmap _wBmp;
        private WriteableBitmap _wBmpRecon;
        private WriteableBitmap _wBmpHAADF;
        private WriteableBitmap _wBmpHAADFROI;
        private int nTickCount = 0;
        private decimal dTickCountAvg = 0;
        private int nCount = 0;

        // scan voltage range calibrated from FEI internal scan system
        public double x_scan_max = 1.5;
        public double y_scan_max = 1.9;
        public double x_scan_min = -1.5;
        public double y_scan_min = -1.9;


        public decimal Fps
        {
            get { return Math.Round(Convert.ToDecimal(Convert.ToDouble(_imageCount) / TotalSeconds), 3); }
            //get { return Convert.ToDecimal(TotalSeconds); }

        }

        public int ImageCount
        {
            get { return _imageCount; }
            set
            {
                _imageCount = value;
                NotifyPropertyChanged("ImageCount");
            }
        }

        public decimal Ilt
        {
            get
            {
                dTickCountAvg =
                     ((dTickCountAvg * nCount + nTickCount) / (nCount + 1));

                nCount++;
                return Math.Round((dTickCountAvg / 1000), 3);
            }
        }

        public double TotalSeconds
        {
            get
            {
                if (_firstImage == false)
                    return Math.Round(((DateTime.Now - _renderStart).TotalMilliseconds) / 1000);
                else
                    return 1;   // return 0 would cause N/0 error
            }
        }

        public ObservableCollection<Property> CameraProperties { get; private set; }

        System.Windows.Point _startPosition;
        bool _isResizing = false;
        bool _isResizing2 = false;



        // status return from hardware
        public enum HW_STATUS_RETURNS
        {
            HW_SUCCESS,
            HW_OTHER
        }
        #endregion

        #region initialize and close main window
        // used to close main window
        private void Window_Closing(object sender, CancelEventArgs e)
        {

        }
        public MainWindow()
        {
            InitializeComponent();
            MessageBox.Text += "\n";
        }
        #endregion

        #region connect to DE server

        // Get Image Transfer Mode      
        private ImageTransfer_Mode GetImageTransferMode()
        {
            string strHostName = Dns.GetHostName();
            IPHostEntry ipEntry = Dns.GetHostEntry(strHostName);

            bool bFind = false;
            foreach (IPAddress ipaddr in ipEntry.AddressList)
            {
                if (ipaddr.ToString() == IPAddr.Text.Trim())
                {
                    bFind = true;
                    break;
                }
            }
            if (!bFind && IPAddr.Text.Trim() != "127.0.0.1")
                return ImageTransfer_Mode.ImageTransfer_Mode_TCP;

            /*
             *  determine whether it is TCP mode or Memory map mode
             */
            using (MemoryMappedFile mmf = MemoryMappedFile.OpenExisting("ImageFileMappingObject"))
            {
                using (MemoryMappedViewAccessor viewAccessor = mmf.CreateViewAccessor())
                {

                    int imageSize = Marshal.SizeOf((typeof(Mapped_Image_Data_)));
                    var imageDate = new Mapped_Image_Data_();
                    viewAccessor.Read(0, out imageDate);

                    if (imageDate.client_opened_mmf)
                        return ImageTransfer_Mode.ImageTransfer_Mode_MMF;
                }
            }

            return ImageTransfer_Mode.ImageTransfer_Mode_TCP;
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (btnConnect.Content.ToString() == "Disconnect")
            {
                /*                if (_liveModeEnabled)
                                {
                                    _liveView.Close();
                                }*/
                _deInterface.close();
                cmbCameras.Items.Clear();
                cmbCameras.Text = "";
                btnConnect.Content = "Connect";
                slider_outerang.Value = 1;
            }
            else if (_deInterface.connect(IPAddr.Text, 48880, 48879))
            {

                DeError error = _deInterface.GetLastError();
                Console.WriteLine(error.Description);
                try
                {
                    //get the list of cameras for the combobox
                    List<String> cameras = new List<String>();
                    _deInterface.GetCameraNames(ref cameras);
                    cmbCameras.Items.Clear();
                    foreach (var camera in cameras)
                    {
                        cmbCameras.Items.Add(camera);
                    }
                    cmbCameras.SelectedIndex = 0;
                }
                catch (Exception exc)
                {
                    System.Windows.MessageBox.Show(exc.Message);
                }

                btnConnect.Content = "Disconnect";

                switch (GetImageTransferMode())
                {
                    case ImageTransfer_Mode.ImageTransfer_Mode_MMF:
                        cmbTransport.SelectedIndex = 0;
                        break;
                    case ImageTransfer_Mode.ImageTransfer_Mode_TCP:
                        cmbTransport.SelectedIndex = 1;
                        break;
                    default:
                        break;
                }
                cmbTransport.IsEnabled = false;
                string xSize = "";
                string ySize = "";
                _deInterface.GetProperty("Image Size X", ref xSize);
                _deInterface.GetProperty("Image Size Y", ref ySize);
                PixelsX.Text = xSize;
                PixelsY.Text = ySize;
            }

        }

        #endregion

        #region Acquire HAADF and choose ROI

        // Function to acquire single HAADF image with default setting: max voltage range, 512*512 beam positions
        // No DE camera is used together with beam scan, only collect data from digitizer.
        // For DE camera in master mode, camera has to run to generate trigger signal
        public void Single_Acquire(object sender, RoutedEventArgs e)
        {
            // test for passive mode scan control
            int x_step_num = 256;
            int y_step_num = 256;
            int[] Xarray_index = new int[x_step_num * 2];   // Xarray_index contains one round scan
            int[] Yarray_index = new int[y_step_num + 2];   // Yarray_index contains one single trip scan with two more at beginning and end to drive beam away
            double[] Xarray_vol = new double[x_step_num];
            double[] Yarray_vol = new double[y_step_num + 1];
            double x_step_size = 1 / (x_step_num - 1);
            double y_step_size = 1 / (y_step_num - 1);

            for (int ix = 0; ix < x_step_num * 2; ix++)
            {
                if (ix < x_step_num)
                {
                    Xarray_vol[ix] = -0.5 + x_step_size * ix;
                    Xarray_index[ix] = ix;
                }
                else
                {
                    Xarray_index[ix] = x_step_num * 2 - ix - 1;
                }
            }

            for (int iy = 0; iy < y_step_num; iy++)
            {
                Yarray_index[iy] = iy;
                Yarray_vol[iy+1] = -0.5 + y_step_size * iy;
            }
            Yarray_vol[0] = 1;
            Yarray_vol[y_step_num + 1] = 1;


            /*int[] Xarray_index = new int[x_step_num * x_step_num * 2];
            int[] Yarray_index = new int[y_step_num * y_step_num * 2];
            double[] Xarray_vol = new double[x_step_num + 1];
            double[] Yarray_vol = new double[y_step_num + 1];
            double x_step_size = 1 / (x_step_num - 1);
            double y_step_size = 1 / (y_step_num - 1);

            for (int iy = 0; iy < y_step_num; iy++)
            {
                for (int ix = 0; ix < x_step_num; ix++)
                {
                    if (iy % 2 == 0)
                    {
                        Xarray_index[iy * Convert.ToInt32(x_step_num) + ix] = ix;
                    }
                    else
                    {
                        Xarray_index[iy * Convert.ToInt32(x_step_num) + ix] = 256 - ix - 1;
                    }
                    Yarray_index[iy * Convert.ToInt32(x_step_num) + ix] = iy;
                }
            }

            for (int ix = Convert.ToInt32(x_step_num) * Convert.ToInt32(y_step_num); ix < Xarray_index.Length; ix++)
            {
                Xarray_index[ix] = 1;
            }

            for (int iy = Convert.ToInt32(y_step_num) * Convert.ToInt32(x_step_num); iy < Yarray_index.Length; iy++)
            {
                Yarray_index[iy] = 1;
            }

            for (int ix = 0; ix < x_step_num; ix++)
            {
                Xarray_vol[ix] = -0.5 + x_step_size * ix;
            }

            for (int iy = 0; iy < y_step_num; iy++)
            {
                Yarray_vol[iy] = -0.5 + y_step_size * iy;
            }

            Xarray_vol[x_step_num] = 1;
            Yarray_vol[y_step_num] = 1;*/

            double[] WaveformArray_Ch1 = { };
            // push 512*512*2 sized array to AWG
            PushAWGsetting(Xarray_index, Yarray_index, Xarray_vol, Yarray_vol);
            PushDigitizerSetting_defaultHAADF(ref WaveformArray_Ch1);

            HAADFreconstrcution(WaveformArray_Ch1, 256, 256, 0);

        }

        // Function used to reconstruct 2D matrix from 1D array acquired from digitizer
        // Input: Array: 1D array acquired, currently hardcoded to 10 samples per probe position, array size must be larger than 10*size_x*size_y
        //        size_x/size_y: target 2D matrix size in pixel
        //        option: 0 for reconstruction on default image window (512 px), 1 for reconstruction on ROI window (customized size)

        public void HAADFreconstrcution(double[] RawArray, int size_x, int size_y, int option)
        {

            // Generate new array for rescaled HAADF image
            UInt16[] HAADF_rescale = new UInt16[size_x * size_y];

            // Generate csv file to save HAADF raw array
            var csv = new StringBuilder();

            double Array_max = RawArray.Max();
            double Array_min = RawArray.Min();
            double scale = 65535 / (Array_max - Array_min);
            double[] subArray = new double[9];
            List<double> subArray_list = new List<double>();

            for (int i = 0; i < size_x * size_y; i++)
            {
                Array.Copy(RawArray, i * 10 + 1, subArray, 0, 9);   // copy 9 elements from array each time
                subArray_list.Clear();
                subArray_list = subArray.ToList();
                double average = subArray_list.Average();
                var average_string = average.ToString();
                csv.AppendLine(average_string);

                // rescale each point to [0 65535]
                int row = ( (i - i % size_x) / size_y );
                if (row % 2 == 0)
                {
                    HAADF_rescale[i] = (ushort)((average - Array_min) / (Array_max - Array_min) * scale);
                }
                else
                {
                    HAADF_rescale[size_x * ( row + 1 ) - i % size_x - 1] = (ushort)((average - Array_min) / (Array_max - Array_min) * scale);
                }

            }

            // write to different bitmap for different options

            int bytesPerPixel = 2;
            int stride = size_x * bytesPerPixel;
            BitmapSource HAADFbmpSource = BitmapSource.Create(size_x, size_y, 96, 96, PixelFormats.Gray16, null, HAADF_rescale, stride);



            // invoke different image box source for different options
            if (option == 0)
            {
                HAADF.Source = HAADFbmpSource;
            }

            if (option == 1)
            {
                HAADFacquisition.Source = HAADFbmpSource;
            }

            // save HAADF raw data to csv file

 /*           string FullPath = HAADFPath.Text + "HAADF_Preview_" + size_x + "_" + size_y + "_" + DateTime.Now.ToString("h:mm:ss tt");

            System.IO.FileInfo fi = null;
            try
            {
                fi = new System.IO.FileInfo(FullPath);
            }
            catch (ArgumentException) { }
            catch (System.IO.PathTooLongException) { }
            catch (NotSupportedException) { }
            if (ReferenceEquals(fi, null))
            {
                System.Windows.Forms.MessageBox.Show("HAADF saving path is not valid!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            else
            {
                File.WriteAllText(FullPath, csv.ToString());
            }*/

        }

        // Function used to write digitizer setting for default single HAADF acquisition
        public void PushDigitizerSetting_defaultHAADF(ref double[] WaveformArray_Ch1)
        {

            string sent;
            bool isNumeric = int.TryParse(FrameRate.Text, out int n);
            int record_size;
            record_size = 256 * 256 * 10;
            record_size = Convert.ToInt32(record_size * 1.3);
            sent = "A total " + record_size + "samples will be recorded by digitizer.\n";
            MessageBox.Text += sent;
            int recording_rate = Int32.Parse(FrameRate.Text) * 10;
            sent = "Digitizer frame rate is set to " + recording_rate + " samples per second.\n";
            MessageBox.Text += sent;
            // this function can only be called when running on DE camera computer with Keysight libraries
            Digitizer.Program.FetchData(record_size, recording_rate, ref WaveformArray_Ch1);

        }

        private void window_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_isResizing)    // top left resizing grip
            {
                System.Windows.Point currentPosition = Mouse.GetPosition(this);
                double diffX = currentPosition.X - _startPosition.X;
                double diffY = currentPosition.Y - _startPosition.Y;
                double currentLeft = gridResize.Margin.Left;
                double currentTop = gridResize.Margin.Top;
                double currentRight = gridResize.Margin.Right;
                double currentBottom = gridResize.Margin.Bottom;
                if(currentLeft<0)
                {
                    currentLeft = 0;
                }
                if(currentLeft > 512 - currentRight - 30)
                {
                    currentLeft = 512 - currentRight - 30;
                }
                if (currentTop < 0)
                {
                    currentTop = 0;
                }
                if (currentTop > 512 - currentBottom - 30)
                {
                    currentTop = 512 - currentBottom - 30;
                }

                gridResize.Margin = new Thickness(currentLeft + diffX, currentTop + diffY, currentRight, currentBottom);
                _startPosition = currentPosition;
                StartX.Text = currentLeft.ToString();
                StartY.Text = currentTop.ToString();
                EndX.Text = (512-currentRight).ToString();
                EndY.Text = (512-currentBottom).ToString();  // 28 for height of topic
            }
            if (_isResizing2)
            {
                System.Windows.Point currentPosition = Mouse.GetPosition(this);
                double diffX = currentPosition.X - _startPosition.X;
                double diffY = currentPosition.Y - _startPosition.Y;
                double currentLeft = gridResize.Margin.Left;
                double currentTop = gridResize.Margin.Top;
                double currentRight = gridResize.Margin.Right;
                double currentBottom = gridResize.Margin.Bottom;
                if (currentRight < 0)
                {
                    currentRight = 0;
                }
                if (currentLeft > 512 - currentRight - 30)
                {
                    currentRight = 512 - currentLeft - 30;
                }
                if (currentBottom < 0)
                {
                    currentBottom = 0;
                }
                if (currentTop > 512 - currentBottom - 30)
                {
                    currentBottom = 512 - currentTop - 30;
                }
                gridResize.Margin = new Thickness(currentLeft, currentTop, currentRight - diffX, currentBottom - diffY);
                _startPosition = currentPosition;
                StartX.Text = currentLeft.ToString();
                StartY.Text = currentTop.ToString();
                EndX.Text = (512-currentRight).ToString();
                EndY.Text = (512-currentBottom).ToString();
            }
        }

        private void resizeGrip_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isResizing == true)
            {
                _isResizing = false;
                Mouse.Capture(null);
            }

        }

        private void resizeGrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.Capture(resizeGrip2))
            {
                _isResizing = true;
                _startPosition = Mouse.GetPosition(this);
            }
        }

        private void resizeGrip3_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isResizing2 == true)
            {
                _isResizing2 = false;
                Mouse.Capture(null);
            }
        }

        private void resizeGrip3_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Mouse.Capture(resizeGrip3))
            {
                _isResizing2 = true;
                _startPosition = Mouse.GetPosition(this);
            }
        }

        #endregion

        #region load seq/mrc and save as emd(h5) file

        private void SEQFilePath_Click(object sender, RoutedEventArgs e)
        {
            string folder = SEQPath.Text;
            OpenFileDialog openFileDialog1 = new OpenFileDialog();
            openFileDialog1.Filter = "Cursor Files|*.seq";
            openFileDialog1.Title = "Select a Cursor File";
            //System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                folder = openFileDialog1.FileName;
            }
            folder = folder.Replace("\\", "/");
            SEQPath.Text = folder;
        }


        private void HAADFFilePath_Click(object sender, RoutedEventArgs e)
        {
            string folder = HAADFPath.Text;
            System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                folder = dlg.SelectedPath;
            }
            folder = folder.Replace("\\", "/");
            HAADFPath.Text = folder;
        }

        private void EMDFilePath_Click(object sender, RoutedEventArgs e)
        {
            string folder = EMDPath.Text;
            System.Windows.Forms.FolderBrowserDialog dlg = new System.Windows.Forms.FolderBrowserDialog();
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                folder = dlg.SelectedPath;
            }
            folder = folder.Replace("\\", "/");
            EMDPath.Text = folder;
            string filename = EMDName.Text;
            string fullpath = folder + "/" + filename + ".emd";
        }

        private void DiscardEMD_Click(object sender, RoutedEventArgs e)
        {
            string folder = EMDPath.Text;
            string filename = EMDName.Text;
            string fullpath = folder + "/" + filename + ".emd";
            File.Delete(fullpath);
        }

        // load mrc file that just acquired and do reconstrcution for BF/ABF
        public void LoadnRecon_Click(object sender, RoutedEventArgs e)
        {
            // if EnableDetector option is not checked, change it to ture and use default BF range
            if (EnableDetector.IsChecked == false)
            {
                EnableDetector.IsChecked = true;
                DisableDetector.IsChecked = false;
                slider_innerang.Value = 0;
                slider_outerang.Value = 1;
            }
            // call function to load MRC file and do reconstruction, MRC file when using DE server, SEQ file when using Streampix
            
            UInt32 sizex = 0;
            UInt32 sizey = 0;
            UInt16 numframe = 0;

            //ReadMRCfile();
            SEQ.LoadSEQheader(SEQPath.Text, ref sizex, ref sizey, ref numframe);
            string sent;
            sent = "A total " + numframe + " frames acquired on DE camera in " + SEQPath.Text + " .\n";
            MessageBox.Text += sent;
            sent = " Each frame has " + sizex + " by " + sizey + " pixels.\n";
            MessageBox.Text += sent;
            UInt16[] FirstFrame = new UInt16[sizex * sizey];
            SEQ.LoadFirstFrame(SEQPath.Text, ref FirstFrame);


            // downsampling and rescale first frame before display in 400x400 px image box
            int ratio = (int)Math.Ceiling((double)sizex / 400);
            int sizex_resize = (int)Math.Floor((double)sizex / (double)ratio);
            int sizey_resize = (int)Math.Floor((double)sizey / (double)ratio);
            UInt16[] FirstFrame_resize = new UInt16[sizex_resize * sizey_resize];
            double[] subArray = new double[ratio];
            List<double> subArray_list = new List<double>();

            for (int j = 0; j < sizey_resize ; j++)
            {
                for (int i = 0; i < sizex_resize; i++)
                {
                    Array.Copy(FirstFrame, j * sizex + i * ratio, subArray, 0, ratio);
                    subArray_list.Clear();
                    subArray_list = subArray.ToList();
                    if ((UInt16)subArray_list.Average() < 1500)
                    {
                        FirstFrame_resize[j * sizex_resize + i] = (UInt16)subArray_list.Average();
                    }
                }
            }

            UInt16 maxint = FirstFrame_resize.Max();
            UInt16 minint = FirstFrame_resize.Min();
            FirstFrame_resize = FirstFrame_resize.Select(r => (UInt16)( (double)r / (maxint - minint) * 65535)).ToArray();


            int bytesPerPixel = 2;
            int stride = sizex_resize * bytesPerPixel;
            BitmapSource FirstFramebmpSource = BitmapSource.Create(sizex_resize, sizey_resize, 96,96, PixelFormats.Gray16, null, FirstFrame_resize, stride);
            pictureBox1.Source = FirstFramebmpSource;
        }

        private void ReconFromSEQ_Click(object sender, RoutedEventArgs e)
        {

        }

        // save the loaded mrc file to EMD format
        private void ResaveEMD_Click(object sender, RoutedEventArgs e)
        {
            //HDF5.InitializeHDF(numpos, height, width);
        }


        // function to load 4DSTEM dataset from mrc file, resave as h5, and reconstruct to 2D image with virtual aperture
        public void ReadMRCfile()
        {
            // start reading mrc file
            string path_string = "";
            string name_string = "";
            _deInterface.GetProperty("Autosave Directory", ref path_string);
            _deInterface.GetProperty("Autosave Frames - Previous Dataset Name", ref name_string);
            path_string = path_string.Replace("\\","/");
            string path = path_string + "/" + name_string + "_RawImages.mrc";

            using (var filestream = File.Open(@path, FileMode.Open))
            using (var binaryStream = new BinaryReader(filestream))
            {
                // read headers
                width = binaryStream.ReadInt32();
                height = binaryStream.ReadInt32();
                numpos = binaryStream.ReadInt32();
                int format = binaryStream.ReadInt32();
                for (var i = 0; i < 6; i++)    // the rest 6 integer numbers, int32, useless here
                {
                    binaryStream.ReadInt32();
                    //Console.WriteLine(binaryStream.ReadInt32());
                }
                Console.WriteLine('\n');
                for (var i = 0; i < 12; i++)    // 12 floating numbers, single
                {
                    binaryStream.ReadSingle();
                    //Console.WriteLine(binaryStream.ReadSingle());
                }
                Console.WriteLine('\n');
                for (var i = 0; i < 30; i++)    // 30 integer numbers, int32
                {
                    binaryStream.ReadInt32();
                    //Console.WriteLine(binaryStream.ReadInt32());
                }
                Console.WriteLine('\n');
                for (var i = 0; i < 8; i++)    // 8 chars
                {
                    binaryStream.ReadChar();
                    //Console.WriteLine(binaryStream.ReadChar());
                }
                Console.WriteLine('\n');
                for (var i = 0; i < 2; i++)    // 2 integer numbers, int32
                {
                    binaryStream.ReadInt32();
                    //Console.WriteLine(binaryStream.ReadInt32());
                }
                for (var i = 0; i < 10; i++)    // 10 strings
                {
                    binaryStream.ReadChars(80);
                    //Console.WriteLine(binaryStream.ReadChars(80));
                }

                // finish reading headers
                UInt16[,,] datacube = new UInt16[width, height, numpos];

                // 3D array created for reconstruction and HDF5 file
                for (var ilayer = 0; ilayer < numpos; ilayer++)
                {
                    for (var iy = 0; iy < height; iy++)
                    {
                        for (var ix = 0; ix < width; ix++)
                        {
                            datacube[ix, iy, ilayer] = binaryStream.ReadUInt16(); // [ix, iy, ilayer], correspond to [col, row, layer]
                        }
                    }
                }

                // start reconstruction and show reconstruction result if option enabled
                string StrX = null;
                string StrY = null;
                int px = 0, py = 0;

                // create H5 file with attributes and data
                string fullpath = EMDPath.Text + "/" + EMDName.Text + ".emd" ;
                //H5FileId fileId = HDF5.InitializeHDF(numpos, width, height, datacube,fullpath);


                PosX.Dispatcher.Invoke(
                    (ThreadStart)delegate { StrX = PosX.Text; }
                    );
                PosY.Dispatcher.Invoke(
                    (ThreadStart)delegate { StrY = PosX.Text; }
                    );

                if (Int32.TryParse(StrX, out px))
                {
                    if (Int32.TryParse(StrY, out py))
                    {
                        if (numpos == px * py)
                        {
                            Bitmap ReconBMP = new Bitmap(px, py);   // bitmap for recon purpose
                            UInt16[] recon = new UInt16[px * py]; // array for reconstrcution purpose
                            UInt16[] recon_scale = new UInt16[px * py]; // array for scaled reconstrcuction image
                            ushort sum = 0;
                            int min = 65535;
                            int max = 0;
                            recon_scale[0] = 255;
                            BitmapSource ReconBitmapSource = ConvertBitmapSource(ReconBMP); // convert bitmap to bitmapsource, then can be used to generate writable bitmap
                            InitializeWBmpRecon(ReconBitmapSource);
                            for (var iy = 0; iy < py; iy++)
                            {
                                for (var ix = 0; ix < px; ix++)
                                {
                                    UInt16[] imagelayer = ExtractArray(datacube, iy * px + ix, width, height);
                                    double innerang = 0;
                                    double outerang = 0;
                                    slider_innerang.Dispatcher.Invoke(
                                        (ThreadStart)delegate { innerang = slider_innerang.Value; }
                                        );
                                    slider_outerang.Dispatcher.Invoke(
                                        (ThreadStart)delegate { outerang = slider_outerang.Value; }
                                        );
                                    sum = IntegrateBitmap(imagelayer, width, height, innerang, outerang);
                                    recon[iy*px + ix] = sum;
                                    if (recon[iy * px + ix] < min) min = recon[iy * px + ix];
                                    if (recon[iy * px + ix] > max) max = recon[iy * px + ix]; //update max and min after recon array changed
                                    for (int i = 0; i < iy * px + ix; i++)
                                    {
                                        recon_scale[i] = (ushort)((recon[i] - min) * 255 / (max - min + 1));  // rescale with new max and min if scale changed
                                    }
                                }
                            }
                            this.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                _wBmpRecon.WritePixels(new Int32Rect(0, 0, px, py), recon_scale, px * 2, 0);

                            }));
                            
                        }
                    }
                }
            }
        }

        #endregion

        #region Set AWG and digitizer
        // Configure AWG and digitizer based on current settings
        private void Submit_Setting_Click(object sender, RoutedEventArgs e)
        {
            string pxx = PosX.Text;
            string pxy = PosY.Text;
            string sent;
            // check whether X,Y position box contains number
            bool isNumeric = int.TryParse(pxx, out int n) && int.TryParse(pxy, out int m);
            if (!isNumeric)
            {
                System.Windows.Forms.MessageBox.Show("X/Y position setting is wrong!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // generate scan array padded to 2x size
            double[] Xarray = new double[int.Parse(pxx) * int.Parse(pxy) * 2];
            double[] Yarray = new double[int.Parse(pxx) * int.Parse(pxy) * 2];
            // array that holds voltage index of each scan position
            int[] Xarray_index = new int[Int32.Parse(PosX.Text) * Int32.Parse(PosY.Text) * 2];
            int[] Yarray_index = new int[Int32.Parse(PosX.Text) * Int32.Parse(PosY.Text) * 2];
            double[] Xarray_vol = new double[Int32.Parse(PosX.Text) + 1];
            double[] Yarray_vol = new double[Int32.Parse(PosY.Text) + 1];

            double[] WaveformArray_Ch1 = { };

            sent = "A total " + pxx + " by " + pxy + "scan positions will be generated by arbitrary wave generator.\n";
            MessageBox.Text += sent;

            GenerateScanArray(ref Xarray, ref Yarray, ref Xarray_index, ref Yarray_index, ref Xarray_vol, ref Yarray_vol);

            // push settings to AWG and digitizer, waiting for Xu's API to communicate to hardware
            // Must set AWG first otherwise software will pulse after push settings to digitizer
            PushAWGsetting(Xarray_index, Yarray_index, Xarray_vol, Yarray_vol);
            PushDigitizerSetting(WaveformArray_Ch1);

            //_deInterface.SetProperty("ROI Dimension X", "512");
            //_deInterface.SetProperty("Image Size X", pxx);
            //_deInterface.SetProperty("Image Size Y", pxy);

        }

        // Function used to generate two scan array for AWG channels based on current setting
        // Input: two empty double arrays
        // Save two arrays into Xarray and Yarray
        public void GenerateScanArray(ref double[] Xarray, ref double[] Yarray, ref int[] Xarray_index, ref int[] Yarray_index, ref double[] Xarray_vol, ref double[] Yarray_vol)
        {
            // fractional scan range between [-0.5, 0.5]
            double x_scan_low = (double.Parse(StartX.Text) - 256)/256/2;
            double x_scan_high = (double.Parse(EndX.Text) - 256)/256/2;
            double y_scan_low = (double.Parse(StartY.Text) - 256)/256/2;
            double y_scan_high = (double.Parse(EndY.Text) - 256)/256/2;

            double x_step_num = double.Parse(PosX.Text);
            double y_step_num = double.Parse(PosY.Text);

            // step size in fraction
            double x_step_size = (x_scan_high - x_scan_low) / (double)(x_step_num - 1);
            double y_step_size = (y_scan_high - y_scan_low) / (double)(y_step_num - 1);


            // current scheme uses sawtooth shaped wave
            // array that holds real voltage fraction for each index, final one saves 1

            for (int ix = 0; ix < x_step_num; ix++)
            {
                Xarray_vol[ix] = x_scan_low + x_step_size * ix;
            }

            for (int iy = 0; iy < y_step_num; iy++)
            {
                Yarray_vol[iy] = y_scan_low + y_step_size * iy;
            }
            Xarray_vol[(int)x_step_num] = (double)1;
            Yarray_vol[(int)y_step_num] = (double)1;


            for (int iy = 0; iy < y_step_num ; iy++)
            {
                for (int ix = 0; ix < x_step_num; ix++)
                {
                    if (iy%2 == 0)
                    {
                        //Xarray[iy * Convert.ToInt32(x_step_num) + ix] = x_scan_low + x_step_size * (double)ix;
                        Xarray_index[iy * Convert.ToInt32(x_step_num) + ix] = ix;
                    }
                    else
                    {
                        //Xarray[iy * Convert.ToInt32(x_step_num) + ix] = x_scan_high - x_step_size * (double)ix;
                        Xarray_index[iy * Convert.ToInt32(x_step_num) + ix] = Int32.Parse(PosX.Text) - ix - 1;
                    }
                    
                    //Yarray[iy * Convert.ToInt32(x_step_num) + ix] = y_scan_low + y_step_size * (double)iy;
                    Yarray_index[iy * Convert.ToInt32(x_step_num) + ix] = iy;
                }
            }

            for (int ix = Convert.ToInt32(x_step_num) * Convert.ToInt32(y_step_num); ix < Xarray.Length; ix++)
            {
                Xarray_index[ix] = (int)x_step_num;
            }

            for (int iy = Convert.ToInt32(y_step_num) * Convert.ToInt32(x_step_num); iy < Xarray.Length; iy++)
            {
                Yarray_index[iy] = (int)y_step_num;
            }
        }

        // Function used to write AWG setting onto Xu's API or Chenyu's API
        // 2* x/y_scan_max will always be used as amplitute for two channels, in order to drive beam away in the end
        public void PushAWGsetting(int[] Xarray_index, int[] Yarray_index, double[] Xarray_vol, double[] Yarray_vol)
        {
            //ScanControl_cz.ScanControl_cz status = new ScanControl_cz.ScanControl_cz();
            // Test for passive mode scan control
            ScanControl_cz_passive.ScanControl_cz status = new ScanControl_cz_passive.ScanControl_cz(); 
            status.ScanControlInitialize(x_scan_max * 2, y_scan_max * 2, Xarray_vol, Yarray_vol, Xarray_index, Yarray_index, 0);

            //status.ScanControlInitialize();
            //status.SetScanParameter(Xarray, Yarray, 0); // Xarray, Yarray, delay (x10ns)
            //status.StartScan();                         // start scan and wait for trigger signals
        }

        // Function used to write digitizer setting based on scan grid and frame rate setting
        public void PushDigitizerSetting(double[] WaveformArray_Ch1)
        {

            string sent;
            bool isNumeric = int.TryParse(FrameRate.Text, out int n);
            if (!isNumeric)
            {
                System.Windows.Forms.MessageBox.Show("Frame rate setting is wrong!", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            int record_size;
            record_size = Int32.Parse(PosX.Text) * Int32.Parse(PosY.Text) * 10;
            record_size = Convert.ToInt32(record_size * 1.3);
            sent = "A total " + record_size + "samples will be recorded by digitizer.\n";
            MessageBox.Text += sent ;
            int recording_rate = Int32.Parse(FrameRate.Text) * 10;
            sent = "Digitizer frame rate is set to " + recording_rate + " samples per second.\n";
            MessageBox.Text += sent ;
            //double[] WaveformArray_Ch1 = { };
            // this function can only be called when running on DE camera computer with Keysight libraries
            Digitizer.Program.FetchData(record_size, recording_rate, ref WaveformArray_Ch1);
            // reconstruct and show image in ROI box when data fetched from digitizer
            HAADFreconstrcution(WaveformArray_Ch1, Int32.Parse(PosX.Text), Int32.Parse(PosY.Text), 1);

        }

        #endregion

        #region old scheme to stream image from camera       
        // start live view by clicking 'stream from DE'
        public void btnLiveCapture_Click(object sender, RoutedEventArgs e)
        {
            
            if (_liveModeEnabled)
            {
                _liveModeEnabled = false;
                btnLiveCapture.Content = "Stream from DE";
                _updateTimer.Stop();
                //Dispatcher.InvokeShutdown();      // this just somehow works to stop streaming the image, software would go through BeginInvoke once then idle
            }
            else
            {
                
                //HDF5.InitializeHDF();   // initialize the HDF file used to save 3D data cube
                bool ImageRecon = false;
                if (EnableDetector.IsChecked == true) ImageRecon = true;
                ImageCount = 0;
                semaphore = new Semaphore(0, 1);
                new LiveModeView();
 //               Closing += LiveViewWindow_Closing;
                InitializeWBmp(GetImage()); // only used to display image on imagebox.1
                Show();
                _updateTimer = new System.Timers.Timer(10);
                _updateTimer.Elapsed += new ElapsedEventHandler(_updateTimer_Elapsed);

                InitializeWBmp(GetImage()); // initialize image in picture box
                                            //enable livemode on the server
                _deInterface.EnableLiveMode();
                _liveModeEnabled = true;
                btnLiveCapture.Content = "Stop Streaming";

                // start new task for background image rendering
                // determine size for each image

                string xSize = "";
                string ySize = "";
                _deInterface.GetProperty("Image Size X", ref xSize);
                _deInterface.GetProperty("Image Size Y", ref ySize);
                width = Convert.ToInt32(xSize);
                height = Convert.ToInt32(ySize);

                // determine how many frames to take
                string StrX = null;
                string StrY = null;
                int px = 0, py = 0;
                numpos = 0;

                PosX.Dispatcher.Invoke(
                    (ThreadStart)delegate { StrX = PosX.Text; }
                    );
                PosY.Dispatcher.Invoke(
                    (ThreadStart)delegate { StrY = PosX.Text; }
                    );

                if (Int32.TryParse(StrX, out px))
                {
                    if (Int32.TryParse(StrY, out py))
                    {
                        numpos = px * py;
                    }
                }

                //H5FileId fileId = HDF5.InitializeHDF(numpos, width, height);
                UInt16[,,] datacube = new UInt16[numpos,width,height];    // generate the data cube, each value should be an integer
                UInt16[] image = new UInt16[width*height];  // 1D image array used to save temp 2D frame
                // generate reconstruction bitmap and initialize _wBmpRecon
                UInt16[] recon = new UInt16[px * py]; // array for reconstrcution purpose
                UInt16[] recon_scale = new UInt16[px * py]; // array for scaled reconstrcuction image
                Bitmap ReconBMP = new Bitmap(px, py);   // bitmap for recon purpose
                BitmapSource ReconBitmapSource = ConvertBitmapSource(ReconBMP); // convert bitmap to bitmapsource, then can be used to generate writable bitmap
                InitializeWBmpRecon(ReconBitmapSource);

                int length = px * py;
                ushort min = recon[0];
                ushort max = recon[0];

                semaphore.Release();
                int nTickCount = 0;
                Task.Factory.StartNew(() =>
                {
                    while (_liveModeEnabled)
                    {
                        System.Threading.Thread.Sleep(1);
                        {
                            if (m_imageQueue.Count > 0)
                            // scale and display image
                            {
                                semaphore.WaitOne();
                                image = m_imageQueue.Dequeue();
                                semaphore.Release();
                                SetImage(image, width, height);
                                SetImageLoadTime(nTickCount);
                                // fill in array 'recon' for reconstruction image
                                double innerang = 0;
                                double outerang = 0;
                                slider_innerang.Dispatcher.Invoke(
                                    (ThreadStart)delegate { innerang = slider_innerang.Value; }
                                    );
                                slider_outerang.Dispatcher.Invoke(
                                    (ThreadStart)delegate { outerang = slider_outerang.Value; }
                                    );
                                
                                recon[ImageCount-1] = IntegrateBitmap(image, width, height, innerang, outerang);
                            }
                            if(ImageCount==1 && ImageRecon)   // case for first pixel
                            {
                                
                                min = recon[0];
                                max = recon[0];
                                recon_scale[0] = 255;  // rescale with new max and min
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    _wBmpRecon.WritePixels(new Int32Rect(0, 0, px, py), recon_scale, px * 2, 0);

                                }));

                                for (int x = 0; x < width; x++)
                                {
                                    for (int y = 0; y < height; y++)
                                    {
                                        datacube[0, x, y] = image[x * height + y];
                                    }
                                }
                            }

                            if(ImageCount > 1 && ImageRecon)
                            {
                                // imagecount would increase by 1 after setimage function, one more number on recon array
                                if (recon[ImageCount - 1] < min) min = recon[ImageCount - 1];
                                if (recon[ImageCount - 1] > max) max = recon[ImageCount - 1]; //update max and min after recon array changed
                                for (int i = 0; i < ImageCount; i++)
                                {
                                    recon_scale[i] = (ushort)((recon[i] - min) * 255 / (max - min + 1));  // rescale with new max and min
                                }
                                this.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    _wBmpRecon.WritePixels(new Int32Rect(0, 0, px, py), recon_scale, px * 2, 0);

                                }));
                                for (int x = 0; x < width; x++)
                                {
                                    for (int y = 0; y < height; y++)
                                    {
                                        datacube[ImageCount-1, x, y] = image[x*height+y];
                                    }
                                }
                            }
                            // criteria to stop image acquisition
                            if (ImageCount == numpos)
                            {
                                //HDF5.WriteDataCube(fileId, datacube);
                                _liveModeEnabled = false;
                                Dispatcher.BeginInvoke((Action)(() =>
                                {
                                    btnLiveCapture.Content = "Stream from DE";  //invoke is needed to control btn from another thread
                                }));
                                ImageCount = 0;
                                _updateTimer.Stop();
                                return;
                            }
                        }
                    }
                }).ContinueWith(p =>
                {

                });

                Task.Factory.StartNew(() =>
                {
                    while (_liveModeEnabled)
                    {
                        {
                            int nTickCountOld = 0;
                            //UInt16[] image;
                            nTickCountOld = System.Environment.TickCount;
                            _deInterface.GetImage(out image);   //get image from camera
                            nTickCount = System.Environment.TickCount - nTickCountOld; // get time elapsed
                            semaphore.WaitOne();
                            m_imageQueue.Enqueue(image);
                            semaphore.Release();
                        }
                        System.Threading.Thread.Sleep(1);
                    }
                }).ContinueWith(o =>
                {
                    if (_deInterface.isConnected())
                        _deInterface.DisableLiveMode();
                });
            }
        }

        #endregion

        #region start image acquisition in DE slave mode

        private void btnGetImage_Click(object sender, RoutedEventArgs e)
        {
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            SingleCapture();
            stopWatch.Stop();
            TimeSpan ts = stopWatch.Elapsed;
            string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
            ts.Hours, ts.Minutes, ts.Seconds,
            ts.Milliseconds / 10);
            Console.WriteLine("RunTime " + elapsedTime);
            System.Windows.Forms.MessageBox.Show("Image acquisition finished.\n Total time: " + elapsedTime);
        }

        private void DE_SlaveMode(object sender, RoutedEventArgs e)
        {
            btnGetImage.IsEnabled = true;
        }
        private void DE_MasterMode(object sender, RoutedEventArgs e)
        {
            btnGetImage.IsEnabled = false;
        }

        #endregion

        #region old scheme to capture single image from DE camera
        public void SingleCapture()
        // Get a 16 bit gray scale image from the server and return a BitmapSource
        {
            try
            {
                // old scheme of single acquisition in a new window
                /*ImageView imageView = new ImageView();
                imageView.image.Source = GetImage();    //return a BitmapSource
                imageView.Show();*/

                // image acquisition scheme adapted from live stream, display image in imagebox1
                InitializeWBmp(GetImage());
                Show();
                //InitializeWBmp(GetImage()); // initialize image in picture box
                //enable livemode on the server
            }
            catch (Exception exc)
            {
                System.Windows.MessageBox.Show(exc.Message);
            }
        }

        private BitmapSource GetImage()
        {
            UInt16[] image;
            _deInterface.GetImage(out image);
            if (image == null)
            {
                DeError error = _deInterface.GetLastError();
                Console.WriteLine(error.Description);
                return null;
            }
            string xSize = "";
            string ySize = "";
            _deInterface.GetProperty("Image Size X", ref xSize);
            _deInterface.GetProperty("Image Size Y", ref ySize);
            width = Convert.ToInt32(xSize);
            height = Convert.ToInt32(ySize);
            int bytesPerPixel = (PixelFormats.Gray16.BitsPerPixel + 7) / 8;
            int stride = 4 * ((width * bytesPerPixel + 3) / 4);

            int length = width * height;
            ushort min = image[0];
            ushort max = image[0];
            for (int i = 1; i < length; i++)
            {
                if (image[i] < min) min = image[i];
                if (image[i] > max) max = image[i];
            }
            double gain = UInt16.MaxValue / Math.Max(max - min, 1);
            UInt16[] image16 = new UInt16[length];
            // load data into image16, 1D array for 2D image
            for (int i = 0; i < length; i++)
                image16[i] = (ushort)((image[i] - min) * gain);

            byte[] imageBytes = new byte[stride * height];


            return BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray16, null, image16, stride);
        }
        #endregion

        #region other functions related to image display and GUI

        // function used to extract 2D layer from 3D datacube, used for 1D array saving scheme
        public UInt16[] ExtractArray(UInt16[,,] DataCube, int layernum, int width, int height)
        {
            UInt16[] layer = new UInt16[width * height];
            for (var iy = 0; iy < width; iy++)
            {
                for (var ix = 0; ix < height; ix++)
                {
                    layer[iy * width + ix] = DataCube[ix, iy, layernum];
                }
            }
            return layer;
        }

        // function used to extract 2D layer from 3D datacube
        public UInt16[,] ExtractLayer(UInt16[,,] DataCube, int layernum, int width, int height)
        {
            UInt16[,] layer = new UInt16[width, height];
            for (var iy = 0; iy < width; iy++)
            {
                for (var ix = 0; ix < height; ix++)
                {
                    layer[iy, ix] = DataCube[layernum, iy, ix];
                }
            }
            return layer;
        }

        public static void SaveClipboardImageToFile(string filePath)
        {
            var image = System.Windows.Clipboard.GetImage();
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                encoder.Save(fileStream);
            }
        }

        // click to start using virtual detector to reconstrcut image
        private void button1_Click(object sender, RoutedEventArgs e)
        {
            SolidColorBrush strokeBrush = new SolidColorBrush(Colors.Red);
            strokeBrush.Opacity = .25d;
            InnerAngle.Visibility = Visibility.Visible;
            InnerAngle.Stroke = strokeBrush;
            InnerAngle.Height = 400;
            InnerAngle.StrokeThickness = InnerAngle.Height / 2;
            slider_outerang.Value = 1;
            slider_innerang.Value = 0;
        }

        // called by change on innerang slider, change the radius of inner angle ellipse
        private void changeinnerang(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double innerang = slider_innerang.Value;
            InnerAngle.StrokeThickness = InnerAngle.Width / 2 * (1.0 - innerang);

        }

        // called by change on outerang slider, will simultaneously change ellipse thickness according to innerang
        private void changeouterang(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            double outerang = slider_outerang.Value;
            Thickness margin = InnerAngle.Margin;
            margin.Top = 7.0 + 200.0 * (1.0 - outerang);
            InnerAngle.Margin = margin;
            InnerAngle.Height = 400.0 - margin.Top - margin.Top + 14.0;
            InnerAngle.Width = InnerAngle.Height;
            InnerAngle.StrokeThickness = InnerAngle.Width / 2 * (1.0 - slider_innerang.Value);
        }

        public Bitmap CreateBitmap(ushort[] imagedata, int pxx, int pxy)
        {
            System.Drawing.Bitmap flag = new System.Drawing.Bitmap(pxx, pxy);
            for (int x = 0; x < pxx; x++)
            {
                for (int y = 0; y < pxy ; y++)
                {
                    int pixel = pxx * y + x;
                    flag.SetPixel(x, y, System.Drawing.Color.FromArgb(imagedata[pixel],imagedata[pixel],imagedata[pixel]));
                }
            }

            return flag;
        }

        // convert bitmap to BitmapSource
        public BitmapSource ConvertBitmapSource(System.Drawing.Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height, 96, 96, PixelFormats.Gray8, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }

        public void SetImage(UInt16[] imageData, int width, int height)
        {
            // Scale image
            int length = width * height;
            ushort min = imageData[0]; ushort max = imageData[0];
            for (int i = 1; i < length; i++)
            {
                if (imageData[i] < min) min = imageData[i];
                if (imageData[i] > max) max = imageData[i];
            }
            double gain = UInt16.MaxValue / Math.Max(max - min, 1);
            UInt16[] image16 = new UInt16[length];
            for (int i = 0; i < length; i++)
            {
                image16[i] = (ushort)((imageData[i] - min) * gain);
            }
            if (_firstImage)
            {
                _renderStart = DateTime.Now;
                _updateTimer.Start();
                _firstImage = false;
            }

            //use the dispatcher to invoke onto the UI thread for image displaying
            this.Dispatcher.BeginInvoke(new Action(() =>
            {
                //write the image data to the WriteableBitmap buffer
                _wBmp.WritePixels(new Int32Rect(0, 0, width, height), image16, width * 2, 0);

            }));
            ImageCount++;
            
        }

        // use 1D array as input, sum up intensity within range to return one single value
        public UInt16 IntegrateBitmap(UInt16[] imageData, int pxx, int pxy, double innerang, double outerang)
        {
            double centerx = pxx / 2;
            double centery = pxy / 2;
            if (pxx > pxy)
            {
                outerang = pxx * outerang;
            }
            else
            {
                outerang = pxy * outerang;
            }
                // use the smaller one among pxx and pxy to calculate outerang
            innerang = outerang * innerang;
            UInt16 sum=0;
            for (int i = 0; i<pxx; i++)
            {
                for (int j = 0; j < pxy;j++)
                {
                    double distance = Math.Pow(Convert.ToDouble(i - centerx), 2) + Math.Pow(Convert.ToDouble(j - centery), 2);
                    distance = Math.Sqrt(distance);
                    if (distance < outerang && distance > innerang)
                    {
                        sum += imageData[i+j*pxx];
                    }
                }
            }
            return sum;
        }

/*        private void LiveViewWindow_Closing(object sender, CancelEventArgs e)
        {
            _liveModeEnabled = false;
            Dispatcher.BeginInvoke((Action)(() =>
            {
                btnLiveCapture.Content = "Test Load Image Speed";
            }));
        }
*/

        // only get useful properties instead of getting all properties
        private void cmbCameras_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            CameraProperties.Clear();

            if (!_deInterface.isConnected())
                return;
            _deInterface.SetCameraName(cmbCameras.SelectedItem.ToString());

            List<String> props = new List<String>();
            props.Add("Acquisition Mode");
            props.Add("Autosave Raw Frames");
            props.Add("Autosave Directory");
            props.Add("Autosave Frames - Previous Dataset Name");
            props.Add("Binning X");
            props.Add("Binning Y");
            props.Add("Camera Position");
            props.Add("Exposure Time (seconds)");
            props.Add("Exposure Time Max (seconds)");
            props.Add("Frames Per Second");
            props.Add("Frames Per Second (Max)");
            props.Add("ROI Dimension X");
            props.Add("ROI Dimension Y");
            props.Add("ROI Offset X");
            props.Add("ROI Offset Y");
            props.Add("Sensor Hardware Binning");
            props.Add("Sensor Hardware ROI");
            props.Add("Total Number of Frames");

            //_deInterface.GetPropertyNames(ref props);

            foreach (string propertyName in props)
            {
                string value = string.Empty;
                _deInterface.GetProperty(propertyName, ref value);
                CameraProperties.Add(new Property { Name = propertyName, Value = value });
            }
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _deInterface = new DeInterfaceNET();
            //observable collection for Camera properties, used for binding with DataGrid
            CameraProperties = new ObservableCollection<Property>();
            NotifyPropertyChanged("CameraProperties");
        }

        private void _updateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (TotalSeconds > 1)   // add an extra criteria to avoid bugs when time is shorter than 1 second
            {
                NotifyPropertyChanged("Fps");
                NotifyPropertyChanged("TotalSeconds");
            }

        }

        public void InitializeWBmp(BitmapSource bmpSource)
        {
            // Initialize the WriteableBitmap with a BitmapSource for the image specs
            //_wBmp = new WriteableBitmap(bmpSource);

            _wBmp = new WriteableBitmap(bmpSource.PixelWidth, bmpSource.PixelHeight, bmpSource.DpiX, bmpSource.DpiY, bmpSource.Format, bmpSource.Palette);
            pictureBox1.Source = _wBmp; // display _wBmp to pictureBox1, will be called only once
        }

        public void InitializeWBmpRecon(BitmapSource bmpSource)
        {
            //_wBmp = new WriteableBitmap(bmpSource);

            _wBmpRecon = new WriteableBitmap(bmpSource.PixelWidth, bmpSource.PixelHeight, bmpSource.DpiX, bmpSource.DpiY, bmpSource.Format, bmpSource.Palette);
            Recon.Dispatcher.Invoke(
                (ThreadStart)delegate { Recon.Source = _wBmpRecon; }
            );
        }

        public void SetImageLoadTime(int nTickCount)
        {
            this.nTickCount = nTickCount;
            NotifyPropertyChanged("Ilt");
        }

        private void EnableDetector_click(object sender, RoutedEventArgs e)
        {
            DisableDetector.IsChecked = false;
            SolidColorBrush strokeBrush = new SolidColorBrush(Colors.Red);
            strokeBrush.Opacity = .25d;
            InnerAngle.Visibility = Visibility.Visible;
            InnerAngle.Stroke = strokeBrush;
            InnerAngle.Height = 400;
            InnerAngle.StrokeThickness = InnerAngle.Height / 2;
            slider_outerang.Value = 1;
            slider_innerang.Value = 0;
            //ReadMRCfile();
        }

        private void DisableDetector_click(object sender, RoutedEventArgs e)
        {
            InnerAngle.Visibility = Visibility.Hidden;
            EnableDetector.IsChecked = false;
        }

        private void EnableDetector_Checked(object sender, RoutedEventArgs e)
        {
        }

        private void Reset_settings(object sender, RoutedEventArgs e)
        {

        }
        #endregion


    }




    /*        #region INotifyPropertyChanged

            public event PropertyChangedEventHandler PropertyChanged;
            private void NotifyPropertyChanged(String info)
            {
                if (PropertyChanged != null)
                {
                    PropertyChanged(this, new PropertyChangedEventArgs(info));
                }
            }

            #endregion
    */
}

    public class Property
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }

    enum ImageTransfer_Mode
    {
        ImageTransfer_Mode_TCP = 1,		// Use TCP/IP connected protocol (original mode)
        ImageTransfer_Mode_MMF = 2		// Use memory mapped file share buffer (local client only)
    };

    struct Mapped_Image_Data_
    {
        public bool client_opened_mmf;			// set to true by local client before connection to server
        public System.UInt32 buffer_size_;			// size of image buffer
        public System.UInt32 image_id_;		// image id, incremented with each new image transferred
        public System.UInt32 image_size_;		// image size in bytes
        public System.UInt32 img_start_;	// first pixel of image buffer
    };
