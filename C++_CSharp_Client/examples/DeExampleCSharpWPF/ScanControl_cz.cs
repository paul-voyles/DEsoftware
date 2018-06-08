using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeysightSD1;

// AWG test code copied from Keysight with a few modifications
namespace ScanControl_cz
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

        public HW_STATUS_RETURNS ScanControlInitialize(double x_amp, double y_amp, double[] Xarray_vol, double[] Yarray_vol, int[] Xarray_index, int[] Yarray_index, double delay)
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

            // Set external trigger as input
            moduleAOU.triggerIOdirection(SD_TriggerDirections.AOU_TRG_IN);
            // Config trigger as external trigger and rising edge
            moduleAOU.AWGtriggerExternalConfig(1, SD_TriggerExternalSources.TRIGGER_EXTERN, SD_TriggerBehaviors.TRIGGER_RISE);
            moduleAOU.AWGtriggerExternalConfig(2, SD_TriggerExternalSources.TRIGGER_EXTERN, SD_TriggerBehaviors.TRIGGER_RISE);
            // flush both channels
            status = moduleAOU.AWGflush(1);
            status = moduleAOU.AWGflush(2);


            int WFinModuleCount;


            // load waveform for channel 2 (X)
            for (WFinModuleCount = 0; WFinModuleCount < xpoints.Count; WFinModuleCount++)
            {
                // with 16 reps when generate wave form, AWG generates the desired scan pattern, not sure why
                var tmpWaveform_X = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, new double[] { xpoints[WFinModuleCount], xpoints[WFinModuleCount], xpoints[WFinModuleCount], xpoints[WFinModuleCount], xpoints[WFinModuleCount], xpoints[WFinModuleCount], xpoints[WFinModuleCount], xpoints[WFinModuleCount], xpoints[WFinModuleCount], xpoints[WFinModuleCount], xpoints[WFinModuleCount], xpoints[WFinModuleCount], xpoints[WFinModuleCount], xpoints[WFinModuleCount], xpoints[WFinModuleCount], xpoints[WFinModuleCount] });   // WaveForm has to contain even number of points to activate padding option 1
                status = moduleAOU.waveformLoad(tmpWaveform_X, WFinModuleCount, 1);       // padding option 1 is used to maintain ending voltage after each WaveForm
                if (status < 0)
                {
                    Console.WriteLine("Error while loading " + WFinModuleCount + " point from x array");
                }
            }
            for (WFinModuleCount = 0; WFinModuleCount < xindex.Count; WFinModuleCount++)
            {
                status = moduleAOU.AWGqueueWaveform(2, xindex[WFinModuleCount], SD_TriggerModes.EXTTRIG, 0, 1, 0);// AWG, waveform#, trigger, delay, cycle,prescaler
                if (status < 0)
                {
                    Console.WriteLine("Error while queuing " + WFinModuleCount + " point from x array");
                }
                /*if (WFinModuleCount > 1023)
                {
                    Console.WriteLine(xindex[WFinModuleCount] + " Status: " + status + "\n");
                }*/
            }

            // load waveform for channel 1 (Y)

            for (WFinModuleCount = 0; WFinModuleCount < ypoints.Count; WFinModuleCount++)
            {
                var tmpWaveform_Y = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, new double[] { ypoints[WFinModuleCount], ypoints[WFinModuleCount], ypoints[WFinModuleCount], ypoints[WFinModuleCount], ypoints[WFinModuleCount], ypoints[WFinModuleCount], ypoints[WFinModuleCount], ypoints[WFinModuleCount], ypoints[WFinModuleCount], ypoints[WFinModuleCount], ypoints[WFinModuleCount], ypoints[WFinModuleCount], ypoints[WFinModuleCount], ypoints[WFinModuleCount], ypoints[WFinModuleCount], ypoints[WFinModuleCount] });   // WaveForm has to contain even number of points to activate padding option 1
                status = moduleAOU.waveformLoad(tmpWaveform_Y, WFinModuleCount, 1);       // padding option 1 is used to maintain ending voltage after each WaveForm
                if (status < 0)
                {
                    Console.WriteLine("Error while loading " + WFinModuleCount + " point from y array, error code " + status);
                }
            }
            // queue waveform for channel 1
            for (WFinModuleCount = 0; WFinModuleCount < yindex.Count; WFinModuleCount++)
            {
                status = moduleAOU.AWGqueueWaveform(1, yindex[WFinModuleCount], SD_TriggerModes.EXTTRIG, 0, 1, 0);// AWG, waveform#, trigger, delay, cycle,prescaler
                if (status < 0)
                {
                    Console.WriteLine("Error while queuing " + WFinModuleCount + " point from y array, error code " + status);
                }
            }


            // Configure queue to only one shot
            moduleAOU.AWGqueueConfig(1, 0);
            moduleAOU.AWGqueueConfig(2, 0);

            // Start both channel and wait for triggers
            moduleAOU.AWGstart(1);
            moduleAOU.AWGstart(2);

            /*while (moduleAOU.AWGisRunning(1)==1)
            {
                Console.WriteLine(moduleAOU.AWGnWFplaying(1));
                Thread.Sleep(10);
            }*/

            return HW_STATUS_RETURNS.HW_SUCCESS;

        }

    }
}