using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DeInterface;
using System.IO;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.ObjectModel;

namespace DeExampleCSharp
{
    public partial class Form1 : Form
    {
        private DeInterfaceNET ifc;
        private ImageForm imageForm;

        public Form1()
        {
            ifc = new DeInterfaceNET();
            InitializeComponent();
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            if (connectButton.Text == "Disconnect")
            {
                ifc.close();
                cameraComboBox.Items.Clear();
                cameraComboBox.Text = "";
                propertyListView.Items.Clear();
                propertyListView.Text = "";
                connectButton.Text = "Connect";
                captureButton.Hide();
            }
            else if (ifc.connect("127.0.0.1", 48880, 48879))
            {
                try
                {
                    List<String> cameras = new List<String>();
                    ifc.GetCameraNames(ref cameras);
                    cameraComboBox.Items.Clear();
                    cameraComboBox.MaxDropDownItems = cameras.Count;
                    cameraComboBox.Items.AddRange(cameras.ToArray());
                    cameraComboBox.SelectedIndex = 0;
                }
                catch (Exception exc)
                {
                    MessageBox.Show(exc.Message);
                }

                connectButton.Text = "Disconnect";
                captureButton.Show();

            }
        }

        private void cameraComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ifc.SetCameraName(cameraComboBox.Text);
            List<String> props = new List<String>();
            ifc.GetPropertyNames(ref props);
            propertyListView.Items.Clear();
            for (int i = 0; i < props.Count; i++)
            {
                String value = "";
                ifc.GetProperty(props[i], ref value);
                ListViewItem listItem = new ListViewItem(new string[] {props[i], value});
                propertyListView.Items.Insert(i, listItem);
            }
        }

        private void captureButton_Click(object sender, EventArgs e)
        {
            UInt16[] image;
            ifc.GetImage(out image);
            try
            {
                string xSize = "";
                string ySize = "";
                ifc.GetProperty("Image Size X", ref xSize);
                ifc.GetProperty("Image Size Y", ref ySize);
                int width = Convert.ToInt32(xSize);
                int height = Convert.ToInt32(ySize);
                int bytesPerPixel = (PixelFormats.Gray16.BitsPerPixel + 7) / 8;
                int stride = 4 * ((width * bytesPerPixel + 3) / 4);

                int length = width * height;
                int min = image[0];
                int max = image[0];
                for (int i = 1; i < length; i++)
                {
                    if (image[i] < min) min = image[i];
                    if (image[i] > max) max = image[i];
                }
                double gain = UInt16.MaxValue / Math.Max(max - min, 1);
                UInt16[] image16 = new UInt16[length];
                for (int i = 0; i < length; i++)
                    image16[i] = Convert.ToUInt16(Math.Min(((int)image[i] - min) * gain, UInt16.MaxValue));

                byte[] imageBytes = new byte[stride * height];

                imageForm = new ImageForm();
                BitmapSource bitSrc = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray16, null, image16, stride);
                imageForm.SetImage(bitSrc);
                imageForm.Show();

         /*       // use an 8-bit image for display 
                // 16-bit BMPs are not supported by Microsoft
                // 8-bits requires a palette since colors are indexed
                // note that bitmap display in C# is slow
                Bitmap bmp = new Bitmap(Convert.ToInt32(xSize), 
                                        Convert.ToInt32(ySize), 
                                        PixelFormat.Format8bppIndexed);
                ColorPalette palette = bmp.Palette;
                for (int i = 0; i < palette.Entries.Count(); i++)
                    palette.Entries[i] = Color.FromArgb(i, i, i);
                bmp.Palette = palette;
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height),
                                                  ImageLockMode.ReadWrite,
                                                  PixelFormat.Format8bppIndexed);
                IntPtr ptr = bmpData.Scan0;
                int stride = bmpData.Stride;
                Byte[] rgbArray = new Byte[bmp.Width];
                for (int r = 0; r < bmp.Height; ++r)
                {
                    // convert 16-bit array to 8-bit array
                    for (int c = 0; c < bmp.Width; c++)
                        rgbArray[c] = Convert.ToByte(image[r * bmp.Width + c]);
                    Marshal.Copy(rgbArray, 0, new IntPtr((int)ptr + stride * r), bmp.Width);
                }
                bmp.UnlockBits(bmpData);
                imageForm = new ImageForm();
                imageForm.imagePictureBox.Image = bmp;
                imageForm.Show();*/
            }
            catch (Exception exc)
            {
                MessageBox.Show(exc.Message);
            }
        }
    }
}
