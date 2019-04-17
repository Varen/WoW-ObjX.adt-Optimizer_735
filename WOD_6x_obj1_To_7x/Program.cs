using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WOD_6x_obj1_To_7x
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length == 0)
            {
                Console.WriteLine("Drag and drop the _obj0 files on the executable to use this program.");
                Console.WriteLine("To generate bboxes, add 'extractBBoxes' as command line argument");
                Console.ReadLine();
                return;
            }
            // Mode 0: ExtractBBoxes from obj1s
            if(args[0] == "extractBBoxes")
            {
                GenerateBBOxesFile();
                Console.WriteLine("Generated BoundingBoxData.dat");
            }

            // Mode 1: do the obj1s from obj0s
            else
            {
                bool loaded = false;
                try
                {
                    Console.WriteLine("Loading BoundingBoxData.dat");
                    LoadDefaultBoxData("BoundingBoxData.dat");
                    loaded = true;
                }
                catch (Exception)
                {
                    Console.WriteLine("Loading of BoundingBoxData.dat failed!");
                }

                if(loaded)
                {
                    Console.WriteLine("Generating obj1s..");
                    GenerateObj1FromObj0(args);
                }
            }
            Console.WriteLine("All Done!");
            Console.ReadLine();
        }

        public struct ModelData
        {
            public C3Vector AABBoxMin;
            public C3Vector AABBoxMax;
            public float radius;

            public ModelData(C3Vector aabox_min, C3Vector aabox_max, bool calcRadius = false)
            {
                AABBoxMin = aabox_min;
                AABBoxMax = aabox_max;

                if (calcRadius)
                {
                    float tempRadius = Math.Abs(AABBoxMax.X - AABBoxMin.X);
                    radius = tempRadius;

                    tempRadius = Math.Abs(AABBoxMax.Y - AABBoxMin.Y);
                    if (tempRadius > radius)
                        radius = tempRadius;

                    tempRadius = Math.Abs(AABBoxMax.Z - AABBoxMin.Z);

                    if (tempRadius > radius)
                        radius = tempRadius;
                }
                else
                    radius = 0;
            }
        }

        // Contains the string -> default BoundingBox data
        public static readonly Dictionary<string, ModelData> ModelName_To_DefaultAABox = new Dictionary<string, ModelData>();

        static void SaveDefaultBoxData(string saveFile)
        {
            BinaryWriter bw = new BinaryWriter(File.OpenWrite(saveFile));
            foreach (var item in ModelName_To_DefaultAABox)
            {
                bw.Write(item.Key);
                bw.Write(item.Value.AABBoxMin.X);
                bw.Write(item.Value.AABBoxMin.Y);
                bw.Write(item.Value.AABBoxMin.Z);
                bw.Write(item.Value.AABBoxMax.X);
                bw.Write(item.Value.AABBoxMax.Y);
                bw.Write(item.Value.AABBoxMax.Z);
            }
            bw.Close();
        }

        static void LoadDefaultBoxData(string loadFile)
        {
            BinaryReader br = new BinaryReader(File.OpenRead(loadFile));

            while (br.BaseStream.Position < br.BaseStream.Length)
            {
                string modelName = br.ReadString();
                C3Vector boxMin = new C3Vector();
                C3Vector boxMax = new C3Vector();

                boxMin.X = br.ReadSingle();
                boxMin.Y = br.ReadSingle();
                boxMin.Z = br.ReadSingle();

                boxMax.X = br.ReadSingle();
                boxMax.Y = br.ReadSingle();
                boxMax.Z = br.ReadSingle();

                ModelName_To_DefaultAABox[modelName] = new ModelData(boxMin, boxMax);
            }
            br.Close();
        }

        private static void GenerateBBOxesFile()
        {
            string[] allObj1s = Directory.GetFiles("input", "*.m2", SearchOption.AllDirectories);


            foreach (var file in allObj1s)
            {
                BinaryReader br = new BinaryReader(File.OpenRead(file));
                br.BaseStream.Seek(0x0A0, SeekOrigin.Begin);

                C3Vector boxMin = new C3Vector();
                C3Vector boxMax = new C3Vector();

                boxMin.X = br.ReadSingle();
                boxMin.Y = br.ReadSingle();
                boxMin.Z = br.ReadSingle();

                boxMax.X = br.ReadSingle();
                boxMax.Y = br.ReadSingle();
                boxMax.Z = br.ReadSingle();
                ModelName_To_DefaultAABox[file.Substring(6).Replace('\\','/')] = new ModelData(boxMin, boxMax);
                br.Close();
            }
            SaveDefaultBoxData("BoundingBoxData.dat");
        }


        class ModelInstanceData
        {
            public C3Vector position;
            public C3Vector rotation;
            public ushort scale; // 1024 = 1

            public string modelName;
            public ModelData calculatedAABBox;

            public void CalculateBox()
            {
                // TODO: calculate box here
                // TEMP:





                calculatedAABBox.AABBoxMin = new C3Vector(); //ModelName_To_DefaultAABox[modelName].AABBoxMin;
                calculatedAABBox.AABBoxMax = new C3Vector(); //ModelName_To_DefaultAABox[modelName].AABBoxMax;
                calculatedAABBox.radius = 50; // This gotta be mathed from biggest distance min-max
            }
        }
        const float ServerClientCoordinateDifference = 51200.0f / 3.0f;

        // 
        static float ClientPosToServerPos(float input)
        {
            return ServerClientCoordinateDifference - input;
        }

        private static void GenerateObj1FromObj0(string[] args)
        {

            Console.WriteLine("Processing " + args.Length + " element(s)");
            Console.ReadLine();
            foreach (var arg in args)
            {
                Console.Write('.');
                BinaryReader br = new BinaryReader(File.Open(arg, FileMode.Open));

                int replPos = arg.LastIndexOf("obj0");

                string newName = (arg.Remove(replPos, 4)).Insert(replPos, "obj1");
                BinaryWriter bw = new BinaryWriter(File.OpenWrite(newName));

                // Write Header + MMDX Tag
                bw.Write(br.ReadBytes(16));

                // End Offset
                UInt32 strEndOffset = br.ReadUInt32();
                bw.Write(strEndOffset);


                StringBuilder sb = new StringBuilder();


                List<string> M2Strings = new List<string>();

                UInt32 MMDXStringCount = 0;
                // MMDX: Write Strings
                while (br.BaseStream.Position < strEndOffset)
                {
                    sb.Clear();
                    MMDXStringCount++;
                    byte b = 50;
                    while (b != 0x0)
                    {
                        b = br.ReadByte();
                        bw.Write(b);
                        sb.Append((char)b);
                    }
                    sb.Remove(sb.Length - 1, 1);
                    M2Strings.Add(sb.ToString());
                }

                // MMID Tag and Offset
                bw.Write(br.ReadBytes(8));

                // MMID Offsets
                for (int i = 0; i < MMDXStringCount; i++)
                {
                    bw.Write(br.ReadUInt32());
                }

                List<string> WMOStrings = new List<string>();

                //MWMO Tag
                bw.Write(br.ReadBytes(4));
                strEndOffset = br.ReadUInt32();
                bw.Write(strEndOffset);
                UInt32 MWMOStringCount = 0;
                // MMDX: Write Strings

                long myLongEndOffset = strEndOffset + br.BaseStream.Position;

                

                while (br.BaseStream.Position < myLongEndOffset)
                {
                    sb.Clear();

                    MWMOStringCount++;
                    byte b = 50;
                    
                    while (b != 0x0)
                    {
                        b = br.ReadByte();
                        bw.Write(b);
                        sb.Append((char)b);
                    }
                    sb.Remove(sb.Length - 1, 1);
                    WMOStrings.Add(sb.ToString());
                }

                // MWID Tag and Offset
                bw.Write(br.ReadBytes(8));

                
                // MWID Offsets
                for (int i = 0; i < MWMOStringCount; i++)
                {
                    bw.Write(br.ReadUInt32());
                }

                // CHANGES START HERE
                //TAG RENAME MDDF -> MLDD: Advance 4 and write our own tag instead
                // aka:Doodad placement data is the same as the obj0.
                br.ReadBytes(4);
                char[] DDLMtag = { 'D', 'D', 'L', 'M' };
                bw.Write(DDLMtag);

                strEndOffset = br.ReadUInt32();
                bw.Write(strEndOffset);
                UInt32 MLDDCount = 0;

                List<ModelInstanceData> DoodadPlacementData = new List<ModelInstanceData>();

                myLongEndOffset = strEndOffset + br.BaseStream.Position;
                while (br.BaseStream.Position < myLongEndOffset)
                {
                    MLDDCount++;
                    ModelInstanceData thisDoodad = new ModelInstanceData();

                    #region M2Data
                    uint modelID = br.ReadUInt32();

                    thisDoodad.modelName = M2Strings[Convert.ToInt32(modelID)];

                    bw.Write(modelID);
                    bw.Write(br.ReadUInt32()); // UniqueID

                    thisDoodad.position.X = br.ReadSingle();
                    thisDoodad.position.Y = br.ReadSingle();
                    thisDoodad.position.Z = br.ReadSingle();

                    bw.Write(thisDoodad.position.X);
                    bw.Write(thisDoodad.position.Y);
                    bw.Write(thisDoodad.position.Z);

                    thisDoodad.rotation.X = br.ReadSingle();
                    thisDoodad.rotation.Y = br.ReadSingle();
                    thisDoodad.rotation.Z = br.ReadSingle();

                    bw.Write(thisDoodad.rotation.X);
                    bw.Write(thisDoodad.rotation.Y);
                    bw.Write(thisDoodad.rotation.Z);

                    thisDoodad.scale = br.ReadUInt16();
                    bw.Write(thisDoodad.scale);

                    bw.Write(br.ReadBytes(2)); // Write flags

                    thisDoodad.CalculateBox();
                    DoodadPlacementData.Add(thisDoodad);

                    #endregion
                }

                // FROM HERE ON OUT: Remake Offsets!

                // ALL NEW DATA: MLDX
                {

                    char[] MLDXtag = { 'X', 'D', 'L', 'M' };
                    bw.Write(MLDXtag);


                    MemoryStream tempMs = new MemoryStream();
                    BinaryWriter tempBw = new BinaryWriter(tempMs);
                    for (int i = 0; i < MLDDCount; i++)
                    {
                        var calcedAABBox = DoodadPlacementData[i].calculatedAABBox;
                        
                        // Bounding Box Min
                        tempBw.Write(calcedAABBox.AABBoxMin.X);
                        tempBw.Write(calcedAABBox.AABBoxMin.Y);
                        tempBw.Write(calcedAABBox.AABBoxMin.Z);
                        // Bounding Box Max
                        tempBw.Write(calcedAABBox.AABBoxMax.X);
                        tempBw.Write(calcedAABBox.AABBoxMax.Y);
                        tempBw.Write(calcedAABBox.AABBoxMax.Z);
                        // Radius
                        tempBw.Write(calcedAABBox.radius);
                    }

                    bw.Write(Convert.ToInt32(tempBw.BaseStream.Position));
                    long thePos = tempBw.BaseStream.Position;
                    tempMs.Seek(0, SeekOrigin.Begin);
                    bw.Write((new BinaryReader(tempMs).ReadBytes(Convert.ToInt32(thePos))));

                    tempMs.Close();
                }

                // ALL NEW DATA: MLDL

                {
                    char[] MLDLtag = { 'L', 'D', 'L', 'M' };
                    bw.Write(MLDLtag);


                    MemoryStream tempMs = new MemoryStream();
                    BinaryWriter tempBw = new BinaryWriter(tempMs);
                    for (int i = 0; i < MLDDCount; i++)
                    {
                        tempBw.Write(UInt32.MinValue);
                    }

                    bw.Write(Convert.ToInt32(tempBw.BaseStream.Position));

                    long thePos = tempBw.BaseStream.Position;
                    tempMs.Seek(0, SeekOrigin.Begin);
                    bw.Write((new BinaryReader(tempMs).ReadBytes(Convert.ToInt32(thePos))));

                    tempMs.Close();
                }


                List<ModelData> WMOBBoxes = new List<ModelData>();

                UInt32 MLMDCount = 0;
                // MLMD -> Same as MODF, without the Bounding Boxes
                {
                    br.ReadBytes(4);
                    char[] MLMDtag = { 'D', 'M', 'L', 'M' };
                    bw.Write(MLMDtag);


                    strEndOffset = br.ReadUInt32();

                    // Use offset to read all data. Convert BBoxes coordinates. Save in MemoryStream.

                    MemoryStream tempMs = new MemoryStream();
                    BinaryWriter tempBw = new BinaryWriter(tempMs);

                    myLongEndOffset = strEndOffset + br.BaseStream.Position;
                    while (br.BaseStream.Position < myLongEndOffset)
                    {
                        MLMDCount++;
                        tempBw.Write(br.ReadBytes(32)); // Write Start Of entry

                        // We save the wmo bbox for later use, and convert it too 'cause we that good.
                        C3Vector AABBoxMax = new C3Vector();
                        C3Vector AABBoxMin = new C3Vector();

                        // Things get switched around here because the client and server directions are different for min and max
                        AABBoxMax.Y = ClientPosToServerPos(br.ReadSingle());
                        AABBoxMin.Z = br.ReadSingle();
                        AABBoxMax.X = ClientPosToServerPos(br.ReadSingle());

                        AABBoxMin.Y = ClientPosToServerPos(br.ReadSingle());
                        AABBoxMax.Z = br.ReadSingle();
                        AABBoxMin.X = ClientPosToServerPos(br.ReadSingle());

                        ModelData md = new ModelData(AABBoxMin, AABBoxMax, true);
                        WMOBBoxes.Add(md);

                        tempBw.Write(br.ReadBytes(8)); // Write rest of Entry
                    }

                    // Write offset to end of MemoryStream, then write MemoryStream contents
                    bw.Write(Convert.ToInt32(tempBw.BaseStream.Position));

                    long thePos = tempBw.BaseStream.Position;
                    tempMs.Seek(0, SeekOrigin.Begin);
                    bw.Write((new BinaryReader(tempMs).ReadBytes(Convert.ToInt32(thePos))));

                    tempMs.Close();
                }

                // ALL NEW DATA: MLMX
                {

                    char[] MLMXtag = { 'X', 'M', 'L', 'M' };
                    bw.Write(MLMXtag);

                    MemoryStream tempMs = new MemoryStream();
                    BinaryWriter tempBw = new BinaryWriter(tempMs);
                    for (int i = 0; i < MLMDCount; i++)
                    {
                        // Bounding Box Min
                        tempBw.Write(WMOBBoxes[i].AABBoxMin.X);
                        tempBw.Write(WMOBBoxes[i].AABBoxMin.Y);
                        tempBw.Write(WMOBBoxes[i].AABBoxMin.Z);

                        // Bounding Box Max
                        tempBw.Write(WMOBBoxes[i].AABBoxMax.X);
                        tempBw.Write(WMOBBoxes[i].AABBoxMax.Y);
                        tempBw.Write(WMOBBoxes[i].AABBoxMax.Z);

                        // Radius
                        tempBw.Write(WMOBBoxes[i].radius);
                    }

                    bw.Write(Convert.ToInt32(tempBw.BaseStream.Position));
                    long thePos = tempBw.BaseStream.Position;
                    tempMs.Seek(0, SeekOrigin.Begin);
                    bw.Write((new BinaryReader(tempMs).ReadBytes(Convert.ToInt32(thePos))));

                    tempMs.Close();
                }

                // END
                br.Close();
                bw.Close();
            }
        }
    }
}
