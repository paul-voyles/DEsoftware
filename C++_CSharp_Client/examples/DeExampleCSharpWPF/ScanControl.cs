using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeysightSD1;

namespace ScanControl
{
    /// <summary>
    /// HW control status.
    /// </summary>
    public enum HW_STATUS_RETURNS
    {
        HW_SUCCESS,
        HW_OTHER
    }

    /// <summary>
    /// This class initialize and equip arbituary waveform generator for scan control
    /// </summary>
    public class ScanControl
    {
        /// <summary>
        /// constructor. Empty now
        /// </summary>
        public ScanControl()
        {
            xpoints = new List<double>();
            ypoints = new List<double>();

            outputdelay = 0;
        }

        /// <summary>
        /// Initialize Arbituary Waveform Generator Hardware
        /// </summary>
        /// <returns>integer -- errorcode </returns>
        public HW_STATUS_RETURNS ScanControlInitialize()
        {
            //SD_AOU module = new SD_AOU();
            module = new SD_AOU();

            string sModuleName = "M3201A";
            int nChassis = 1;
            int nSlot = 3;

            int status;

            if (module.isOpen() == false) //Check if the module is not already opened
            {
                status = module.open(sModuleName, nChassis, nSlot);

                if (status > 0)
                {
                    Console.WriteLine("Module Opened -- " + status.ToString());
                }
                else
                {
                    Console.WriteLine("Module Open error -- " + status.ToString());
                    return HW_STATUS_RETURNS.HW_OTHER;
                }
            }

            //both channel AWG
            module.channelAmplitude(1, 0.15);				// 1.2 Volts Peak
            module.channelAmplitude(2, 0.15);				// 1.2 Volts Peak
            module.channelWaveShape(1, SD_Waveshapes.AOU_AWG);
            module.channelWaveShape(2, SD_Waveshapes.AOU_AWG);

            // Set external trigger as input
            module.triggerIOdirection(SD_TriggerDirections.AOU_TRG_IN);
            // Config trigger as external trigger and rising edge
            module.AWGtriggerExternalConfig(1, SD_TriggerExternalSources.TRIGGER_EXTERN, SD_TriggerBehaviors.TRIGGER_RISE);
            module.AWGtriggerExternalConfig(2, SD_TriggerExternalSources.TRIGGER_EXTERN, SD_TriggerBehaviors.TRIGGER_RISE);

            return HW_STATUS_RETURNS.HW_SUCCESS;
        }

        /// <summary>
        /// set scan parameter (arrays of points)
        /// </summary>
        /// <param name="x">x direction setting, double array</param>
        /// <param name="y">y direction setting, double array</param>
        /// <param name="delay">output delay after trigger (10s ns), integer</param>
        /// <returns>integer -- errorcode</returns>
        public HW_STATUS_RETURNS SetScanParameter(double[] x, double[] y, int delay)
        {
            xpoints.Clear();
            ypoints.Clear();

            // normalzie scan array to [-1 1] and use the maximum value as amplitute
            double xpoints_max = x.Select(z => Math.Abs(z)).Max();
            double ypoints_max = y.Select(z => Math.Abs(z)).Max();

            x_amp = xpoints_max;
            y_amp = ypoints_max;

            x.Select(d => d / xpoints_max);
            y.Select(d => d / ypoints_max);

            xpoints = x.ToList();
            ypoints = y.ToList();

            outputdelay = delay;

            if (prepareScan() == 0)
            {
                return HW_STATUS_RETURNS.HW_SUCCESS;
            }
            else
            {
                return HW_STATUS_RETURNS.HW_OTHER;
            }
        }

        /// <summary>
        /// set scan parameter (boundary and resolution)
        /// </summary>
        /// <param name="xstart">x direction start volatage, double</param>
        /// <param name="xend">x direction stop volatage, double</param>
        /// <param name="ystart">y direction start volatage, double</param>
        /// <param name="yend">y direction stop volatage, double</param>
        /// <param name="xres">x direction number of scan points, integer</param>
        /// <param name="yres">y direction number of scan points, integer</param>
        /// <param name="delay">output delay after trigger (10s ns), integer</param>
        /// <returns>integer -- errorcode</returns>
        public HW_STATUS_RETURNS SetScanParameter(double xstart, double xend, double ystart, double yend, int xres, int yres, int delay)
        {
            double xmin, xmax;
            double ymin, ymax;
            double xstep, ystep;
            int ny = 1;
            int nx = 1;

            if ((xend < xstart) || (yend < ystart))
            {
                return HW_STATUS_RETURNS.HW_OTHER;
            }

            xmin = xstart;
            xmax = xend;
            ymin = ystart;
            ymax = yend;

            xstep = (xmax - xmin) / nx;
            ystep = (ymax - ymin) / ny;
            if ((xres <= 0) || (yres <= 0))
            {
                return HW_STATUS_RETURNS.HW_OTHER;
            }

            nx = xres;
            ny = yres;

            xstep = (xmax - xmin) / nx;
            ystep = (ymax - ymin) / ny;

            xpoints.Clear();
            ypoints.Clear();

            //fill in the lists
            for (int i = 0; i < nx; i++)
            {
                xpoints.Add(xmin + i * xstep);
            }
            for (int i = 0; i < ny; i++)
            {
                ypoints.Add(ymin + i * ystep);
            }

            outputdelay = delay;

            if (prepareScan() == 0)
            {
                return HW_STATUS_RETURNS.HW_SUCCESS;
            }
            else
            {
                return HW_STATUS_RETURNS.HW_OTHER;
            }

        }

