using System;
using System.Collections.Generic;
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using DeInterface;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Runtime.InteropServices;
using System.Net;
using HDF5DotNet;
using FileLoader;
using System.Timers;
using System.Drawing;
using System.Windows.Forms;

// HDF5 part copied from HDF5DotNet-src/examples/CSharpExample/CSharpExample1, cz 7/14/17

namespace FileLoader
{
    class SEQ
    {
        public static void LoadSEQheader(string path, ref UInt32 sizex, ref UInt32 sizey, ref UInt16 numframes)
        {
            using (var filestream = File.Open(@path, FileMode.Open))

            using (var binaryStream = new BinaryReader(filestream))
            {

                binaryStream.BaseStream.Seek(548, SeekOrigin.Begin);   // 548 bytes loaded
                sizex = binaryStream.ReadUInt32();
                sizey = binaryStream.ReadUInt32();
                binaryStream.BaseStream.Seek(552, SeekOrigin.Begin);
                UInt32 FrameSize = binaryStream.ReadUInt32();
                binaryStream.BaseStream.Seek(572, SeekOrigin.Begin);
                numframes = binaryStream.ReadUInt16();    // 574 bytes read
                binaryStream.BaseStream.Seek(584, SeekOrigin.Begin);
                double frameRate = binaryStream.ReadDouble();   // 592 bytes
            }

        }

        public static void LoadFirstFrame(string path, ref UInt16[] FirstFrame)
        {
            using (var filestream = File.Open(@path, FileMode.Open))

            using (var binaryStream = new BinaryReader(filestream))
            {
                UInt32 framesize = 2105344; // number of bytes occupied by one frame
                UInt32 pos = 8192 + framesize * 0;
                int target = FirstFrame.GetUpperBound(0);
                int count = 0;
                binaryStream.BaseStream.Seek(pos, SeekOrigin.Begin);
                while (count < target)
                {
                    FirstFrame[count] = binaryStream.ReadUInt16();
                    count++;
                }
            }

        }
    }
    class HDF5
    {
        // This function is unsafe because it gets a void pointer as a
        // parameter.
        public static int myErrorFunction(int estackId, IntPtr myPtr)
        {
            Console.WriteLine("Hey! This is my error delegate!");
            Console.WriteLine("The value of myPtr is {0}",
               (int)myPtr);
            return 1;
        }

        static IntPtr userAlloc(IntPtr size, IntPtr alloc_info)
        {
            Console.WriteLine("Using userAlloc to alloc {0} bytes", size.ToInt32());
            return H5CrtHeap.Allocate(size);
        }

        //unsafe static void userFree(IntPtr memory, void* free_info)
        static void userFree(IntPtr memory, IntPtr free_info)
        {
            Console.WriteLine("Using userFree to free memory");
            H5CrtHeap.Free(memory);
            return;
        }

        // Function used with H5L.iterate
        static H5IterationResult MyH5LFunction(H5GroupId id,
                                               string objectName,
                                               H5LinkInfo info, Object param)
        {
            Console.WriteLine("The object name is {0}", objectName);
            Console.WriteLine("The linkType is {0}", info.linkType);
            Console.WriteLine("The object parameter is {0}", param);
            return H5IterationResult.SUCCESS;
        }

        // Function used with H5A.iterate
        static H5IterationResult MyH5AFunction(
           H5AttributeId attributeId,
           String attributeName,
           H5AttributeInfo info,
           Object attributeNames)
        {
            Console.WriteLine("Iteration attribute is {0}", attributeName);
            ArrayList nameList = (ArrayList)attributeNames;
            nameList.Add(attributeName);

            // Returning SUCCESS means that iteration should continue to the 
            // next attribute (if one exists).
            return H5IterationResult.SUCCESS;
        }

