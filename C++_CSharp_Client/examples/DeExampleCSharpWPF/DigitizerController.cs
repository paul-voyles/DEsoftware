#region specify using dirctives
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Ivi.Driver.Interop;
using Keysight.KtM9217.Interop;
#endregion 

// this cannot run at the same time as soft front panel of digitizer
// This will only work on DE compute with Keysight libraries installed
namespace Digitizer
{
    class Program
    {
        [STAThread]

        public static void CancelAcquisition()
        {
            KtM9217 driver = null;
            driver = new KtM9217();

            // Edit resource and options as needed. Resource is ignored if option Simulate=true
            string resourceDesc = "PXI0::8-0.0::INSTR";

            string initOptions = "QueryInstrStatus=true, Simulate=false, DriverSetup= Model=, Trace=false";

            bool idquery = true;
            bool reset = true;

            // Initialize the driver. See driver help topic "Initializing the IVI-COM Driver" for additional information
            driver.Initialize(resourceDesc, idquery, reset, initOptions);
            if (driver != null && driver.Initialized)
            {
                #region Close Driver Instances
                driver.Close();
                Console.WriteLine("Driver Closed");
                #endregion
            }
        }

        public static void FetchData( int record_size, int recording_rate, ref double[] WaveformArray_Ch1)
        {
            Console.WriteLine(" PrintProperties");
            Console.WriteLine();
            KtM9217 driver = null;

            try
            {
                #region Initialize Driver Instances
                driver = new KtM9217();

                // Edit resource and options as needed. Resource is ignored if option Simulate=true
                string resourceDesc = "PXI0::8-0.0::INSTR";

                string initOptions = "QueryInstrStatus=true, Simulate=false, DriverSetup= Model=, Trace=false";

                bool idquery = true;
                bool reset = true;

                // Initialize the driver. See driver help topic "Initializing the IVI-COM Driver" for additional information
                driver.Initialize(resourceDesc, idquery, reset, initOptions);
                Console.WriteLine("Driver Initialized\n");
                #endregion
/*
                #region Print Driver Properties
                Console.WriteLine("Identifier: {0}", driver.Identity.Identifier);
                Console.WriteLine("Revision: {0}", driver.Identity.Revision);
                Console.WriteLine("Vendor: {0}", driver.Identity.Vendor);
                Console.WriteLine("Description: {0}", driver.Identity.Description);
                Console.WriteLine("Model: {0}", driver.Identity.InstrumentModel);
                Console.WriteLine("FirmwareRev: {0}", driver.Identity.InstrumentFirmwareRevision);
                Console.WriteLine("Serial #: {0}", driver.System.SerialNumber);
                Console.WriteLine("\nSimulate: {0}\n", driver.DriverOperation.Simulate);
                #endregion
*/
                #region ActiveSource Settings
                Console.WriteLine("Configuring ActiveSource source to External\n");
                driver.Trigger.ActiveSource = ("External");
                //driver.Trigger.PretriggerSamples = 0;
                //driver.Trigger.Delay = 0;
                //driver.Trigger.Holdoff = 0;
                #endregion

                #region Acquisition Settings
                Console.WriteLine("Configuring Acquisition...");
                Console.WriteLine("Number of records to acquire: 1");
                driver.Acquisition.NumRecordsToAcquire = 1; // maximum 1024 for external, 1 for immediate
                Console.WriteLine("Number of record size: " + record_size + "\n");
                driver.Acquisition.RecordSize = record_size; //minium is 2, maximun is 3.2e7
                Console.WriteLine("Rate of the sample clock: " + recording_rate + "\n");
                driver.Acquisition.SampleRate = recording_rate;
                #endregion

                #region Channels Settings - Channel 1
                Console.WriteLine("Configuring Channel 1...");
                Console.WriteLine("Voltage range: 4V");
                driver.Channels.get_Item("Channel1").Range = 4;
                Console.WriteLine("Coupling: DC\n");
                driver.Channels.get_Item("Channel1").Coupling = KtM9217VerticalCouplingEnum.KtM9217VerticalCouplingDC;
                #endregion

                /* #region Channels Settings - Channel 2
                 Console.WriteLine("Configuring Channel 2...");
                 Console.WriteLine("Voltage range: 32V");
                 driver.Channels.get_Item("Channel2").Range = 32;
                 Console.WriteLine("Coupling: DC\n");
                 driver.Channels.get_Item("Channel2").Coupling = KtM9217VerticalCouplingEnum.KtM9217VerticalCouplingDC;
                 #endregion */

                #region Initiates waveform acquisition
                Console.WriteLine("The hardware leaves the Idle state and waits for trigger to acquire waveform\n");
                driver.Acquisition.Initiate();
                #endregion

                #region Wait for acquisition to complete
                Console.WriteLine("Wait for acquisition to complete - MaxTimeMilliseconds (Infinite)\n");
                driver.Acquisition.WaitForAcquisitionComplete(0);
                #endregion

                #region Fetch waveform data
                long ActualPoints_Ch1 = 0;
                long FirstValidPoint_Ch1 = 0;
                double InitialXOffset_Ch1 = 0;
                double InitialXTimeSeconds_Ch1 = 0.0;
                double InitialXTimeFraction_Ch1 = 0.0;
                double XIncrement_Ch1 = 0.0;

                Console.WriteLine("Fetch Channel 1 acquired waveform...");
                driver.Channels.get_Item("Channel1").Measurement.FetchWaveformReal64(ref WaveformArray_Ch1, ref ActualPoints_Ch1, ref FirstValidPoint_Ch1, ref InitialXOffset_Ch1, ref InitialXTimeSeconds_Ch1, ref InitialXTimeFraction_Ch1, ref XIncrement_Ch1);
                #endregion

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            finally
            {
                if (driver != null && driver.Initialized)
                {
                    #region Close Driver Instances
                    driver.Close();
                    Console.WriteLine("Driver Closed");
                    #endregion
                }
            }

            //Console.WriteLine("Done - Press Enter to Exit");
            //Console.ReadLine();

        }
    }
}
