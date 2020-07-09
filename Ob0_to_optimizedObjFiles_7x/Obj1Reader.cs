using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/* We use this to read Legion 7.1.5 obj1 files, then we'll use it to extract the bounding data for each model.
 * 
 */
namespace Obj_Optimizer_7x
{

    struct C3Vector
    {
        float x;
        float y;
        float z;

        public float X { get => x; set => x = value; }
        public float Y { get => y; set => y = value; }
        public float Z { get => z; set => z = value; }
        public C3Vector(float X, float Y, float Z)
        {
            x = X;
            y = Y;
            z = Z;
        }
    }

    struct U3Vector
    {
        uint x;
        uint y;
        uint z;

        public uint X { get => x; set => x = value; }
        public uint Y { get => y; set => y = value; }
        public uint Z { get => z; set => z = value; }
    }

    /*[Flags]
    enum MLDDFlags
    {
        mddf_biodome = 1,             // this sets internal flags to | 0x800 (WDOODADDEF.var0xC).
        mddf_shrubbery = 2,           // the actual meaning of these is unknown to me. maybe biodome is for really big M2s. 6.0.1.18179 seems not to check for this flag
    }
    */
    struct MLDDEntry
    {
        // SMDoodadDef
        uint mmidEntry;           // references an entry in the MMID chunk, specifying the model to use.
        uint uniqueId;            // this ID should be unique for all ADTs currently loaded. Best, they are unique for the whole map. Blizzard has these unique for the whole game.
        public C3Vector position;            // This is relative to a corner of the map. Subtract 17066 from the non vertical values and you should start to see something that makes sense. You'll then likely have to negate one of the non vertical values in whatever coordinate 
                                      //system you're using to finally move it into place.
        public C3Vector rotation;            // degrees. This is not the same coordinate system orientation like the ADT itself! (see history.)
        ushort scale;               // 1024 is the default size equaling 1.0f.
        public ushort MDDFFlags;               // values from enum MDDFFlags.

        public uint MmidEntry { get => mmidEntry; set => mmidEntry = value; }
        public uint UniqueId { get => uniqueId; set => uniqueId = value; }
        public ushort Scale { get => scale; set => scale = value; }
    }


    [Flags]
    enum MLMDFlags
    {
        mlmd_destroyable = 1,         // set for destroyable buildings like the tower in DeathknightStart. This makes it a server-controllable game object.
        mlmd_use_lod = 2,             // WoD(?)+: also load _LOD1.WMO for use dependent on distance
        mlmd_unk_4 = 4,               // Legion(?)+: unknown
    }

    struct MLMDEntry
    {
        uint mwidEntry;           // references an entry in the MWID chunk, specifying the model to use.
        uint uniqueId;            // this ID should be unique for all ADTs currently loaded. Best, they are unique for the whole map.
        public C3Vector position;
        public C3Vector rotation;            // same as in MDDF.
        public ushort MLMDFlags;               // values from enum MODFFlags.
        ushort doodadSet;           // which WMO doodad set is used.
        ushort nameSet;             // which WMO name set is used. Used for renaming goldshire inn to northshire inn while using the same model.
        ushort unk;                 // Legion(?)+: has data finally!

        public uint MwidEntry { get => mwidEntry; set => mwidEntry = value; }
        public uint UniqueId { get => uniqueId; set => uniqueId = value; }
        public ushort DoodadSet { get => doodadSet; set => doodadSet = value; }
        public ushort NameSet { get => nameSet; set => nameSet = value; }
        public ushort Unk { get => unk; set => unk = value; }
    }

    struct header
    {
        string tag;
        uint version;
        uint misc;

        public string Tag { get => tag; set => tag = value; }
        public uint Version { get => version; set => version = value; }
        public uint Misc { get => misc; set => misc = value; }
    }


    struct MLFD
    {
        public string tag;
        public uint size;
        public uint[] m2LodOffset;  //Index into MLDD per lod
        public uint[] m2LodLength;  //Number of elements used from MLDD per lod
        public uint[] wmoLodOffset; //Index into MLMD per lod
        public uint[] wmoLodLength; //Number of elements used from MLMD per lod

    }
    //

    struct MMDX
    {
        string tag;
        uint strEndOffset;
        public List<string> fileNames;

        public string Tag { get => tag; set => tag = value; }
        public uint StrEndOffset { get => strEndOffset; set => strEndOffset = value; }
    }

    struct MMID
    {
        string tag;
        uint amt;
        uint[] ModelFilenameOffsets;

        public string Tag { get => tag; set => tag = value; }
        public uint Amt { get => amt; set => amt = value; }
        public uint[] ModelFilenameOffsets1 { get => ModelFilenameOffsets; set => ModelFilenameOffsets = value; }
    }