        // Generate string attribute
        // GroupName: target group for the new attribute
        // AttName: attribute name
        // AttContent: content for the attribute, has to be a string
        public static void StringAttributeGenerator(H5GroupId GroupName, string AttName, string AttContent)
        {
            char[] AttContentChar = AttContent.ToCharArray();
            byte[] asciiStr = ASCIIEncoding.ASCII.GetBytes(AttContentChar);
            int length = asciiStr.Length;
            H5AttributeId attributeId = H5A.create(GroupName, AttName, H5T.create(H5T.CreateClass.STRING, length), H5S.create(H5S.H5SClass.SCALAR));
            H5A.write(attributeId, H5T.create(H5T.CreateClass.STRING, length), new H5Array<byte>(asciiStr));
            H5A.close(attributeId);
        }

        // Generate floating number attributes
        // GroupName: target group for the new attribute
        // AttName: attribute name
        // AttContent: content for the attribute, has to be a single floating number, here 32bit floating number is used, consider using 64bit double if necessary
        public static void NumberAttributeGenerator(H5GroupId GroupName, string AttName, float AttContent)
        {
            float[] AttArray = new float[1] { AttContent };
            long[] dims = new long[1];
            dims[0] = AttArray.Length;
            H5AttributeId attributeId = H5A.create(GroupName, AttName, H5T.copy(H5T.H5Type.NATIVE_FLOAT), H5S.create_simple(1, dims));
            H5A.write(attributeId, H5T.copy(H5T.H5Type.NATIVE_FLOAT), new H5Array<float>(AttArray));
            H5A.close(attributeId);
        }

        // Generate double attributes
        // GroupName: target group for the new attribute
        // AttName: attribute name
        // AttContent: content for the attribute, has to be a single floating number, here 64bit double is used, to generate subgroups in Data
        public static void DoubleAttributeGenerator(H5GroupId GroupName, string AttName, double AttContent)
        {
            double[] AttArray = new double[1] { AttContent };
            long[] dims = new long[1];
            dims[0] = AttArray.Length;
            H5AttributeId attributeId = H5A.create(GroupName, AttName, H5T.copy(H5T.H5Type.NATIVE_DOUBLE), H5S.create_simple(1, dims));
            H5A.write(attributeId, H5T.copy(H5T.H5Type.NATIVE_FLOAT), new H5Array<double>(AttArray));
            H5A.close(attributeId);
        }

        public static void WriteDataCube(H5FileId fileId, UInt16[,,] datacube)
        {
            H5GroupId dataGroup = H5G.create(fileId, "/data");
            H5GroupId dataSubGroup = H5G.create(dataGroup, "DEsoftware");
            long[] dims = new long[3] { datacube.GetLength(0), datacube.GetLength(1), datacube.GetLength(2)};
            
            H5DataSpaceId spaceId = H5S.create_simple(3, dims);
            H5DataTypeId typeId = H5T.copy(H5T.H5Type.NATIVE_USHORT);
            H5DataSetId dataSetId = H5D.create(dataSubGroup, "data",
                                                   typeId, spaceId);

            // create attribute emd_group_type for dataSubGroup, which is required to have value 1
            int par = 1;
            long[] attdims = new long[1];
            int[] AttArray = new int[1] { par };
            dims[0] = AttArray.Length;
            H5AttributeId attributeId = H5A.create(dataSubGroup, "emd_group_type", H5T.copy(H5T.H5Type.NATIVE_UCHAR), H5S.create_simple(1, dims));
            H5A.write(attributeId, H5T.copy(H5T.H5Type.NATIVE_INT), new H5Array<int>(AttArray));
            H5A.close(attributeId);

            // write datacube to "data", which contains whole 3D datacube
            H5D.write<ushort>(dataSetId, typeId, new H5Array<ushort>(datacube));

            // create three more array for three dimensions
            long[] dim1 = new long[1] { datacube.GetLength(0) };
            double[] dimarray = new double [datacube.GetLength(0)];
            spaceId = H5S.create_simple(3, dim1 );
            typeId = H5T.copy(H5T.H5Type.NATIVE_DOUBLE);
            dataSetId = H5D.create(dataSubGroup, "dim1", typeId, spaceId);
            H5D.write<double>(dataSetId, typeId, new H5Array<double>(dimarray));

            H5S.close(spaceId);
            H5T.close(typeId);
            H5D.close(dataSetId);
            H5G.close(dataGroup);
            H5G.close(dataSubGroup);
        }

