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
        public HW_STATUS_RETURNS ScanControlInitialize()
        {
            int status;

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

            // Config amplitude and setup AWG in channels 0 and 1
            moduleAOU.channelAmplitude(1, 0.15);
            moduleAOU.channelWaveShape(1, SD_Waveshapes.AOU_AWG);
            moduleAOU.channelAmplitude(2, 0.15);
            moduleAOU.channelWaveShape(2, SD_Waveshapes.AOU_AWG);

            // Set external trigger as input
            moduleAOU.triggerIOdirection(SD_TriggerDirections.AOU_TRG_IN);
            // Config trigger as external trigger and rising edge
            moduleAOU.AWGtriggerExternalConfig(1, SD_TriggerExternalSources.TRIGGER_EXTERN, SD_TriggerBehaviors.TRIGGER_RISE);
            // flush both channels
            status = moduleAOU.AWGflush(1);
            status = moduleAOU.AWGflush(2);

            int WFinModuleCount;
            for (WFinModuleCount = 0; WFinModuleCount < 100; WFinModuleCount++)
            {
                var tmpWaveform = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, new double[] { WFinModuleCount * 0.01, WFinModuleCount * 0.01 });   // WaveForm has to contain even number of points to activate padding option 1
                status = moduleAOU.waveformLoad(tmpWaveform, WFinModuleCount, 1);       // padding option 1 is used to maintain ending voltage after each WaveForm
                if (status < 0)
                {
                    Console.WriteLine("Error LOAD");
                }
                status = moduleAOU.AWGqueueWaveform(1, WFinModuleCount, SD_TriggerModes.EXTTRIG, 0, 1, 4095);// AWG, waveform#, trigger, delay, cycle,prescaler
                if (status < 0)
                {
                    Console.WriteLine("Error queue");
                }
            }
            moduleAOU.AWGqueueConfig(1, 1);
            moduleAOU.AWGstart(1);

            return HW_STATUS_RETURNS.HW_SUCCESS;

        }

    }
}