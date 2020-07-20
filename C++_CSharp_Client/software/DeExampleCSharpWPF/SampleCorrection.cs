using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using AForge;
using AForge.Math;
using TEMControlWrapper;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;

namespace SampleCorrection
{
    public enum BeamBlank
    {
        BlankOFF,
        BlankON
    }

    // Module for sample drift correction
    // Note this correction method only works for square frame with a size of 2^n
    public class DriftCorr
    {
        private double dx;
        private double dy;
        private Microscope TEM;
        private Complex[,] cimg_ref;
        public int width;
        public int height;
        public double MAG;


        // Initialze microscope connection
        public void InitMicroscope()
        {
            RemotingConfiguration.Configure(@"..\..\app.config", false);
            Console.WriteLine("Connecting remote server...");
            try
            {
                TEM = new Microscope();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Microscope control initialization failed");
            }
        }

        public void BeamBlankSwitch(BeamBlank blank)
        {
            bool blankdir = blank == BeamBlank.BlankON ? true : false;
            TEM.SwitchBeamBlank(blankdir);
        }

        // Initialize drift correction method
        public void InitCorrection(string fp0, int SizeX, int SizeY)
        {
            // Set image size
            width = SizeX;
            height = SizeY;

            // Load reference frame and perform 2D FFT
            double[,] data = new double[height, width];
            LoadHAADFData(fp0, data);
            cimg_ref = FromHAADFData(data);
            FourierTransform.FFT2(cimg_ref, FourierTransform.Direction.Forward);

            MAG = TEM.GetMag();
        }

        public double Pix2Mtr(double pix)
        {
            double drf_real;
            var Mags = File.ReadAllLines(@"Mag_Calib.txt").Select(x => double.Parse(x.Split('\t')[0])).ToArray();
            var FOVs = File.ReadAllLines(@"Mag_Calib.txt").Select(x => double.Parse(x.Split('\t')[1])).ToArray();

            // Calculate drift value in terms of meters
            int Mag_index = Array.IndexOf(Mags, MAG);
            double mpp = FOVs[Mag_index] * 1E-9 / width;
            drf_real = mpp * pix;

            return drf_real;
        }

        // Load HAADFData into 2D array
        public void LoadHAADFData(string fp, double[,] img)
        {
            var data = File.ReadLines(fp).Where(line => line != "").ToArray();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    img[y, x] = Convert.ToDouble(data[y * width + x]);
                }
            }
        }

        // Convert HAADFData to 2D Complex structure
        public Complex[,] FromHAADFData(double[,] img)
        {
            Complex[,] img_cpx = new Complex[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    img_cpx[y, x] = new Complex(img[y, x], 0.0);
                }
            }

            return img_cpx;
        }

        // Calculate sample drift from drifted frame and reference frame using a cross correlation method in Fourier space
        // This method is whole-pixel registration, which should be sufficient for in-aquisition drift correction
        public void Get_Drift(double[,] img_drf)
        {
            Complex[,] cimg_drf = FromHAADFData(img_drf);
            FourierTransform.FFT2(cimg_drf, FourierTransform.Direction.Forward);

            // Compute cross correlation
            Complex[,] product = new Complex[height, width];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    cimg_drf[y, x].Im *= -1;// get conjugate
                    product[y, x] = Complex.Multiply(cimg_ref[y, x], cimg_drf[y, x]);
                }
            }

            FourierTransform.FFT2(product, FourierTransform.Direction.Backward);
            double[,] cross_corr = new double[height, width];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    cross_corr[y, x] = product[y, x].Magnitude;
                }
            }

            // Locate maximum
            double Cmax = 0;
            int[] max_index = { -1, -1 };
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    double currentPix = cross_corr[y, x];
                    if (currentPix > Cmax)
                    {
                        Cmax = currentPix;
                        max_index = new int[] { y, x };
                    }
                }
            }

            // Calculate drift
            int[] mid_point = { height / 2, width / 2 };
            for (int ind = 0; ind < 2; ind++)
                max_index[ind] = (max_index[ind] > mid_point[ind]) ? max_index[ind] - 2 * mid_point[ind] : max_index[ind];
            dy = Pix2Mtr((double)max_index[0]);
            dx = Pix2Mtr((double)max_index[1]);
        }

        // Correct sample drift
        public void CorrectDrift(string fp)
        {
            double[,] img = new double[height, width];
            LoadHAADFData(fp, img);
            Get_Drift(img);
            Console.WriteLine($"Correcting sample drift: x drift {dx} angstroms; y drift {dy} angstroms.\n");

            // perform correction action
            try
            {
                TEM.DoImgSft(dx, dy);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine("Drift correction failed");
            }

        }

        // Save microscope status into text file
        public void SaveMicroscopeState(string fp)
        {
            DateTime now = DateTime.Now;
            string filename;
            filename = "Acquisition_" + now.ToString("MM/d/yyyy_HH:mm") + ".txt";
            fp += filename;
            Console.WriteLine($"Saving status file in {fp}.");
            TEM.SaveStatus(fp);
        }

    }

}