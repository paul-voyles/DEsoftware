using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeysightSD1;
using DeExampleCSharpWPF;

// 6/8/2018: test suggesting that there might be a limit on waveforms queued into AWG, try to get around this problem

// AWG test code copied from Keysight with a few modifications
namespace ScanControl_passive
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
            string sent;
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
            //moduleAOU.AWGtriggerExternalConfig(1, SD_TriggerExternalSources.TRIGGER_PXI, SD_TriggerBehaviors.TRIGGER_RISE);
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
                // use software trigger for Y channel
                status = moduleAOU.AWGqueueWaveform(1, yindex[WFinModuleCount], SD_TriggerModes.SWHVITRIG, 0, 1, 0);// AWG, waveform#, trigger, delay, cycle,prescaler
                if (status < 0)
                {
                    Console.WriteLine("Error while queuing " + WFinModuleCount + " point from y array, error code " + status);
                }
            }


            // Configure X channel to cyclic mode
            moduleAOU.AWGqueueConfig(1, 0);
            moduleAOU.AWGqueueConfig(2, 1);

            // Start both channel, x channel wait for external trigger, send software trigger to y channel
            moduleAOU.AWGstart(1);
            moduleAOU.AWGstart(2);
            moduleAOU.AWGtrigger(1);    // trigger Y channel to protection value

            // determine how long to pause after each jump based on frame rate
            int pause_ms = 1;
            double frametime = 1000 / (double)recording_rate;
            if (frametime > 1)
            {
                pause_ms = (int)Math.Ceiling(frametime);
            }

            int ncycle = 0;
            Console.WriteLine("Now on Y channel " + moduleAOU.AWGnWFplaying(1));
            while (moduleAOU.AWGnWFplaying(2)==0)   // x channel may not be at zero when no trigger come, replace with AWGisRunning
            {
                // Empty loop wait for trigger to come
            }

            // Now cycle start
            moduleAOU.AWGtrigger(1);    // trigger Y channel to first value
            Console.WriteLine("Now on Y channel " + moduleAOU.AWGnWFplaying(1));
            ncycle++;   // ncycle=1, currently working on cycle 1

            while ((ncycle < yindex.Count - 2) && Convert.ToBoolean(moduleAOU.AWGisRunning(2)))
            {
                while(moduleAOU.AWGnWFplaying(2) != ypoints.Count - 2)
                {
                   // empty loop wait for x channel to play last waveform
                }
                ncycle++;
                moduleAOU.AWGtrigger(1);
                Console.WriteLine("Jump to cycle " + ncycle + " now on Y channel: " + moduleAOU.AWGnWFplaying(1) + " now on X channel : " + moduleAOU.AWGnWFplaying(2));
                System.Threading.Thread.Sleep(pause_ms * 2);
                while ( moduleAOU.AWGnWFplaying(2) != 0 )
                {
                    // empty loop wait for x channel to play first waveform
                }
                ncycle++;
                moduleAOU.AWGtrigger(1);
                Console.WriteLine("Jump to cycle " + ncycle + " now on Y channel: " + moduleAOU.AWGnWFplaying(1) + " now on X channel : " + moduleAOU.AWGnWFplaying(2));
                System.Threading.Thread.Sleep(pause_ms * 2);
            }


            // after all cycles finished, sleep for 5 sec before stop AWG
            System.Threading.Thread.Sleep(5000);
            moduleAOU.AWGstop(1);
            moduleAOU.AWGstop(2);

            Console.Write("Both channel closed");

            return HW_STATUS_RETURNS.HW_SUCCESS;

        }

    }
}