using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace WOD_6x_obj1_To_7x
{
    class Program
    {
        static void Main(string[] args)
        {
            if(args.Length == 0)
            {
                args = Directory.GetFiles(Directory.GetCurrentDirectory(),"*_obj0.adt");

                Console.WriteLine("Found " + args.Length + " files.");
            }
            // Mode 0: ExtractBBoxes from m2s
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
                    LoadCustomRadiusPerPath("radius_bypath.cfg");
                    LoadCustomRadiusPerID("radius_byid.cfg");
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
            Console.Beep();
            Console.ReadLine();
        }

        private static void LoadCustomRadiusPerPath(string v)
        {
            StreamReader sr = new StreamReader(v);
            while (!sr.EndOfStream)
            {
                
                string[] thingy = sr.ReadLine().Split(';');
                if(thingy[0].IndexOf("--") == 0)
                {
                    continue;
                }
                string path = thingy[0].ToLower();

                if(!ModelName_To_DefaultAABox.ContainsKey(path))
                {
                    if(!path.Contains(".wmo"))
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
                ModelName_To_DefaultAABox[file.Substring(6).Replace('\\','/').ToLower()] = new ModelData(boxMin, boxMax);
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

            public string modelName;
            public ModelData calculatedAABBox;
            public uint uniqueID;

            void SetAsPlacedAABBox(C3Vector min, C3Vector max, float[] placementMatrix, float pathRadius)
            {
                // Turn the default aabbox into  vertices
                float[][] vec = new float[8][];
                
                vec[0] = Vec4.FromValues(min.X ,min.Y, min.Z,1);
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
                else if (pathRadius != 0)
                {
                    calculatedAABBox.radius *= pathRadius;
                }
                else
                    calculatedAABBox.RecalcRadius();

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
                

                float[] scaleVector = {scale / 1024.0f, scale / 1024.0f, scale / 1024.0f };
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
                    uint uniqueID = br.ReadUInt32();

                    thisDoodad.uniqueID = uniqueID;

                    bw.Write(uniqueID);

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
                        tempBw.Write(br.ReadBytes(4)); // Write Start Of entry
                        uint uniqueID = br.ReadUInt32();
                        tempBw.Write(uniqueID);
                        tempBw.Write(br.ReadBytes(24));

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
                        if (CustomRadiusPerID.ContainsKey(uniqueID))
                        {
                            md.radius *= CustomRadiusPerID[uniqueID][0];

                            md.AABBoxMin.X -= CustomRadiusPerID[uniqueID][1];
                            md.AABBoxMin.Y -= CustomRadiusPerID[uniqueID][1];
                            md.AABBoxMin.Z -= CustomRadiusPerID[uniqueID][1];

                            md.AABBoxMax.X += CustomRadiusPerID[uniqueID][1];
                            md.AABBoxMax.Y += CustomRadiusPerID[uniqueID][1];
                            md.AABBoxMax.Z += CustomRadiusPerID[uniqueID][1];
                        }

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
