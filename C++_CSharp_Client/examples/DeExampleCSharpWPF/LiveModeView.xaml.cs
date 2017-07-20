using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Timers;

namespace DeExampleCSharpWPF
{
    /// <summary>
    /// Interaction logic for LiveModeView.xaml
    /// </summary>
    public partial class LiveModeView : Window, INotifyPropertyChanged
    {
        private int _imageCount = 0;
        private bool _firstImage = true;
        private DateTime _renderStart;
        private Timer _updateTimer;
        private WriteableBitmap _wBmp;
        private int nTickCount = 0;
        private decimal dTickCountAvg = 0;
        private int nCount = 0;
        public decimal Fps
        {
            get { return Math.Round(Convert.ToDecimal(_imageCount / TotalSeconds), 3); }
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

        public double TotalSeconds {
            get 
            {
                if (_firstImage == false)
                    return Math.Round(((DateTime.Now - _renderStart).TotalMilliseconds) / 1000);
                else
                    return 1;   // return 0 would cause N/0 error
            } 
        }

        public LiveModeView()
        {
            InitializeComponent();
            _updateTimer = new Timer(1000);
            _updateTimer.Elapsed += new ElapsedEventHandler(_updateTimer_Elapsed);
        }

        /// <summary>
        /// update FPS and TotalSeconds properties
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _updateTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            NotifyPropertyChanged("Fps");
            NotifyPropertyChanged("TotalSeconds");

        }

        /// <summary>
        /// Initialize the WriteableBitmap with a BitmapSource for the image specs
        /// </summary>
        /// <param name="bmpSource"></param>
        public void InitializeWBmp(BitmapSource bmpSource)
        {
            //_wBmp = new WriteableBitmap(bmpSource);

            _wBmp = new WriteableBitmap(bmpSource.PixelWidth, bmpSource.PixelHeight, bmpSource.DpiX, bmpSource.DpiY, bmpSource.Format, bmpSource.Palette);
            image.Source = _wBmp;
        }

        /// <summary>
        /// Set the image to the array of ushorts representing a 16 bit grayscale image
        /// </summary>
        /// <param name="imageData"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
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

        public void SetImageLoadTime(int nTickCount)
        {
            this.nTickCount = nTickCount;
            NotifyPropertyChanged("Ilt");
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
    }
}
