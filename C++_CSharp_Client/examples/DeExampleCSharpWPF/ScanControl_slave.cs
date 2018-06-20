using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeysightSD1;

// 6/11/2018: Modified this scan control procedure to do conventional scan
// AWG test code copied from Keysight with a few modifications
namespace ScanControl_slave
{
    public enum HW_STATUS_RETURNS
    {
        HW_SUCCESS,
        HW_OTHER
    }


    public class ScanControl_cz
    {
        private List<double> xpoints;
        private List<double> ypoints;
        private List<int> xindex;
        private List<int> yindex;

        public HW_STATUS_RETURNS ScanControlInitialize(double x_amp, double y_amp, double[] Xarray_vol, double[] Yarray_vol, int[] Xarray_index, int[] Yarray_index, double delay, int recording_rate)
        {
            int status;
            // Channel 1 for y scan and channel 2 for x scan

            //Create an instance of the AOU module
            SD_AOU moduleAOU = new SD_AOU();
            string ModuleName = "M3201A";
            int nChassis = 1;
            int nSlot = 3;

            if ((status = moduleAOU.open(ModuleName, nChassis, nSlot)) < 0)
            {
                Console.WriteLine("Error openning the Module 'M3201A', make sure the slot and chassis are correct. Aborting...");
                Console.ReadKey();

                return HW_STATUS_RETURNS.HW_SUCCESS;
            }

            // Determine prescaling factor and number of samples per step to use

            int nSamples;
            int Prescaling;

            nSamples = (int) Math.Ceiling(1e8 / recording_rate / 4095);
            Prescaling = (int) Math.Ceiling(1e8 / recording_rate / nSamples);
            while (Prescaling > 1.05e8/recording_rate/nSamples)
            {
                nSamples++;
                Prescaling = (int)Math.Ceiling(1e8 / recording_rate / nSamples);
            }

            // Config amplitude and setup AWG in channels 1 and 2,
            moduleAOU.channelAmplitude(1, y_amp);
            moduleAOU.channelWaveShape(1, SD_Waveshapes.AOU_AWG);
            moduleAOU.channelAmplitude(2, x_amp);
            moduleAOU.channelWaveShape(2, SD_Waveshapes.AOU_AWG);
            moduleAOU.waveformFlush();

            // Convert array into list

            xpoints = new List<double>();
            ypoints = new List<double>();
            xindex = new List<int>();
            yindex = new List<int>();

            xpoints.Clear();
            ypoints.Clear();
            xindex.Clear();
            yindex.Clear();
            xpoints = Xarray_vol.ToList();
            ypoints = Yarray_vol.ToList();
            xindex = Xarray_index.ToList();
            yindex = Yarray_index.ToList();

            status = moduleAOU.AWGflush(1);
            status = moduleAOU.AWGflush(2);
            status = moduleAOU.AWGflush(3);
            status = moduleAOU.AWGflush(4);



            /// Generate and queue waveform for X channel (channel 2)
            var Waveform_X = new double[nSamples * xindex.Count()];
            int Count = 0;
            for (int ix = 0; ix < xindex.Count; ix++)
            {
                for (int i = 0; i < nSamples; i++)
                {
                    Waveform_X[Count] = xpoints[ix];
                    Count++;
                }
            }
            var SD_Waveform_X = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, Waveform_X);
            status = moduleAOU.waveformLoad(SD_Waveform_X, 0, 1);       // padding option 1 is used to maintain ending voltage after each WaveForm

            if (status < 0)
            {
                Console.WriteLine("Error while loading x waveform");
            }

            status = moduleAOU.AWGqueueWaveform(2, 0, SD_TriggerModes.AUTOTRIG, 0, yindex.Count, Prescaling);
            if (status < 0)
            {
                Console.WriteLine("Error while queuing x waveform");
            }


            /// Generate and queue waveform for Y channel (channel 1)
            var Waveform_Y = new double[nSamples * xindex.Count() * yindex.Count()];
            Count = 0;
            for (int iy = 0; iy < yindex.Count(); iy++)
            {
                for (int ix = 0; ix < xindex.Count(); ix++)
                {
                    for (int i = 0; i < nSamples; i++)
                    {
                        Waveform_Y[Count] = ypoints[iy];
                        Count++;
                    }
                }
            }
            var SD_Waveform_Y = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, Waveform_Y);
            status = moduleAOU.waveformLoad(SD_Waveform_Y, 1, 1);       // padding option 1 is used to maintain ending voltage after each WaveForm

            if (status < 0)
            {
                Console.WriteLine("Error while loading x waveform");
            }

            status = moduleAOU.AWGqueueWaveform(1, 1, SD_TriggerModes.AUTOTRIG, 0, 1, Prescaling);
            if (status < 0)
            {
                Console.WriteLine("Error while queuing x waveform");
            }


            /// Generate and queue waveform for DE trigger (channel 3)
            var Waveform_DE = new double[nSamples * xindex.Count()];
            Count = 0;
            for (int ix = 0; ix < xindex.Count; ix++)
            {
                Waveform_DE[ix * nSamples] = 1;
            }
            var SD_Waveform_DE = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, Waveform_DE);
            status = moduleAOU.waveformLoad(SD_Waveform_X, 2, 1);       // padding option 1 is used to maintain ending voltage after each WaveForm

            if (status < 0)
            {
                Console.WriteLine("Error while loading x waveform");
            }

            status = moduleAOU.AWGqueueWaveform(3, 0, SD_TriggerModes.AUTOTRIG, 0, yindex.Count, Prescaling);
            if (status < 0)
            {
                Console.WriteLine("Error while queuing x waveform");
            }

            // Configure X channel to cyclic mode， Y to single shot
            moduleAOU.AWGqueueConfig(1, 0);
            moduleAOU.AWGqueueConfig(2, 1);
            moduleAOU.AWGqueueConfig(3, 1);

            // Start both channel and wait for triggers
            moduleAOU.AWGstartMultiple(7);


            return HW_STATUS_RETURNS.HW_SUCCESS;

        }

    }
}