using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeysightSD1;

// 6/11/2018: Modified this scan control procedure to do conventional scan
// AWG test code copied from Keysight with a few modifications
namespace ScanControl_traditional
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
            int nSlot = 7;

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


            int WFinModuleCount = 0;


            /// load waveform for channel 2 (X)
            for (int ix = 0; ix < xpoints.Count; ix++)
            {
                // with 16 reps when generate wave form, AWG generates the desired scan pattern, not sure why
                // var tmpWaveform_X = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, new double[] { xpoints[ix], xpoints[ix], xpoints[ix], xpoints[ix], xpoints[ix], xpoints[ix], xpoints[ix], xpoints[ix], xpoints[ix], xpoints[ix], xpoints[ix], xpoints[ix], xpoints[ix], xpoints[ix], xpoints[ix], xpoints[ix] });
                var tmpWaveform_X = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, new double[] { xpoints[ix], xpoints[ix]});
                status = moduleAOU.waveformLoad(tmpWaveform_X, WFinModuleCount, 1);       // padding option 1 is used to maintain ending voltage after each WaveForm
                if (status < 0)
                {
                    Console.WriteLine("Error while loading " + ix + " point from x array");
                }
                WFinModuleCount++;
            }
            // queue x channel, for x, WFinModuleCount is the same as ix
            for (int ix = 0; ix < xindex.Count; ix++)
            {
                // loop x array
                status = moduleAOU.AWGqueueWaveform(2, xindex[ix], SD_TriggerModes.EXTTRIG, 0, 1, 4);// AWG, waveform#, trigger, delay, cycle,prescaler
                if (status < 0)
                {
                    Console.WriteLine("Error while queuing " + ix + " point from x array");
                }
            }

            /// load waveform for channel 1 (Y)

            for (int iy = 0; iy < ypoints.Count; iy++)
            {
            //    var tmpWaveform_Y = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, new double[] { ypoints[iy], ypoints[iy], ypoints[iy], ypoints[iy], ypoints[iy], ypoints[iy], ypoints[iy], ypoints[iy], ypoints[iy], ypoints[iy], ypoints[iy], ypoints[iy], ypoints[iy], ypoints[iy], ypoints[iy], ypoints[iy] });
                var tmpWaveform_Y = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, new double[] { ypoints[iy], ypoints[iy] });

                status = moduleAOU.waveformLoad(tmpWaveform_Y, WFinModuleCount, 1);       // padding option 1 is used to maintain ending voltage after each WaveForm
                if (status < 0)
                {
                    Console.WriteLine("Error while loading " + iy + " point from y array, error code " + status);
                }
                WFinModuleCount++;
            }
            // queue waveform for channel 1
            for (int iy = 0; iy < yindex.Count; iy++)
            {
                // use external trigger and cycles for Y channel
                status = moduleAOU.AWGqueueWaveform(1, yindex[iy] + xpoints.Count, SD_TriggerModes.EXTTRIG_CYCLE, 0, xindex.Count, 4);// AWG, waveform#, trigger, delay, cycle,prescaler
                if (status < 0)
                {
                    Console.WriteLine("Error while queuing " + iy + " point from y array, error code " + status);
                }
            }

            // y protection waveform runs only once
            var protWaveform_Y = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, new double[] { -0.99, -0.99 });
            status = moduleAOU.waveformLoad(protWaveform_Y, WFinModuleCount, 1);  // use pos xpoints.Count + ypoints.Count at waveform pool, can be shared by both x and y
            if (status < 0)
            {
                Console.WriteLine("Error while loading protection point from y array, error code " + status);
            }
            status = moduleAOU.AWGqueueWaveform(1, xpoints.Count + ypoints.Count, SD_TriggerModes.EXTTRIG, 0, 2, 4095);// AWG, waveform#, trigger, delay, cycle, prescaler
            if (status < 0)
            {
                Console.WriteLine("Error while queuing protection point from y array, error code " + status);
            }


            // Configure X channel to cyclic mode， Y to single shot
            moduleAOU.AWGqueueConfig(1, 0);
            moduleAOU.AWGqueueConfig(2, 1);


            Console.WriteLine("Scanning in traditional way with " + Xarray_index.Count() + " by " + Yarray_index.Count() + " beam positions.");
            // Start both channel and wait for triggers
            moduleAOU.AWGstart(1);
            moduleAOU.AWGstart(2);  // after AWGstart(2), AWGisRunning(2) = 1, AWGnWFplaying(2) = 0, same for channel 1, there might be 1 px offset
            Console.WriteLine("Now running on x and y " + moduleAOU.AWGnWFplaying(1) +"----" + moduleAOU.AWGnWFplaying(2));
            #region previous scheme to jump on Y channel

            // determine how long to pause after each jump based on frame rate
            /*int pause_ms = 1;
            double frametime = 1000 / (double)recording_rate;
            if (frametime > 1)
            {
                pause_ms = (int)Math.Ceiling(frametime);
            }
            int ncycle = 0;
                       Console.WriteLine("Now on Y channel " + moduleAOU.AWGnWFplaying(1) + " Now on X channel: " + moduleAOU.AWGnWFplaying(2) + "_" + moduleAOU.AWGisRunning(2));
                       while (moduleAOU.AWGnWFplaying(2) == 0)   // x channel may not be at zero when no trigger come, replace with AWGisRunning
                       {
                           // Empty loop wait for trigger to come
                       }

                       // Now cycle start
                       Console.WriteLine("Now on Y channel " + moduleAOU.AWGnWFplaying(1) + " Now on X channel " + moduleAOU.AWGnWFplaying(2));
                       ncycle++;   // ncycle=1, currently working on cycle 1



                       while (ncycle < yindex.Count())
                       {
                           if(moduleAOU.AWGnWFplaying(2)==xindex.Count()-1)
                           {
                               ncycle++;
                               moduleAOU.AWGtrigger(1);
                               Console.WriteLine("Jump to cycle " + ncycle + " now on Y channel: " + moduleAOU.AWGnWFplaying(1) + " now on X channel : " + moduleAOU.AWGnWFplaying(2));
                               System.Threading.Thread.Sleep(pause_ms * 2);
                           }
                       }
                       moduleAOU.AWGtrigger(1);    // trigger y to protection position and stop x scan
                       Console.WriteLine("Now on Y channel " + moduleAOU.AWGnWFplaying(1));
                       moduleAOU.AWGstop(2);
                       System.Threading.Thread.Sleep(5000);    // sleep 5 secs with beam in protection position for user to block beam and stop acquisition
           */
            #endregion
            while (moduleAOU.AWGnWFplaying(1) != xpoints.Count + ypoints.Count)
            {
                
            }
            Console.WriteLine("Acquisition finished, stop camera now");
            moduleAOU.AWGstop(2);
            moduleAOU.AWGstop(1);


            return HW_STATUS_RETURNS.HW_SUCCESS;

        }

    }
}