        public static H5FileId InitializeHDF(int numpos, int height, int width, UInt16[,,] datacube, string fullpath)
        {
            // write in HDF5 (.h5) format

            // generate standard groups
            H5FileId fileId = H5F.create(fullpath,
                            H5F.CreateMode.ACC_TRUNC);
            //H5FileId fileId = H5F.create("D:/2017/Pixelated Camera/CameraSoftware/FileFormat/Test/test5.emd",
                             //H5F.CreateMode.ACC_TRUNC);
            //NumberAttributeGenerator(fileId, "voltage", Convert.ToSingle(300));

            //H5GroupId dataGroup = H5G.create(fileId, "/data");  //dash is required for root group
            H5GroupId userGroup = H5G.create(fileId, "/user");
            H5GroupId micGroup = H5G.create(fileId, "/microscope");
            H5GroupId sampleGroup = H5G.create(fileId, "/sample");
            H5GroupId commentGroup = H5G.create(fileId, "/comments");

            // generate attributes for user group, all attributes are sting
            StringAttributeGenerator(userGroup, "user", "Chenyu Zhang");
            StringAttributeGenerator(userGroup, "email", "chenyu.zhang@wisc.edu");
            StringAttributeGenerator(userGroup, "institution", "UW-Madison");
            StringAttributeGenerator(userGroup, "department", "Materials Science and Engineering");

            // generate attributes for microscope group
            StringAttributeGenerator(micGroup, "voltage units", "kV");
            NumberAttributeGenerator(micGroup, "voltage", Convert.ToSingle(300));
            StringAttributeGenerator(micGroup, "wavelength units", "nm");
            NumberAttributeGenerator(micGroup, "wavelength", Convert.ToSingle(0.00197));

            // generate attributes for sample group
            StringAttributeGenerator(sampleGroup, "material", "STO");
            StringAttributeGenerator(sampleGroup, "preparation", "Mechanical polishing and ion milling");
            StringAttributeGenerator(sampleGroup, "Zone Axis", "[1][0][0]");

            // Write 3D data cube to the file
            WriteDataCube(fileId, datacube);

            // close groups and file
            H5G.close(userGroup);
            H5G.close(micGroup);
            H5G.close(sampleGroup);
            H5G.close(commentGroup);
            //H5G.close(dataGroup);
            H5F.close(fileId);

            return fileId;
        }