        /// <summary>
        /// tell hadware to start outputing waveforms
        /// </summary>
        /// <returns>integer -- errorcode</returns>
        public HW_STATUS_RETURNS StartScan()
        {
            int status;

            // one shot
            module.AWGqueueConfig(1, 0);
            module.AWGqueueConfig(2, 0);

            // Start the AWG to reproduce the waveforms in the queue for ch1
            if (c1WFinQueueCount > 0)
            {
                status = module.AWGstart(1);
                if (status >= 0)
                {
                    return HW_STATUS_RETURNS.HW_SUCCESS;
                }
                else
                {
                    return HW_STATUS_RETURNS.HW_OTHER;
                }
            }
            return HW_STATUS_RETURNS.HW_OTHER;

            // Start the AWG to reproduce the waveforms in the queue for ch2
            if (c1WFinQueueCount > 0)
            {
                status = module.AWGstart(2);
                if (status >= 0)
                {
                    return HW_STATUS_RETURNS.HW_SUCCESS;
                }
                else
                {
                    return HW_STATUS_RETURNS.HW_OTHER;
                }
            }
            return HW_STATUS_RETURNS.HW_OTHER;
        }

        /// <summary>
        /// tell hadware to stop outputing waveforms
        /// </summary>
        /// <returns>integer -- errorcode</returns>
        public HW_STATUS_RETURNS StopScan()
        {
            int status1, status2;

            status1 = module.AWGstop(1);
            status2 = module.AWGstop(2);
            if ((status1 < 0) || (status2 < 0))
                return HW_STATUS_RETURNS.HW_OTHER;

            return HW_STATUS_RETURNS.HW_SUCCESS;
        }

        // Called by SetScanParameter to generate WaveForm and queue to AWG
        private int prepareScan()
        {
            int status;
            SD_Wave tmpWaveform;

            module.waveformFlush();

            // Remove all waveform from the channel 1 queue
            status = module.AWGflush(1);
            if (status >= 0)
            {
                c1WFinQueueCount = 0;
            }
            else
            {
                return 1;
            }

            // Remove all waveform from the channel 2 queue
            status = module.AWGflush(2);
            if (status >= 0)
            {
                c2WFinQueueCount = 0;
            }
            else
            {
                return 1;
            }
            // set trigger direction to input
            module.triggerIOdirection(SD_TriggerDirections.AOU_TRG_IN);
            // set trigger configuration to external, rising edge
            module.AWGtriggerExternalConfig(1, SD_TriggerExternalSources.TRIGGER_EXTERN, SD_TriggerBehaviors.TRIGGER_RISE);

            // set wave shape and amplitude
            module.channelAmplitude(1, y_amp);				// 1.2 Volts Peak
            module.channelWaveShape(1, SD_Waveshapes.AOU_AWG);
            module.channelAmplitude(2, x_amp);				// 1.2 Volts Peak
            module.channelWaveShape(2, SD_Waveshapes.AOU_AWG);

            // generate waveforms.
            // Notes: waveform number is from 0 to y_length-1 for y direction, then frm y_length to y_length + x_length -1;
            // Note2: SD_wave must have even amount of points to enable padding option 1
            WFinModuleCount = 0;
            for (int j = 0; j < ypoints.Count; j++)
            {
                tmpWaveform = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, new double[] { ypoints[j], ypoints[j] });

                status = module.waveformLoad(tmpWaveform, WFinModuleCount, 1);
                if (status >= 0)
                {
                    WFinModuleCount++;
                }
                else
                {
                    return 1;
                }
            }

            for (int i = 0; i < xpoints.Count; i++)
            {

                tmpWaveform = new SD_Wave(SD_WaveformTypes.WAVE_ANALOG, new double[] { xpoints[i], xpoints[i] });

                status = module.waveformLoad(tmpWaveform, WFinModuleCount, 1);
                if (status >= 0)
                {
                    WFinModuleCount++;
                }
                else
                {
                    return 1;
                }

            }

            // WFinModuleCount is one more than total number of waveforms
            if (WFinModuleCount != ypoints.Count + xpoints.Count)
            {
                return 1;
            }

            // Queue the waveform into the channel queue
            for (int j = 0; j < ypoints.Count; j++)
                for (int i = 0; i < xpoints.Count; i++)
                {
                    // Queue the waveform into the channel1 queue
                    status = module.AWGqueueWaveform(1, ypoints.Count + i, SD_TriggerModes.EXTTRIG, outputdelay, 1, 0); // cnannel1, waveform num, trigger (auto=0, ext=5), start delay, cycle, prescaleer
                    if (status >= 0)
                    {
                        c1WFinQueueCount++;
                    }
                    else
                    {
                        return 1;
                    }
                    // Queue the waveform into the channel2 queue
                    status = module.AWGqueueWaveform(2, j, SD_TriggerModes.EXTTRIG, outputdelay, 1, 0); // cnannel, waveform num, trigger (auto=0, ext=5), start delay, cycle, prescaleer
                    if (status >= 0)
                    {
                        c2WFinQueueCount++;
                    }
                    else
                    {
                        return 1;
                    }

                }

            // check quequed waveform number
            if ((c1WFinQueueCount != c2WFinQueueCount) || (c1WFinQueueCount != ypoints.Count + xpoints.Count) || (c2WFinQueueCount != ypoints.Count + xpoints.Count))
            {
                return 1;
            }

            return 0;
        }

        // scan paameter
        private List<double> xpoints;
        private List<double> ypoints;
        private int outputdelay;

        private SD_AOU module;

        int WFinModuleCount = 0;
        int c1WFinQueueCount = 0;
        int c2WFinQueueCount = 0;

        double x_amp;
        double y_amp;
    }
}