    struct MWMO
    {
        string tag;
        uint strEndOffset;

        public List<string> fileNames;

        public string Tag { get => tag; set => tag = value; }
        public uint StrEndOffset { get => strEndOffset; set => strEndOffset = value; }
    }


    struct MWID
    {
        string tag;
        uint amt;
        uint[] WMOFilenameOffsets;

        public string Tag { get => tag; set => tag = value; }
        public uint Amt { get => amt; set => amt = value; }
        public uint[] WMOFilenameOffsets1 { get => WMOFilenameOffsets; set => WMOFilenameOffsets = value; }
    }


    struct MLDD
    {
        string tag;
        uint endPos;
        public List<MLDDEntry> DoodadPlacementInfo;

        public string Tag { get => tag; set => tag = value; }
        public uint EndPos { get => endPos; set => endPos = value; }
    }

    struct CAaBoxAndRadius
    {
        public C3Vector min;
        public C3Vector max;
        float radius;

        public float Radius { get => radius; set => radius = value; }
    }

    struct MLDX
    {
        string tag;
        uint endOffset;

        public CAaBoxAndRadius[] boundings;

        public string Tag { get => tag; set => tag = value; }
        public uint EndOffset { get => endOffset; set => endOffset = value; }
    }



    struct MLDL
    {
        string tag;
        uint endPos;
        public List<uint> unk;

        public string Tag { get => tag; set => tag = value; }
        public uint EndPos { get => endPos; set => endPos = value; }
    }

    struct MLMD
    {
        string tag;
        uint endPos;

        public List<MLMDEntry> WMOPlacementInfo;

        public string Tag { get => tag; set => tag = value; }
        public uint EndPos { get => endPos; set => endPos = value; }
    }

    struct MLMX
    {
        string tag;
        uint endOffset;

        public List<CAaBoxAndRadius> lod_object_extents;

        public string Tag { get => tag; set => tag = value; }
        public uint EndOffset { get => endOffset; set => endOffset = value; }
    }


    public class Obj1
    {

        private static string readTag(BinaryReader br)
        {
            return new string(br.ReadChars(4));
        }

        private static string ReadNullTerminatedString(BinaryReader br)
        {
            string str = "";
            char ch;
            while ((int)(ch = br.ReadChar()) != 0)
                str = str + ch;
            return str;
        }

        header head;
        MLFD mlfd;
        MMDX mmdx;
        MMID mmid;
        MWMO mwmo;
        MWID mwid;
        MLDD mldd;
        MLDX mldx;
        MLDL mldl;
        MLMD mlmd;
        MLMX mlmx;

