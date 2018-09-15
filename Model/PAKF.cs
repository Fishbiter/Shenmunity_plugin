using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Shenmunity
{
    public class PAKF
    {
        public IPAC m_iPac;

        public struct TextureLocation
        {
            public string m_name;
            public long m_offset;
        }

        public List<TextureLocation> m_textureLocations = new List<TextureLocation>();

        public PAKF(BinaryReader r)
        {
            string magic = Encoding.ASCII.GetString(r.ReadBytes(4));
            if(magic != "PAKF")
            {
                throw new DecoderFallbackException(string.Format("Expected PAKF got {0}", magic));
            }
            
            uint pakfSize = r.ReadUInt32();
            uint c1 = r.ReadUInt32();
            uint numTextures = r.ReadUInt32();
            
            if (numTextures > 0)
            {
                do
                {
                    long blockStart = r.BaseStream.Position;
                    magic = Encoding.ASCII.GetString(r.ReadBytes(4));
                    long end = blockStart + r.ReadUInt32();
                    switch (magic)
                    {
                        case "DUMY":
                            break;
                        case "TEXN":
                            var texLoc = new TextureLocation();
                            uint number = r.ReadUInt32();
                            texLoc.m_name = Encoding.ASCII.GetString(r.ReadBytes(4)) + number;
                            texLoc.m_offset = blockStart + 8;
                            m_textureLocations.Add(texLoc);
                            break;

                    }
                    if (magic[0] == 0 || magic == "IPAC")
                    {
                        break;
                    }
                    r.BaseStream.Seek(end, SeekOrigin.Begin);
                }
                while (true);
            }
            r.BaseStream.Seek(pakfSize, SeekOrigin.Begin);
            m_iPac = new IPAC(r);
        }
    }
}