        // changed name from main to CreateHDF and argument to none cz 7/14/17
        // need to accept a filename, file dimension (maybe not if always 2D, but need to consider what is the fastest way to save), and file size
        public static void CreateHDF()
        {
            try
            {
                // We will write and read an int array of this length.
                const int DATA_ARRAY_LENGTH = 12;

                // Rank is the number of dimensions of the data array, rank of 4D STEM data is always 3.
                const int RANK = 3 ;

                // Create an HDF5 file.
                // The enumeration type H5F.CreateMode provides only the legal 
                // creation modes.  Missing H5Fcreate parameters are provided
                // with default values.
                H5FileId fileId = H5F.create("D:/2017/Pixelated Camera/CameraSoftware/FileFormat/Test/default.emd",
                                             H5F.CreateMode.ACC_TRUNC);

                // Create a HDF5 group.  
                H5GroupId groupId = H5G.create(fileId, "/cSharpGroup");
                H5GroupId subGroup = H5G.create(groupId, "mySubGroup");

                // Close the subgroup.
                H5G.close(subGroup);



                // Prepare to create a data space for writing a 1-dimensional 
                // signed integer array.
                long[] dims = new long[RANK];
                dims[0] = DATA_ARRAY_LENGTH;

                // Put descending ramp data in an array so that we can
                // write it to the file.
                int[] dset_data = new int[DATA_ARRAY_LENGTH];
                for (int i = 0; i < DATA_ARRAY_LENGTH; i++)
                    dset_data[i] = DATA_ARRAY_LENGTH - i;

                // Create a data space to accommodate our 1-dimensional array.
                // The resulting H5DataSpaceId will be used to create the 
                // data set.
                H5DataSpaceId spaceId = H5S.create_simple(RANK, dims);

                // Create a copy of a standard data type.  We will use the 
                // resulting H5DataTypeId to create the data set.  We could
                // have  used the HST.H5Type data directly in the call to 
                // H5D.create, but this demonstrates the use of H5T.copy 
                // and the use of a H5DataTypeId in H5D.create.
                H5DataTypeId typeId = H5T.copy(H5T.H5Type.NATIVE_INT);

                // Find the size of the type
                int typeSize = H5T.getSize(typeId);
                Console.WriteLine("typeSize is {0}", typeSize);

                // Set the order to big endian
                H5T.setOrder(typeId, H5T.Order.BE);

                // Set the order to little endian
                H5T.setOrder(typeId, H5T.Order.LE);

                // Create the data set.
                H5DataSetId dataSetId = H5D.create(fileId, "/csharpExample",
                                                   typeId, spaceId);

                // Write the integer data to the data set.
                H5D.write(dataSetId, new H5DataTypeId(H5T.H5Type.NATIVE_INT),
                                  new H5Array<int>(dset_data));

                // If we were writing a single value it might look like this.
                //  int singleValue = 100;
                //  H5D.writeScalar(dataSetId, 
                //                 new H5DataTypeId(H5T.H5Type.NATIVE_INT),
                //                 ref singleValue);

                // Create an integer array to receive the read data.
                int[] readDataBack = new int[DATA_ARRAY_LENGTH];

                // Read the integer data back from the data set
                H5D.read(dataSetId, new H5DataTypeId(H5T.H5Type.NATIVE_INT),
                    new H5Array<int>(readDataBack));

                // Echo the data
                for (int i = 0; i < DATA_ARRAY_LENGTH; i++)
                {
                    Console.WriteLine(readDataBack[i]);
                }

                // Close all the open resources.
                H5D.close(dataSetId);

                // Reopen and close the data sets to show that we can.
                dataSetId = H5D.open(fileId, "/csharpExample");
                H5D.close(dataSetId);
                dataSetId = H5D.open(groupId, "/csharpExample");

                H5D.close(dataSetId);
                H5T.close(typeId);
                H5G.close(groupId);

                // Get H5O info
                H5ObjectInfo objectInfo = H5O.getInfoByName(fileId,
                   "/csharpExample");

                Console.WriteLine("header.space.message is {0}",
                                  objectInfo.header.space.message);
                Console.WriteLine("fileNumber is {0}", objectInfo.fileNumber);
                Console.WriteLine("address is {0}", objectInfo.address);
                Console.WriteLine("type is {0}",
                                  objectInfo.objectType.ToString());
                Console.WriteLine("reference count is {0}",
                                  objectInfo.referenceCount);
                Console.WriteLine("modification time is {0}",
                                  objectInfo.modificationTime);
                Console.WriteLine("birth time is {0}",
                                  objectInfo.birthTime);
                Console.WriteLine("access time is {0}",
                                  objectInfo.accessTime);
                Console.WriteLine("change time is {0}",
                                  objectInfo.changeTime);
                Console.WriteLine("number of attributes is {0}",
                                  objectInfo.nAttributes);

                Console.WriteLine("header version is {0}",
                                  objectInfo.header.version);

                Console.WriteLine("header nMessages is {0}",
                                  objectInfo.header.nMessages);

                Console.WriteLine("header nChunks is {0}",
                                  objectInfo.header.nChunks);

                Console.WriteLine("header flags is {0}",
                                  objectInfo.header.flags);

                H5LinkInfo linkInfo = H5L.getInfo(fileId, "/cSharpGroup");

                Console.WriteLine(
                   "address: {0:x}, charSet: {1}, creationOrder: {2}",
                   linkInfo.address, linkInfo.charSet, linkInfo.creationOrder);

                Console.WriteLine("linkType: {0}, softLinkSizeOrUD: {1}",
                                  linkInfo.linkType, linkInfo.softLinkSizeOrUD);

                // Reopen the group id to show that we can.
                groupId = H5G.open(fileId, "/cSharpGroup");


                // Use H5L.iterate to visit links
                H5LIterateCallback myDelegate;
                myDelegate = MyH5LFunction;
                ulong linkNumber = 0;
                H5IterationResult result =
                   H5L.iterate(groupId, H5IndexType.NAME,
                              H5IterationOrder.INCREASING,
                              ref linkNumber, myDelegate, 0);

                // Create some attributes
                H5DataTypeId attributeType = H5T.copy(H5T.H5Type.NATIVE_INT);
                long[] attributeDims = new long[1];
                const int RAMP_LENGTH = 5;
                attributeDims[0] = RAMP_LENGTH;
                int[] ascendingRamp = new int[RAMP_LENGTH] { 1, 2, 3, 4, 5 };
                int[] descendingRamp = new int[RAMP_LENGTH] { 5, 4, 3, 2, 1 };
                int[] randomData = new int[RAMP_LENGTH] { 3, 123, 27, 6, 1 };
                int[] readBackRamp = new int[RAMP_LENGTH];



                // Call set buffer using H5Memory
                // Allocate memory from "C" runtime heap (not garbage collected)
                H5Memory typeConversionBuffer = new H5Memory(new IntPtr(DATA_ARRAY_LENGTH));
                H5Memory backgroundBuffer = new H5Memory(new IntPtr(DATA_ARRAY_LENGTH));

                // Set the property list type conversion and background buffers.
                H5PropertyListId myPropertyListId =
                      H5P.create(H5P.PropertyListClass.DATASET_XFER);
                H5P.setBuffer(myPropertyListId,
                                 typeConversionBuffer, backgroundBuffer);

                // Test use of vlen

                // Create a vlen data type
                H5DataTypeId tid1 = H5T.vlenCreate(H5T.H5Type.NATIVE_UINT);

                H5DataSetId vDataSetId = H5D.create(fileId, "/vlenTest", tid1,
                  spaceId);

                // Create a jagged array of integers.
                hvl_t[] vlArray = new hvl_t[DATA_ARRAY_LENGTH];

                // HDF5 variable length data types require the use of void 
                // pointers.  C# requires that sections of code that deal 
                // directly with pointer be marked
                // as unsafe.
                unsafe
                {
                    for (int i = 0; i < DATA_ARRAY_LENGTH; i++)
                    {
                        IntPtr ptr = new IntPtr((i + 1) * sizeof(int));
                        // Allocate memory that is not garbage collected.
                        vlArray[i].p = H5CrtHeap.Allocate(
                            new IntPtr((i + 1) * sizeof(int))
                            ).ToPointer();

                        // Fill the array with integers = the row number
                        int* intPointer = (int*)vlArray[i].p;
                        for (int j = 0; j < i + 1; j++)
                        {
                            intPointer[j] = (int)i;
                        }

                        if (IntPtr.Size == 8)
                        {
                            vlArray[i].len = (ulong)i + 1;
                        }
                        else
                        {
                            vlArray[i].len = (uint)i + 1;
                        }
                    }

                    // Write the variable length data
                    H5D.write(vDataSetId, tid1,
                       new H5Array<hvl_t>(vlArray));

                    // Create an array to read back the array.
                    hvl_t[] vlReadBackArray = new hvl_t[DATA_ARRAY_LENGTH];

                    // Read the array back
                    H5D.read(vDataSetId, tid1, new H5Array<hvl_t>(vlReadBackArray));

                    // Write the data to the console
                    /*for (int i = 0; i < DATA_ARRAY_LENGTH; i++)
                    {
                        int* iPointer = (int*)vlReadBackArray[i].p;
                        for (int j = 0; j < i + 1; j++)
                        {
                            Console.WriteLine(iPointer[j]);
                        }
                    }*/

                    // Reclaim the memory that read allocated
                    H5D.vlenReclaim(tid1, spaceId, new H5PropertyListId(H5P.Template.DEFAULT),
                       new H5Array<hvl_t>(vlReadBackArray));

                    // Now read it back again using our own memory manager


                    //H5AllocateCallback allocDelegate = new H5AllocCallback(userAlloc);
                    H5FreeCallback freeDelegate = new H5FreeCallback(userFree);

                    H5PropertyListId memManagerPlist = H5P.create(H5P.PropertyListClass.DATASET_XFER);

                    unsafe
                    {
                        H5P.setVlenMemManager(memManagerPlist, userAlloc, IntPtr.Zero,
                           freeDelegate, IntPtr.Zero);
                    }

                    // Read the array back
                    H5D.read(vDataSetId, tid1,
                             new H5DataSpaceId(H5S.H5SType.ALL),
                             new H5DataSpaceId(H5S.H5SType.ALL),
                             memManagerPlist,
                             new H5Array<hvl_t>(vlReadBackArray));

                    // Write the data to the console
                    /*for (int i = 0; i < DATA_ARRAY_LENGTH; i++)
                    {
                        int* iPointer = (int*)vlReadBackArray[i].p;
                        for (int j = 0; j < i + 1; j++)
                        {
                            Console.WriteLine(iPointer[j]);
                        }
                    }*/

                    // Reclaim the memory that read allocated using our free routines
                    H5D.vlenReclaim(tid1, spaceId, memManagerPlist,
                       new H5Array<hvl_t>(vlReadBackArray));
                }
                H5S.close(spaceId);


                H5DataSpaceId attributeSpace =
                   H5S.create_simple(1, attributeDims);

                H5AttributeId attributeId =
                    H5A.create(groupId, "ascendingRamp", attributeType, attributeSpace);    //H5A used to create attribute

                int offset = H5T.getOffset(attributeType);
                Console.WriteLine("Offset is {0}", offset);

                H5DataTypeId float32BE = H5T.copy(H5T.H5Type.IEEE_F32BE);
                H5T.Norm norm = H5T.getNorm(float32BE);
                Console.WriteLine("Norm is {0}", norm);


                int precision = H5T.getPrecision(float32BE);
                Console.WriteLine("Precision is {0}", precision);

                H5FloatingBitFields bitFields = H5T.getFields(float32BE);
                Console.WriteLine("getFields: sign bit position: {0}", bitFields.signBitPosition);
                Console.WriteLine("getFields: exponent bit position: {0}", bitFields.exponentBitPosition);
                Console.WriteLine("getFields: number of exponent bits: {0}", bitFields.nExponentBits);
                Console.WriteLine("getFields: mantissa bit position: {0} ", bitFields.mantissaBitPosition);
                Console.WriteLine("getFields: number of mantissa bits: {0}", bitFields.nMantissaBits);

                Console.Write("{0}", bitFields);
                // Write to an attribute
                H5A.write<int>(attributeId, attributeType,
                               new H5Array<int>(ascendingRamp));

                // Read from an attribute
                H5A.read<int>(attributeId, attributeType,
                              new H5Array<int>(readBackRamp));

                // Echo results
                Console.WriteLine("ramp elements are: ");
                foreach (int rampElement in readBackRamp)
                {
                    Console.WriteLine("   {0}", rampElement);
                }
                H5A.close(attributeId);

                // Create and write two more attributes.
                attributeId = H5A.createByName(groupId, ".", "descendingRamp",
                                         attributeType, attributeSpace);
                H5A.write<int>(attributeId, attributeType,
                               new H5Array<int>(descendingRamp));
                H5A.close(attributeId);

                attributeId = H5A.createByName(groupId, ".",
                                         "randomData", attributeType,
                                         attributeSpace);
                H5A.write<int>(attributeId, attributeType,
                               new H5Array<int>(randomData));

                // Read back the attribute data
                H5A.read<int>(attributeId, attributeType,
                              new H5Array<int>(readBackRamp));
                Console.WriteLine("ramp elements are: ");
                foreach (int rampElement in readBackRamp)
                {
                    Console.WriteLine("   {0}", rampElement);
                }
                H5A.close(attributeId);

                // Iterate through the attributes.
                long position = 0;
                H5AIterateCallback attributeDelegate;
                attributeDelegate = MyH5AFunction;
                H5ObjectInfo groupInfo = H5O.getInfo(groupId);
                Console.WriteLine(
                    "fileNumber: {0}, total space: {1}, referceCount: {2}, modification time: {3}",
                    groupInfo.fileNumber, groupInfo.header.space.total,
                    groupInfo.referenceCount, groupInfo.modificationTime);

                // While iterating, collect the names of all the attributes.
                ArrayList attributeNames = new ArrayList();
                H5A.iterate(groupId, H5IndexType.CRT_ORDER,
                            H5IterationOrder.INCREASING,
                            ref position, attributeDelegate,
                            (object)attributeNames);

                // Write out the names of the attributes
                foreach (string attributeName in attributeNames)
                {
                    Console.WriteLine("attribute name is {0}", attributeName);
                }

                // Demonstrate H5A.openName
                attributeId = H5A.openName(groupId, "descendingRamp");
                Console.WriteLine("got {0} by name", H5A.getName(attributeId));
                H5A.close(attributeId);

                // Demonstrate H5A.getNameByIndex
                string secondAttribute = H5A.getNameByIndex(groupId, ".",
                    H5IndexType.CRT_ORDER, H5IterationOrder.INCREASING, 1);

                Console.WriteLine("second attribute is named {0}",
                                  secondAttribute);

                // Demonstrate H5G.getInfo
                H5GInfo gInfo = H5G.getInfo(groupId);
                Console.WriteLine(
                   "link storage: {0}, max creation order: {1}, nLinks: {2}",
                   gInfo.linkStorageType, gInfo.maxCreationOrder, gInfo.nLinks);


                // Demonstrate H5A.getSpace
                //attributeId = H5A.openByName(groupId, ".", "descendingRamp");
                attributeId = H5A.open(groupId, "descendingRamp");
                H5DataSpaceId rampSpaceId = H5A.getSpace(attributeId);
                H5S.close(rampSpaceId);

                // Demonstrate H5A.getType
                H5DataTypeId rampTypeId = H5A.getType(attributeId);
                Console.WriteLine("size of ramp data type is {0} bytes.",
                                  H5T.getSize(rampTypeId));
                H5T.close(rampTypeId);

                // Demonstrate H5A.getInfo
                H5AttributeInfo rampInfo = H5A.getInfo(attributeId);
                Console.WriteLine(
                   "characterset: {0}, creationOrder: {1}, creationOrderValid: {2}, dataSize: {3}",
                   rampInfo.characterSet, rampInfo.creationOrder,
                   rampInfo.creationOrderValid, rampInfo.dataSize);

                // Demonstrate H5A.Delete
                H5A.Delete(groupId, "descendingRamp");
                //H5A.DeleteByName(groupId, ".", "descendingRamp");

                // Iterate through the attributes to show that the deletion 
                // was successful.
                position = 0;
                ArrayList namesAfterDeletion = new ArrayList();
                H5A.iterate(groupId, H5IndexType.CRT_ORDER,
                            H5IterationOrder.DECREASING, ref position,
                            attributeDelegate,
                            (object)namesAfterDeletion);


                H5G.close(groupId);

                H5F.close(fileId);

                // Reopen and reclose the file.
                H5FileId openId = H5F.open("myCSharp.h5",
                                           H5F.OpenMode.ACC_RDONLY);
                H5F.close(openId);

                // Set the function to be called on error.
                unsafe
                {
                    H5AutoCallback myErrorDelegate = new H5AutoCallback(myErrorFunction);
                    H5E.setAuto(0, myErrorDelegate, IntPtr.Zero);
                }

                // Uncomment the next line if you want to generate an error to
                // test H5E.setAuto
                // H5G.open(openId, "noGroup");
            }
            // This catches all the HDF exception classes.  Because each call
            // generates a unique exception, different exception can be handled
            // separately.  For example, to catch open errors we could have used
            // catch (H5FopenException openException).
            catch (HDFException e)
            {
                Console.WriteLine(e.Message);
            }

            Console.WriteLine("Processing complete!");
            Console.ReadLine();
        }
    }
}