using System.IO;
using System.Text;

namespace Shenmunity
{
    public class IPAC
    {
        public struct Entry
        {
            public string m_filename;
            public string m_ext;
            public long m_offset;
            public long m_length;
        }

        public Entry[] m_entries;

        public IPAC(BinaryReader r)
        {
            long basePos = r.BaseStream.Position;

            string magic = Encoding.ASCII.GetString(r.ReadBytes(4));
            if (magic != "IPAC")
            {
                //throw new DecoderFallbackException(string.Format("Expected IPAC got {0}", magic));
                return;
            }
            uint size1 = r.ReadUInt32();
            uint num = r.ReadUInt32();
            uint size2 = r.ReadUInt32();

            r.BaseStream.Seek(size1 - 16, SeekOrigin.Current);

            m_entries = new Entry[num];

            for (int i = 0; i < num; i++)
            {
                m_entries[i].m_filename = Encoding.ASCII.GetString(r.ReadBytes(8)).Trim('\0');
                m_entries[i].m_ext = Encoding.ASCII.GetString(r.ReadBytes(4)).Trim('\0');
                m_entries[i].m_offset = r.ReadUInt32() + basePos;
                m_entries[i].m_length = r.ReadUInt32();
            }
        }
    }
}