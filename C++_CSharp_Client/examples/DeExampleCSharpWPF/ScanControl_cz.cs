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

        public HW_STATUS_RETURNS ScanControlInitialize(double x_amp, double y_amp, double[] x_array, double[] y_array, double delay)
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

            // Convert array into list
            xpoints.Clear();
            ypoints.Clear();
            xpoints = x_array.ToList();
            ypoints = y_array.ToList();

            // Set external trigger as input
            moduleAOU.triggerIOdirection(SD_TriggerDirections.AOU_TRG_IN);
            // Config trigger as external trigger and rising edge
            moduleAOU.AWGtriggerExternalConfig(1, SD_TriggerExternalSources.TRIGGER_EXTERN, SD_TriggerBehaviors.TRIGGER_RISE);
            // flush both channels
            status = moduleAOU.AWGflush(1);
            status = moduleAOU.AWGflush(2);

            int WFinModuleCount;
            for (WFinModuleCount = 0; WFinModuleCount < ypoints.Count; WFinModuleCount++)
            {
                var tmpWaveform = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, new double[] { ypoints[WFinModuleCount], ypoints[WFinModuleCount] });   // WaveForm has to contain even number of points to activate padding option 1
                status = moduleAOU.waveformLoad(tmpWaveform, WFinModuleCount, 1);       // padding option 1 is used to maintain ending voltage after each WaveForm
                if (status < 0)
                {
                    Console.WriteLine("Error while loading " + WFinModuleCount + " point from y array");
                }
                status = moduleAOU.AWGqueueWaveform(1, WFinModuleCount, SD_TriggerModes.EXTTRIG, 0, 1, 0);// AWG, waveform#, trigger, delay, cycle,prescaler
                if (status < 0)
                {
                    Console.WriteLine("Error while queuing " + WFinModuleCount + " point from y array");
                }
            }

            for (WFinModuleCount = 0; WFinModuleCount < xpoints.Count; WFinModuleCount++)
            {
                var tmpWaveform = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, new double[] { xpoints[WFinModuleCount], xpoints[WFinModuleCount] });   // WaveForm has to contain even number of points to activate padding option 1
                status = moduleAOU.waveformLoad(tmpWaveform, WFinModuleCount, 1);       // padding option 1 is used to maintain ending voltage after each WaveForm
                if (status < 0)
                {
                    Console.WriteLine("Error while loading " + WFinModuleCount + " point from x array");
                }
                status = moduleAOU.AWGqueueWaveform(2, WFinModuleCount, SD_TriggerModes.EXTTRIG, 0, 1, 0);// AWG, waveform#, trigger, delay, cycle,prescaler
                if (status < 0)
                {
                    Console.WriteLine("Error while queuing " + WFinModuleCount + " point from x array");
                }
            }

            // Configure queue to only one shot
            moduleAOU.AWGqueueConfig(1, 0);
            moduleAOU.AWGqueueConfig(2, 0);

            // Start both channel and wait for triggers
            moduleAOU.AWGstart(1);
            moduleAOU.AWGstart(2);

            return HW_STATUS_RETURNS.HW_SUCCESS;

        }

    }
}