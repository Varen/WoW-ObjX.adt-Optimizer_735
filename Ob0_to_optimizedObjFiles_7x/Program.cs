using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Obj_Optimizer_7x
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                args = Directory.GetFiles(Directory.GetCurrentDirectory() + "\\input", "*_obj0.adt");

                Console.WriteLine("Found " + args.Length + " files.");
            }

            if (args.Length == 0)
            {
                Console.WriteLine("No files found in Input!");
                Console.ReadLine();
                Environment.Exit(0);
            }
            // Mode 0: ExtractBBoxes from m2s
            if (args[0] == "extractBBoxes")
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
                    LoadCustomRadiusPerPath("radius_bypath.cfg");
                    LoadCustomRadiusPerID("radius_byid.cfg");
                    loaded = true;
                }
                catch (Exception)
                {
                    Console.WriteLine("Loading of BoundingBoxData.dat failed!");
                }

                if (loaded)
                {
                    Console.WriteLine("Generating obj1s..");
                    GenerateObj1FromObj0(args);
                }
            }
            Console.WriteLine("All Done!");
            Console.Beep();
            Console.ReadLine();
        }

        private static void LoadCustomRadiusPerPath(string v)
        {
            StreamReader sr = new StreamReader(v);
            while (!sr.EndOfStream)
            {

                string[] thingy = sr.ReadLine().Split(';');
                if (thingy[0].IndexOf("--") == 0)
                {
                    continue;
                }
                string path = thingy[0].ToLower();

                if (!ModelName_To_DefaultAABox.ContainsKey(path))
                {
                    if (!path.Contains(".wmo"))
                    {
                        Console.Beep();
                        MessageBox.Show("Invalid model path: " + path, "Error");
                    }
                    continue;
                }
                var val = ModelName_To_DefaultAABox[path];
                val.radius = Convert.ToSingle(thingy[1]);
                ModelName_To_DefaultAABox[path] = val;
            }
            sr.Close();
        }

        static Dictionary<uint, float[]> CustomRadiusPerID = new Dictionary<uint, float[]>();

        private static void LoadCustomRadiusPerID(string v)
        {
            StreamReader sr = new StreamReader(v);
            while (!sr.EndOfStream)
            {
                string[] thingy = sr.ReadLine().Split(';');

                if (thingy[0].IndexOf("--") == 0)
                {
                    continue;
                }

                uint ID = Convert.ToUInt32(thingy[0]);
                float addRad = Convert.ToSingle(thingy[1]);
                float addBounds = Convert.ToSingle(thingy[2]);
                float[] val = { addRad, addBounds };
                CustomRadiusPerID[ID] = val;
            }
            sr.Close();
        }

        public struct ModelData
        {
            public C3Vector AABBoxMin;
            public C3Vector AABBoxMax;
            public float radius;

            public void SetRadius(float x)
            {
                radius = x;
            }
            public ModelData(C3Vector aabox_min, C3Vector aabox_max, bool calcRadius = false)
            {
                AABBoxMin = aabox_min;
                AABBoxMax = aabox_max;
                radius = 0;

                if (calcRadius)
                {
                    RecalcRadius();
                }
            }

            public void RecalcRadius()
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
        }

        // Contains the string -> default BoundingBox data
        public static Dictionary<string, ModelData> ModelName_To_DefaultAABox = new Dictionary<string, ModelData>();

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

                ModelName_To_DefaultAABox[modelName.ToLower()] = new ModelData(boxMin, boxMax);
            }
            br.Close();
        }

        private static void GenerateBBOxesFile()
        {
            string[] allM2s = Directory.GetFiles("input", "*.m2", SearchOption.AllDirectories);

            Console.WriteLine("Found " + allM2s.Length + " files.");

            foreach (var file in allM2s)
            {
                //Console.Write('.');
                BinaryReader br = new BinaryReader(File.OpenRead(file));
                br.BaseStream.Seek(0x0A8, SeekOrigin.Begin);

                C3Vector boxMin = new C3Vector();
                C3Vector boxMax = new C3Vector();

                boxMin.X = br.ReadSingle();
                boxMin.Y = br.ReadSingle();
                boxMin.Z = br.ReadSingle();

                boxMax.X = br.ReadSingle();
                boxMax.Y = br.ReadSingle();
                boxMax.Z = br.ReadSingle();
                ModelName_To_DefaultAABox[file.Substring(6).Replace('\\', '/').ToLower()] = new ModelData(boxMin, boxMax);
                br.Close();
            }
            SaveDefaultBoxData("BoundingBoxData.dat");
        }

        // Convenience
        static float ToRadian(float x)
        {
            return Convert.ToSingle(Math.PI * x / 180.0f);
        }

        class ModelInstanceData
        {
            public C3Vector position;
            public C3Vector rotation;
            public ushort scale; // 1024 = 1

            public uint modelId;
            public string modelName;
            public ModelData calculatedAABBox;
            public ModelData originalAABBox;
            public uint uniqueID;
            public ushort flags;
            public ushort doodadSet;
            public ushort nameSet;
            public ushort padding;

            void SetAsPlacedAABBox(C3Vector min, C3Vector max, float[] placementMatrix, float pathRadius)
            {
                // Turn the default aabbox into  vertices
                float[][] vec = new float[8][];

                vec[0] = Vec4.FromValues(min.X, min.Y, min.Z, 1);
                vec[1] = Vec4.FromValues(max.X, max.Y, max.Z, 1);
                vec[2] = Vec4.FromValues(min.X, min.Y, max.Z, 1);
                vec[3] = Vec4.FromValues(min.X, max.Y, min.Z, 1);
                vec[4] = Vec4.FromValues(max.X, min.Y, min.Z, 1);
                vec[5] = Vec4.FromValues(min.X, max.Y, max.Z, 1);
                vec[6] = Vec4.FromValues(max.X, min.Y, max.Z, 1);
                vec[7] = Vec4.FromValues(max.X, max.Y, min.Z, 1);

                // Rotate each point properly

                for (int i = 0; i < 8; i++)
                {
                    vec[i] = Vec4.TransformMat4(vec[i], vec[i], placementMatrix);
                }

                calculatedAABBox.AABBoxMin.X = float.MaxValue;
                calculatedAABBox.AABBoxMin.Y = float.MaxValue;
                calculatedAABBox.AABBoxMin.Z = float.MaxValue;

                calculatedAABBox.AABBoxMax.X = float.MinValue;
                calculatedAABBox.AABBoxMax.Y = float.MinValue;
                calculatedAABBox.AABBoxMax.Z = float.MinValue;

                // Find the mins and maxes to make an AABBox
                for (int i = 0; i < 8; i++)
                {
                    // X
                    if (vec[i][0] > calculatedAABBox.AABBoxMax.X)
                        calculatedAABBox.AABBoxMax.X = vec[i][0];
                    if (vec[i][0] < calculatedAABBox.AABBoxMin.X)
                        calculatedAABBox.AABBoxMin.X = vec[i][0];

                    // Y
                    if (vec[i][1] > calculatedAABBox.AABBoxMax.Y)
                        calculatedAABBox.AABBoxMax.Y = vec[i][1];
                    if (vec[i][1] < calculatedAABBox.AABBoxMin.Y)
                        calculatedAABBox.AABBoxMin.Y = vec[i][1];

                    // Z
                    if (vec[i][2] > calculatedAABBox.AABBoxMax.Z)
                        calculatedAABBox.AABBoxMax.Z = vec[i][2];
                    if (vec[i][2] < calculatedAABBox.AABBoxMin.Z)
                        calculatedAABBox.AABBoxMin.Z = vec[i][2];
                }

                if (CustomRadiusPerID.ContainsKey(uniqueID))
                {
                    calculatedAABBox.RecalcRadius();
                    calculatedAABBox.radius *= CustomRadiusPerID[uniqueID][0];

                    calculatedAABBox.AABBoxMin.X -= CustomRadiusPerID[uniqueID][1];
                    calculatedAABBox.AABBoxMin.Y -= CustomRadiusPerID[uniqueID][1];
                    calculatedAABBox.AABBoxMin.Z -= CustomRadiusPerID[uniqueID][1];


                    calculatedAABBox.AABBoxMax.X += CustomRadiusPerID[uniqueID][1];
                    calculatedAABBox.AABBoxMax.Y += CustomRadiusPerID[uniqueID][1];
                    calculatedAABBox.AABBoxMax.Z += CustomRadiusPerID[uniqueID][1];

                }
                else
                {
                    calculatedAABBox.RecalcRadius();
                    if (pathRadius != 0)
                    {
                        calculatedAABBox.radius *= pathRadius;
                    }
                }


            }

            public void CalculateBox()
            {
                // Prepare the placement matrix
                var posx = ServerClientCoordinateDifference - position.X;
                var posy = position.Y;
                var posz = ServerClientCoordinateDifference - position.Z;

                float[] posVec = { posx, posy, posz };

                var placementMatrix = Mat4.Create();
                placementMatrix = Mat4.Identity_(placementMatrix);


                placementMatrix = Mat4.RotateX(placementMatrix, placementMatrix, ToRadian(90));
                placementMatrix = Mat4.RotateY(placementMatrix, placementMatrix, ToRadian(90));

                placementMatrix = Mat4.Translate(placementMatrix, placementMatrix, posVec);

                placementMatrix = Mat4.RotateY(placementMatrix, placementMatrix, ToRadian(rotation.Y - 270));
                placementMatrix = Mat4.RotateZ(placementMatrix, placementMatrix, ToRadian(-rotation.X));
                placementMatrix = Mat4.RotateX(placementMatrix, placementMatrix, ToRadian(rotation.Z - 90));

                //placementMatrix = Mat4.RotateX(placementMatrix, placementMatrix, ToRadian(rotation.Z - 90));
                //placementMatrix = Mat4.RotateY(placementMatrix, placementMatrix, ToRadian(rotation.Y - 270));
                //placementMatrix = Mat4.RotateZ(placementMatrix, placementMatrix, ToRadian(rotation.Y - 180));


                float[] scaleVector = { scale / 1024.0f, scale / 1024.0f, scale / 1024.0f };
                placementMatrix = Mat4.Scale(placementMatrix, placementMatrix, scaleVector);

                // Take the default box and apply the transform
                string modelToFind = modelName.ToLower().Replace('\\', '/');

                SetAsPlacedAABBox(ModelName_To_DefaultAABox[modelToFind].AABBoxMin, ModelName_To_DefaultAABox[modelToFind].AABBoxMax, placementMatrix, ModelName_To_DefaultAABox[modelToFind].radius);
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
            foreach (var arg in args)
            {
                Console.Write('.');
                BinaryReader br_OriginalObj0 = new BinaryReader(File.Open(arg, FileMode.Open));

                int replPos = arg.LastIndexOf("obj0");

                string newObj0Name = Path.GetFileName((arg.Remove(replPos, 4)).Insert(replPos, "obj0"));
                string newObj1Name = Path.GetFileName((arg.Remove(replPos, 4)).Insert(replPos, "obj1"));
                string newFolder = Path.Combine(Directory.GetCurrentDirectory(), "output");
                Directory.CreateDirectory(newFolder);

                BinaryWriter bw_obj1 = new BinaryWriter(File.OpenWrite(Path.Combine(newFolder, newObj1Name)));
                BinaryWriter bw_obj0 = new BinaryWriter(File.OpenWrite(Path.Combine(newFolder, newObj0Name)));

                byte[] readBuffer = br_OriginalObj0.ReadBytes(16);

                // Write Header + MMDX Tag
                bw_obj0.Write(readBuffer);
                bw_obj1.Write(readBuffer);

                // End Offset
                UInt32 strEndOffset = br_OriginalObj0.ReadUInt32();

                bw_obj0.Write(strEndOffset);
                bw_obj1.Write(strEndOffset);


                StringBuilder sb = new StringBuilder();


                List<string> M2Strings = new List<string>();

                UInt32 MMDXStringCount = 0;
                // MMDX: Write Strings
                while (br_OriginalObj0.BaseStream.Position < strEndOffset)
                {
                    sb.Clear();
                    MMDXStringCount++;
                    byte b = 50;
                    while (b != 0x0)
                    {
                        b = br_OriginalObj0.ReadByte();
                        bw_obj0.Write(b);
                        bw_obj1.Write(b);
                        sb.Append((char)b);
                    }
                    sb.Remove(sb.Length - 1, 1);
                    M2Strings.Add(sb.ToString());
                }

                // MMID Tag and Offset
                readBuffer = br_OriginalObj0.ReadBytes(8);
                bw_obj0.Write(readBuffer);
                bw_obj1.Write(readBuffer);

                // MMID Offsets
                for (int i = 0; i < MMDXStringCount; i++)
                {
                    readBuffer = br_OriginalObj0.ReadBytes(4);
                    bw_obj0.Write(readBuffer);
                    bw_obj1.Write(readBuffer);
                }

                List<string> WMOStrings = new List<string>();

                //MWMO Tag
                readBuffer = br_OriginalObj0.ReadBytes(4);
                bw_obj0.Write(readBuffer);
                bw_obj1.Write(readBuffer);


                strEndOffset = br_OriginalObj0.ReadUInt32();
                bw_obj0.Write(strEndOffset);
                bw_obj1.Write(strEndOffset);

                UInt32 MWMOStringCount = 0;
                // MMDX: Write Strings

                long myLongEndOffset = strEndOffset + br_OriginalObj0.BaseStream.Position;



                while (br_OriginalObj0.BaseStream.Position < myLongEndOffset)
                {
                    sb.Clear();

                    MWMOStringCount++;
                    byte b = 50;

                    while (b != 0x0)
                    {
                        b = br_OriginalObj0.ReadByte();
                        bw_obj0.Write(b);
                        bw_obj1.Write(b);
                        sb.Append((char)b);
                    }
                    sb.Remove(sb.Length - 1, 1);
                    WMOStrings.Add(sb.ToString());
                }

                // MWID Tag and Offset
                readBuffer = br_OriginalObj0.ReadBytes(8);
                bw_obj0.Write(readBuffer);
                bw_obj1.Write(readBuffer);


                // MWID Offsets
                for (int i = 0; i < MWMOStringCount; i++)
                {
                    readBuffer = br_OriginalObj0.ReadBytes(4);
                    bw_obj0.Write(readBuffer);
                    bw_obj1.Write(readBuffer);
                }

                //  CHANGES START HERE
                //  TAG RENAME MDDF -> MLDD: Advance 4 and write our own tag instead
                //  Read it all and sort by size to write up later.
                bw_obj0.Write(br_OriginalObj0.ReadBytes(4));

                char[] DDLMtag = { 'D', 'D', 'L', 'M' };
                bw_obj1.Write(DDLMtag);

                strEndOffset = br_OriginalObj0.ReadUInt32();
                bw_obj0.Write(strEndOffset);
                bw_obj1.Write(strEndOffset);
                UInt32 MLDDCount = 0;

                List<ModelInstanceData> DoodadPlacementData = new List<ModelInstanceData>();

                myLongEndOffset = strEndOffset + br_OriginalObj0.BaseStream.Position;
                while (br_OriginalObj0.BaseStream.Position < myLongEndOffset)
                {
                    MLDDCount++;
                    ModelInstanceData thisDoodad = new ModelInstanceData();

                    #region M2Data
                    uint modelID = br_OriginalObj0.ReadUInt32();

                    thisDoodad.modelId = modelID;
                    thisDoodad.modelName = M2Strings[Convert.ToInt32(modelID)];

                    uint uniqueID = br_OriginalObj0.ReadUInt32();

                    thisDoodad.uniqueID = uniqueID;

                    thisDoodad.position.X = br_OriginalObj0.ReadSingle();
                    thisDoodad.position.Y = br_OriginalObj0.ReadSingle();
                    thisDoodad.position.Z = br_OriginalObj0.ReadSingle();

                    thisDoodad.rotation.X = br_OriginalObj0.ReadSingle();
                    thisDoodad.rotation.Y = br_OriginalObj0.ReadSingle();
                    thisDoodad.rotation.Z = br_OriginalObj0.ReadSingle();

                    thisDoodad.scale = br_OriginalObj0.ReadUInt16();
                    thisDoodad.flags = br_OriginalObj0.ReadUInt16();

                    thisDoodad.CalculateBox();
                    DoodadPlacementData.Add(thisDoodad);

                    #endregion
                }

                var sortedDoods = DoodadPlacementData
                    .Select((x, i) => new KeyValuePair<ModelInstanceData, int>(x, i))
                    .OrderByDescending(x => x.Key.calculatedAABBox.radius)
                    .ToList();

                List<ModelInstanceData> DoodadPlacementData_Sorted = sortedDoods.Select(x => x.Key).ToList();
                List<int> DoodadPlacementData_SortedIndex_To_OldIndex = sortedDoods.Select(x => x.Value).ToList();

                // Sort by size, then move on writing.
                DoodadPlacementData.Sort((a, b) => b.calculatedAABBox.radius.CompareTo(a.calculatedAABBox.radius));

                for (int i = 0; i < DoodadPlacementData_Sorted.Count; i++)
                {
                    bw_obj0.Write(DoodadPlacementData_Sorted[i].modelId);
                    bw_obj0.Write(DoodadPlacementData_Sorted[i].uniqueID);

                    bw_obj0.Write(DoodadPlacementData_Sorted[i].position.X);
                    bw_obj0.Write(DoodadPlacementData_Sorted[i].position.Y);
                    bw_obj0.Write(DoodadPlacementData_Sorted[i].position.Z);

                    bw_obj0.Write(DoodadPlacementData_Sorted[i].rotation.X);
                    bw_obj0.Write(DoodadPlacementData_Sorted[i].rotation.Y);
                    bw_obj0.Write(DoodadPlacementData_Sorted[i].rotation.Z);

                    bw_obj0.Write(DoodadPlacementData_Sorted[i].scale);
                    bw_obj0.Write(DoodadPlacementData_Sorted[i].flags);

                    bw_obj1.Write(DoodadPlacementData_Sorted[i].modelId);
                    bw_obj1.Write(DoodadPlacementData_Sorted[i].uniqueID);

                    bw_obj1.Write(DoodadPlacementData_Sorted[i].position.X);
                    bw_obj1.Write(DoodadPlacementData_Sorted[i].position.Y);
                    bw_obj1.Write(DoodadPlacementData_Sorted[i].position.Z);

                    bw_obj1.Write(DoodadPlacementData_Sorted[i].rotation.X);
                    bw_obj1.Write(DoodadPlacementData_Sorted[i].rotation.Y);
                    bw_obj1.Write(DoodadPlacementData_Sorted[i].rotation.Z);

                    bw_obj1.Write(DoodadPlacementData_Sorted[i].scale);
                    bw_obj1.Write(DoodadPlacementData_Sorted[i].flags);
                }

                // FROM HERE ON OUT: Remake Offsets!

                // ALL NEW DATA: MLDX
                {

                    char[] MLDXtag = { 'X', 'D', 'L', 'M' };
                    bw_obj1.Write(MLDXtag);


                    MemoryStream tempMs = new MemoryStream();
                    BinaryWriter tempBw = new BinaryWriter(tempMs);
                    for (int i = 0; i < MLDDCount; i++)
                    {
                        var calcedAABBox = DoodadPlacementData_Sorted[i].calculatedAABBox;

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

                    bw_obj1.Write(Convert.ToInt32(tempBw.BaseStream.Position));
                    long thePos = tempBw.BaseStream.Position;
                    tempMs.Seek(0, SeekOrigin.Begin);
                    bw_obj1.Write((new BinaryReader(tempMs).ReadBytes(Convert.ToInt32(thePos))));

                    tempMs.Close();
                }

                // ALL NEW DATA: MLDL

                {
                    char[] MLDLtag = { 'L', 'D', 'L', 'M' };
                    bw_obj1.Write(MLDLtag);


                    MemoryStream tempMs = new MemoryStream();
                    BinaryWriter tempBw = new BinaryWriter(tempMs);
                    for (int i = 0; i < MLDDCount; i++)
                    {
                        tempBw.Write(UInt32.MinValue);
                    }

                    bw_obj1.Write(Convert.ToInt32(tempBw.BaseStream.Position));

                    long thePos = tempBw.BaseStream.Position;
                    tempMs.Seek(0, SeekOrigin.Begin);
                    bw_obj1.Write((new BinaryReader(tempMs).ReadBytes(Convert.ToInt32(thePos))));

                    tempMs.Close();
                }


                List<ModelInstanceData> WMOInstancesData = new List<ModelInstanceData>();
                //List<ModelData> WMOBBoxes = new List<ModelData>();

                UInt32 MLMDCount = 0;
                // MLMD -> Same as MODF, without the Bounding Boxes

                bw_obj0.Write(br_OriginalObj0.ReadBytes(4));
                char[] MLMDtag = { 'D', 'M', 'L', 'M' };
                bw_obj1.Write(MLMDtag);


                strEndOffset = br_OriginalObj0.ReadUInt32();

                // Use offset to read all data. Convert BBoxes coordinates. Store for later saving

                //MemoryStream tempMs = new MemoryStream();

                myLongEndOffset = strEndOffset + br_OriginalObj0.BaseStream.Position;
                while (br_OriginalObj0.BaseStream.Position < myLongEndOffset)
                {
                    MLMDCount++;
                    ModelInstanceData tempMID = new ModelInstanceData();

                    tempMID.modelId = br_OriginalObj0.ReadUInt32();

                    tempMID.uniqueID = br_OriginalObj0.ReadUInt32();
                    tempMID.position.X = br_OriginalObj0.ReadSingle();
                    tempMID.position.Y = br_OriginalObj0.ReadSingle();
                    tempMID.position.Z = br_OriginalObj0.ReadSingle();

                    tempMID.rotation.X = br_OriginalObj0.ReadSingle();
                    tempMID.rotation.Y = br_OriginalObj0.ReadSingle();
                    tempMID.rotation.Z = br_OriginalObj0.ReadSingle();

                    // We save the wmo bbox for later use, and convert it too 'cause we that good.
                    C3Vector AABBoxMax = new C3Vector();
                    C3Vector AABBoxMin = new C3Vector();

                    C3Vector AABBOxMaxOriginal = new C3Vector();
                    C3Vector AABBOxMinOriginal = new C3Vector();

                    // Things get switched around here because the client and server directions are different for min and max
                    AABBOxMinOriginal.X = br_OriginalObj0.ReadSingle();
                    AABBOxMinOriginal.Y = br_OriginalObj0.ReadSingle();
                    AABBOxMinOriginal.Z = br_OriginalObj0.ReadSingle();

                    AABBOxMaxOriginal.X = br_OriginalObj0.ReadSingle();
                    AABBOxMaxOriginal.Y = br_OriginalObj0.ReadSingle();
                    AABBOxMaxOriginal.Z = br_OriginalObj0.ReadSingle();

                    ModelData originalMd = new ModelData
                    {
                        AABBoxMax = AABBOxMaxOriginal,
                        AABBoxMin = AABBOxMinOriginal
                    };

                    tempMID.originalAABBox = originalMd;

                    AABBoxMax.Y = ClientPosToServerPos(AABBOxMinOriginal.X);
                    AABBoxMin.Z = AABBOxMinOriginal.Y;
                    AABBoxMax.X = ClientPosToServerPos(AABBOxMinOriginal.Z);

                    AABBoxMin.Y = ClientPosToServerPos(AABBOxMaxOriginal.X);
                    AABBoxMax.Z = AABBOxMaxOriginal.Y;
                    AABBoxMin.X = ClientPosToServerPos(AABBOxMaxOriginal.Z);


                    ModelData md = new ModelData(AABBoxMin, AABBoxMax, true);


                    if (CustomRadiusPerID.ContainsKey(tempMID.uniqueID))
                    {
                        md.radius *= CustomRadiusPerID[tempMID.uniqueID][0];

                        md.AABBoxMin.X -= CustomRadiusPerID[tempMID.uniqueID][1];
                        md.AABBoxMin.Y -= CustomRadiusPerID[tempMID.uniqueID][1];
                        md.AABBoxMin.Z -= CustomRadiusPerID[tempMID.uniqueID][1];

                        md.AABBoxMax.X += CustomRadiusPerID[tempMID.uniqueID][1];
                        md.AABBoxMax.Y += CustomRadiusPerID[tempMID.uniqueID][1];
                        md.AABBoxMax.Z += CustomRadiusPerID[tempMID.uniqueID][1];
                    }

                    tempMID.calculatedAABBox = md;
                    tempMID.flags = br_OriginalObj0.ReadUInt16();
                    tempMID.doodadSet = br_OriginalObj0.ReadUInt16();
                    tempMID.nameSet = br_OriginalObj0.ReadUInt16();
                    tempMID.padding = br_OriginalObj0.ReadUInt16();
                    WMOInstancesData.Add(tempMID);
                }

                var sorted = WMOInstancesData
                        .Select((x, i) => new KeyValuePair<ModelInstanceData, int>(x, i))
                        .OrderByDescending(x => x.Key.calculatedAABBox.radius)
                        .ToList();

                List<ModelInstanceData> WMOInstancesData_Sorted = sorted.Select(x => x.Key).ToList();
                List<int> WMOInstancesData_SortedIndex_To_OldIndex = sorted.Select(x => x.Value).ToList();


                // Write Obj0

                using (MemoryStream tmpMS = new MemoryStream())
                {
                    using (BinaryWriter tmpBwMs = new BinaryWriter(tmpMS))
                    {
                        for (int i = 0; i < WMOInstancesData_Sorted.Count; i++)
                        {
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].modelId);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].uniqueID);

                            tmpBwMs.Write(WMOInstancesData_Sorted[i].position.X);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].position.Y);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].position.Z);

                            tmpBwMs.Write(WMOInstancesData_Sorted[i].rotation.X);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].rotation.Y);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].rotation.Z);

                            tmpBwMs.Write(WMOInstancesData_Sorted[i].originalAABBox.AABBoxMin.X);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].originalAABBox.AABBoxMin.Y);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].originalAABBox.AABBoxMin.Z);

                            tmpBwMs.Write(WMOInstancesData_Sorted[i].originalAABBox.AABBoxMax.X);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].originalAABBox.AABBoxMax.Y);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].originalAABBox.AABBoxMax.Z);

                            tmpBwMs.Write(WMOInstancesData_Sorted[i].flags);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].doodadSet);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].nameSet);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].padding);
                        }
                        bw_obj0.Write(Convert.ToInt32(tmpBwMs.BaseStream.Position));

                        long thePos = tmpBwMs.BaseStream.Position;
                        tmpBwMs.Seek(0, SeekOrigin.Begin);
                        bw_obj0.Write((new BinaryReader(tmpMS).ReadBytes(Convert.ToInt32(thePos))));
                        tmpMS.Close();
                    }
                }

                // Write Obj1
                using (MemoryStream tmpMS = new MemoryStream())
                {
                    using (BinaryWriter tmpBwMs = new BinaryWriter(tmpMS))
                    {
                        for (int i = 0; i < WMOInstancesData_Sorted.Count; i++)
                        {
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].modelId);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].uniqueID);

                            tmpBwMs.Write(WMOInstancesData_Sorted[i].position.X);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].position.Y);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].position.Z);

                            tmpBwMs.Write(WMOInstancesData_Sorted[i].rotation.X);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].rotation.Y);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].rotation.Z);

                            tmpBwMs.Write(WMOInstancesData_Sorted[i].flags);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].doodadSet);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].nameSet);
                            tmpBwMs.Write(WMOInstancesData_Sorted[i].padding);
                        }
                        bw_obj1.Write(Convert.ToInt32(tmpBwMs.BaseStream.Position));

                        long thePos = tmpBwMs.BaseStream.Position;
                        tmpBwMs.Seek(0, SeekOrigin.Begin);
                        bw_obj1.Write((new BinaryReader(tmpMS).ReadBytes(Convert.ToInt32(thePos))));
                        tmpMS.Close();
                    }
                }



                char[] MCRDTag = { 'D', 'R', 'C', 'M' }; // Doodads
                char[] MCRWTag = { 'W', 'R', 'C', 'M' }; // WMOs

                // Write rest of obj0, specifying new obj positions
                while (br_OriginalObj0.BaseStream.Position < br_OriginalObj0.BaseStream.Length)
                {
                    bw_obj0.Write(br_OriginalObj0.ReadChars(4)); // mcnk
                    var mcnkSizePos = bw_obj0.BaseStream.Position; // We will update this later

                    uint sizeLeft = br_OriginalObj0.ReadUInt32();
                    bw_obj0.Write(sizeLeft);

                    uint actualMcnkSize = 0;

                    while (sizeLeft > 0)
                    {
                        char[] tag = br_OriginalObj0.ReadChars(4);
                        uint subSize = br_OriginalObj0.ReadUInt32();

                        sizeLeft -= (8 + subSize);

                        if (tag.SequenceEqual(MCRDTag))
                        {
                            if (subSize > 0)
                            {
                                bw_obj0.Write(tag);
                                bw_obj0.Write(subSize);
                                actualMcnkSize += 8;
                            }

                            while (subSize > 0)
                            {

                                uint mcrdId = br_OriginalObj0.ReadUInt32();
                                uint newId = Convert.ToUInt32(DoodadPlacementData_SortedIndex_To_OldIndex.FindIndex(x => x == mcrdId));
                                bw_obj0.Write(newId);
                                subSize -= 4;
                                actualMcnkSize += 4;
                            }
                        }
                        else if (tag.SequenceEqual(MCRWTag))
                        {
                            if (subSize > 0)
                            {
                                bw_obj0.Write(tag);
                                bw_obj0.Write(subSize);
                                actualMcnkSize += 8;
                            }

                            while (subSize > 0)
                            {
                                uint mcrwId = br_OriginalObj0.ReadUInt32();
                                uint newId = Convert.ToUInt32(WMOInstancesData_SortedIndex_To_OldIndex.FindIndex(x => x == mcrwId));
                                bw_obj0.Write(newId);
                                subSize -= 4;
                                actualMcnkSize += 4;
                            }
                        }
                        else 
                        {
                            bw_obj0.Write(tag);
                            bw_obj0.Write(subSize);

                            actualMcnkSize += 8;
                            if (subSize > 0)
                            { 
                                bw_obj0.Write(br_OriginalObj0.ReadBytes(Convert.ToInt32(subSize)));
                                actualMcnkSize += subSize;
                            }
                        }
                    }

                    // write new size since we may have purged unused bits
                    var curPos = bw_obj0.BaseStream.Position;
                    bw_obj0.BaseStream.Position = mcnkSizePos;
                    bw_obj0.Write(actualMcnkSize);
                    bw_obj0.BaseStream.Position = curPos;

                }

                // ALL NEW DATA: MLMX
                {
                    char[] MLMXtag = { 'X', 'M', 'L', 'M' };
                    bw_obj1.Write(MLMXtag);

                    MemoryStream tempMs = new MemoryStream();
                    BinaryWriter tempBw = new BinaryWriter(tempMs);
                    for (int i = 0; i < MLMDCount; i++)
                    {
                        // Bounding Box Min
                        tempBw.Write(WMOInstancesData_Sorted[i].calculatedAABBox.AABBoxMin.X);
                        tempBw.Write(WMOInstancesData_Sorted[i].calculatedAABBox.AABBoxMin.Y);
                        tempBw.Write(WMOInstancesData_Sorted[i].calculatedAABBox.AABBoxMin.Z);

                        // Bounding Box Max
                        tempBw.Write(WMOInstancesData_Sorted[i].calculatedAABBox.AABBoxMax.X);
                        tempBw.Write(WMOInstancesData_Sorted[i].calculatedAABBox.AABBoxMax.Y);
                        tempBw.Write(WMOInstancesData_Sorted[i].calculatedAABBox.AABBoxMax.Z);

                        // Radius
                        tempBw.Write(WMOInstancesData_Sorted[i].calculatedAABBox.radius);
                    }

                    bw_obj1.Write(Convert.ToInt32(tempBw.BaseStream.Position));
                    long thePos = tempBw.BaseStream.Position;
                    tempMs.Seek(0, SeekOrigin.Begin);
                    bw_obj1.Write((new BinaryReader(tempMs).ReadBytes(Convert.ToInt32(thePos))));

                    tempMs.Close();
                }

                // END
                br_OriginalObj0.Close();
                bw_obj0.Close();

                bw_obj1.Close();
            }
        }
    }
}