        public Obj1(string fileName)
        {
            var file = File.OpenRead(fileName);
            if (!file.CanRead)
                throw new Exception("Cannot open Obj1 File:" + fileName);

            BinaryReader br = new BinaryReader(file);

            // MVER
            head.Tag = readTag(br);
            head.Version = br.ReadUInt32();
            head.Misc = br.ReadUInt32();

            // MLFD if present
            string nextTag = readTag(br);

            if(nextTag == "DFLM")
            {
                mlfd.tag = nextTag;
                mlfd.size = br.ReadUInt32();

                mlfd.m2LodOffset = new uint[3];
                mlfd.m2LodLength = new uint[3];
                mlfd.wmoLodOffset = new uint[3];
                mlfd.wmoLodLength = new uint[3];

                mlfd.m2LodOffset[0] = br.ReadUInt32();
                mlfd.m2LodOffset[1] = br.ReadUInt32();
                mlfd.m2LodOffset[2] = br.ReadUInt32();

                mlfd.m2LodLength[0] = br.ReadUInt32();
                mlfd.m2LodLength[1] = br.ReadUInt32();
                mlfd.m2LodLength[2] = br.ReadUInt32();

                mlfd.wmoLodOffset[0] = br.ReadUInt32();
                mlfd.wmoLodOffset[1] = br.ReadUInt32();
                mlfd.wmoLodOffset[2] = br.ReadUInt32();

                mlfd.wmoLodLength[0] = br.ReadUInt32();
                mlfd.wmoLodLength[1] = br.ReadUInt32();
                mlfd.wmoLodLength[2] = br.ReadUInt32();
            }
            else
            {
                file.Seek(-4, SeekOrigin.Current);
            }

            mmdx.Tag = readTag(br);
            mmdx.StrEndOffset = br.ReadUInt32();
            var finPos = file.Position + mmdx.StrEndOffset;

            mmdx.fileNames = new List<string>();
            while (file.Position < finPos)
            {
                mmdx.fileNames.Add(ReadNullTerminatedString(br));
            }

            mmid.Tag = readTag(br);
            mmid.Amt = br.ReadUInt32();
            mmid.ModelFilenameOffsets1 = new uint[mmdx.fileNames.Count];

            for (int i = 0; i < mmdx.fileNames.Count; i++)
            {
                mmid.ModelFilenameOffsets1[i] = br.ReadUInt32();
            }

            mwmo.Tag = readTag(br);
            mwmo.StrEndOffset = br.ReadUInt32();

            finPos = file.Position + mwmo.StrEndOffset;

            mwmo.fileNames = new List<string>();
            while (file.Position < finPos)
            {
                mwmo.fileNames.Add(ReadNullTerminatedString(br));
            }

            mwid.Tag = readTag(br);
            mwid.Amt = br.ReadUInt32();
            mwid.WMOFilenameOffsets1 = new uint[mwmo.fileNames.Count];

            for (int i = 0; i < mwmo.fileNames.Count; i++)
            {
                mwid.WMOFilenameOffsets1[i] = br.ReadUInt32();
            }

            mldd.Tag = readTag(br);
            mldd.EndPos = br.ReadUInt32();

            finPos = file.Position + mldd.EndPos;

            mldd.DoodadPlacementInfo = new List<MLDDEntry>();
            while (file.Position < finPos)
            {
                MLDDEntry mLDDEntry = new MLDDEntry();
                mLDDEntry.MmidEntry = br.ReadUInt32();
                mLDDEntry.UniqueId = br.ReadUInt32();

                mLDDEntry.position.X = br.ReadSingle();
                mLDDEntry.position.Y = br.ReadSingle();
                mLDDEntry.position.Z = br.ReadSingle();

                mLDDEntry.rotation.X = br.ReadSingle();
                mLDDEntry.rotation.Y = br.ReadSingle();
                mLDDEntry.rotation.Z = br.ReadSingle();

                mLDDEntry.Scale = br.ReadUInt16();
                mLDDEntry.MDDFFlags = br.ReadUInt16();

                mldd.DoodadPlacementInfo.Add(mLDDEntry);
            }

            mldx.Tag = readTag(br);
            mldx.EndOffset = br.ReadUInt32();

            
            mldx.boundings = new CAaBoxAndRadius[mldd.DoodadPlacementInfo.Count];

            for (int i = 0; i < mldd.DoodadPlacementInfo.Count; i++)
            {
                var boxi = new CAaBoxAndRadius();

                boxi.min.X = br.ReadSingle();
                boxi.min.Y = br.ReadSingle();
                boxi.min.Z = br.ReadSingle();

                boxi.max.X = br.ReadSingle();
                boxi.max.Y = br.ReadSingle();
                boxi.max.Z = br.ReadSingle();

                boxi.Radius = br.ReadSingle();

                mldx.boundings[i] = boxi;
            }


            nextTag = readTag(br);

            if (nextTag == "LDLM")
            {
                mldl.Tag = nextTag;
                mldl.EndPos = br.ReadUInt32();

                finPos = file.Position + mldl.EndPos;

                mldl.unk = new List<uint>();
                while (file.Position < finPos)
                {
                    mldl.unk.Add(br.ReadUInt32());
                }
            }
            else
            {
                file.Seek(-4, SeekOrigin.Current);
            }

            mlmd.Tag = readTag(br);
            mlmd.EndPos = br.ReadUInt32();

            finPos = file.Position + mlmd.EndPos;

            mlmd.WMOPlacementInfo = new List<MLMDEntry>();
            while (file.Position < finPos)
            {
                var item = new MLMDEntry();

                item.MwidEntry = br.ReadUInt32();
                item.UniqueId = br.ReadUInt32();
                item.position.X = br.ReadSingle();
                item.position.Y = br.ReadSingle();
                item.position.Z = br.ReadSingle();

                item.rotation.X = br.ReadSingle();
                item.rotation.Y = br.ReadSingle();
                item.rotation.Z = br.ReadSingle();

                item.MLMDFlags = br.ReadUInt16();
                item.DoodadSet = br.ReadUInt16();
                item.NameSet = br.ReadUInt16();
                item.Unk = br.ReadUInt16();

                mlmd.WMOPlacementInfo.Add(item);
            }

            mlmx.Tag = readTag(br);
            mlmx.EndOffset = br.ReadUInt32();


            finPos = file.Position + mlmx.EndOffset;
            mlmx.lod_object_extents = new List<CAaBoxAndRadius>();
            while (file.Position < finPos)
            {
                var item = new CAaBoxAndRadius();

                item.min.X = br.ReadSingle();
                item.min.Y = br.ReadSingle();
                item.min.Z = br.ReadSingle();

                item.max.X = br.ReadSingle();
                item.max.Y = br.ReadSingle();
                item.max.Z = br.ReadSingle();

                item.Radius = br.ReadSingle();

                mlmx.lod_object_extents.Add(item);
            }
            
                // --
            }
    }
}
