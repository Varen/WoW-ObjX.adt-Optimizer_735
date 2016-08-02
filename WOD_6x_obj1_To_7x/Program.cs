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

            BinaryReader br = new BinaryReader(File.Open(args[0], FileMode.Open));
            int replPos = args[0].LastIndexOf("obj0");

            string newName = (args[0].Remove(replPos, 4)).Insert(replPos, "obj1");
            BinaryWriter bw = new BinaryWriter(File.OpenWrite(newName));

            // Write Header + MMDX Tag
            bw.Write(br.ReadBytes(16));

            // End Offset
            UInt32 strEndOffset = br.ReadUInt32();
            bw.Write(strEndOffset);

            UInt32 MMDXStringCount = 0;
            // MMDX: Write Strings
            while (br.BaseStream.Position < strEndOffset)
            {
                MMDXStringCount++;
                byte b = 50;
                while (b != 0x0)
                {
                    b = br.ReadByte();
                    bw.Write(b);
                }
            }

            // MMID Tag and Offset
            bw.Write(br.ReadBytes(8));

            // MMID Offsets
            for (int i = 0; i < MMDXStringCount; i++)
            {
                bw.Write(br.ReadUInt32());
            }


            //MWMO Tag
            bw.Write(br.ReadBytes(4));
            strEndOffset = br.ReadUInt32();
            bw.Write(strEndOffset);
            UInt32 MWMOStringCount = 0;
            // MMDX: Write Strings

            long myLongEndOffset = strEndOffset + br.BaseStream.Position;
            while (br.BaseStream.Position < myLongEndOffset)
            {
                MWMOStringCount++;
                byte b = 50;
                while (b != 0x0)
                {
                    b = br.ReadByte();
                    bw.Write(b);
                }
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
            br.ReadBytes(4);
            char[] DDLMtag = { 'D', 'D', 'L', 'M' };
            bw.Write(DDLMtag);

            strEndOffset = br.ReadUInt32();
            bw.Write(strEndOffset);
            UInt32 MLDDCount = 0;
            // MMDX: Write Strings

            myLongEndOffset = strEndOffset + br.BaseStream.Position;
            while (br.BaseStream.Position < myLongEndOffset)
            {
                MLDDCount++;
                bw.Write(br.ReadBytes(36)); // Write Whole Entry
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
                    // Bounding Box Min
                    tempBw.Write(float.MinValue);
                    tempBw.Write(float.MinValue);
                    tempBw.Write(float.MinValue);
                    // Bounding Box Max
                    tempBw.Write(float.MaxValue);
                    tempBw.Write(float.MaxValue);
                    tempBw.Write(float.MaxValue);
                    // Radius
                    tempBw.Write(float.MaxValue);
                }

                Console.WriteLine(tempBw.BaseStream.Position);
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


            UInt32 MLMDCount = 0;
            // MLMD -> Same as MODF, without the Bounding Boxes
            {
                br.ReadBytes(4);
                char[] MLMDtag = { 'D', 'M', 'L', 'M' };
                bw.Write(MLMDtag);


                strEndOffset = br.ReadUInt32();
                
                // Use offset to read all data, skipping Bounding Boxes. Save in MemoryStream.

                MemoryStream tempMs = new MemoryStream();
                BinaryWriter tempBw = new BinaryWriter(tempMs);

                myLongEndOffset = strEndOffset + br.BaseStream.Position;
                while (br.BaseStream.Position < myLongEndOffset)
                {
                    MLMDCount++;
                    tempBw.Write(br.ReadBytes(32)); // Write Start Of entry
                    br.ReadBytes(24); // Skip Bounding Boxes
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
                    tempBw.Write(float.MinValue);
                    tempBw.Write(float.MinValue);
                    tempBw.Write(float.MinValue);
                    // Bounding Box Max
                    tempBw.Write(float.MaxValue);
                    tempBw.Write(float.MaxValue);
                    tempBw.Write(float.MaxValue);
                    // Radius
                    tempBw.Write(float.MaxValue);
                }

                Console.WriteLine(tempBw.BaseStream.Position);
                bw.Write(Convert.ToInt32(tempBw.BaseStream.Position));
                long thePos = tempBw.BaseStream.Position;
                tempMs.Seek(0, SeekOrigin.Begin);
                bw.Write((new BinaryReader(tempMs).ReadBytes(Convert.ToInt32(thePos))));

                tempMs.Close();
            }

            // END
            br.Close();
            bw.Close();
            Console.WriteLine("Dun");
            Console.ReadLine();
        }
    }
}
