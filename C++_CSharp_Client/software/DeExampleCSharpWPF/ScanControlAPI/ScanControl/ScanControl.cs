using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        }

        /// <summary>
        /// Initialize Arbituary Waveform Generator Hardware
        /// </summary>
        /// <returns>integer -- errorcode </returns>
        public HW_STATUS_RETURNS ScanControlInitialize()
        {
            return HW_STATUS_RETURNS.HW_SUCCESS;
        }

        /// <summary>
        /// set the maximum output volatage for scan boundary
        /// </summary>
        /// <param name="xstart">x direction start volatage</param>
        /// <param name="xend">x direction stop volatage</param>
        /// <param name="ystart">y direction start volatage</param>
        /// <param name="yend">y direction stop volatage</param>
        /// <returns>integer -- errorcode</returns>
        public HW_STATUS_RETURNS SetScanVoltageRange(float xstart, float xend, float ystart, float yend)
        {
            return HW_STATUS_RETURNS.HW_SUCCESS;
        }

        /// <summary>
        /// set scan resolution
        /// </summary>
        /// <param name="xres">x direction number of scan points</param>
        /// <param name="yres">y direction number of scan points</param>
        /// <returns>integer -- errorcode</returns>
        public HW_STATUS_RETURNS SetScanResolution(int xres, int yres)
        {
            return HW_STATUS_RETURNS.HW_SUCCESS;
        }

        /// <summary>
        /// tell hadware to start outputing waveforms
        /// </summary>
        /// <returns>integer -- errorcode</returns>
        public HW_STATUS_RETURNS StartScan()
        {
            return HW_STATUS_RETURNS.HW_SUCCESS;
        }

        /// <summary>
        /// tell hadware to stop outputing waveforms
        /// </summary>
        /// <returns>integer -- errorcode</returns>
        public HW_STATUS_RETURNS StopScan()
        {
            return HW_STATUS_RETURNS.HW_SUCCESS;
        }

    }
}
