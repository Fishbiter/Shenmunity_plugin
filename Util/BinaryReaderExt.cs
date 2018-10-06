using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Shenmunity
{
    public static class BinaryReaderExt
    {
        public static float ReadHalf(this BinaryReader br)
        {
            uint v = br.ReadUInt16();

            float sign = (v & 0x8000) != 0 ? -1 : 1;
            int exp = (((int)v >> 10) & 0x1f) - 15;
            return sign * (float)Math.Pow(2, exp) * (1.0f + (float)(v & 0x1ff) / 0x200);
        }

        public static string ReadOffsetString(this BinaryReader br)
        {
            long pos = br.BaseStream.Position;
            long ofs = br.ReadUInt32();

            if ((ofs & 0xff000000) != 0) //probably in-place string
            {
                br.BaseStream.Seek(-4, SeekOrigin.Current);
                return br.ReadAscii(4);
            }

            br.BaseStream.Seek(ofs - 4, SeekOrigin.Current);

            var str = br.ReadZeroTerminatedString();

            br.BaseStream.Seek(pos + 4, SeekOrigin.Begin);
            return str;
        }

        public static string ReadAbsOffsetString(this BinaryReader br)
        {
            long pos = br.BaseStream.Position;
            long ofs = br.ReadUInt32();

            br.BaseStream.Seek(ofs, SeekOrigin.Begin);

            var str = br.ReadZeroTerminatedString();

            br.BaseStream.Seek(pos + 4, SeekOrigin.Begin);
            return str;
        }

        public static string ReadZeroTerminatedString(this BinaryReader br)
        {
            var bytes = new List<byte>();
            do
            {
                var b = br.ReadByte();
                if (b == 0)
                    break;
                bytes.Add(b);
            }
            while (true);

            return Encoding.ASCII.GetString(bytes.ToArray());
        }

        public static string ReadAscii(this BinaryReader br, int numCharacters)
        {
            return Encoding.ASCII.GetString(br.ReadBytes(numCharacters));
        }

        static public void Expect(this BinaryReader br, uint val, StreamWriter outS = null)
        {
            uint v = br.ReadUInt32();
            if (v != val)
            {
                if (outS != null)
                {
                    outS.WriteLine(string.Format("Unexpected value {0} - usually {1}", v, val));
                }
            }
        }
    }
}