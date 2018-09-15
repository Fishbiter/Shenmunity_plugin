using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Shenmunity
{
    public class PAKS
    {
        public IPAC m_iPac;

        public PAKS(BinaryReader r)
        {
            string magic = Encoding.ASCII.GetString(r.ReadBytes(4));
            if (magic != "PAKS")
            {
                throw new DecoderFallbackException(string.Format("Expected PAKS got {0}", magic));
            }

            uint paksSize = r.ReadUInt32();
            uint c1 = r.ReadUInt32();
            uint c2 = r.ReadUInt32();

            m_iPac = new IPAC(r);
        }
    }
}