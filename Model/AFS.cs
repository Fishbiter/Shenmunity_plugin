using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Shenmunity
{
    public class AFS
    {
        public IPAC.Entry[] m_entries;

        public AFS(BinaryReader r)
        {
            string magic = Encoding.ASCII.GetString(r.ReadBytes(4)).Trim('\0');
            if (magic != "AFS")
            {
                throw new DecoderFallbackException(string.Format("Expected AFS got {0}", magic));
            }

            uint num = r.ReadUInt32();

            m_entries = new IPAC.Entry[num];

            for (int i = 0; i < num; i++)
            {
                m_entries[i].m_offset = r.ReadUInt32();
                m_entries[i].m_length = r.ReadUInt32();
            }

            bool gotFilenames = false;

            if(m_entries[0].m_offset - r.BaseStream.Position >= 8)
            {
                r.BaseStream.Seek(m_entries[0].m_offset - 8, SeekOrigin.Begin);
                uint fileListOffset = r.ReadUInt32();
                r.BaseStream.Seek(fileListOffset, SeekOrigin.Begin);
                if (fileListOffset != 0)
                {
                    gotFilenames = true;
                    for (int i = 0; i < num; i++)
                    {
                        m_entries[i].m_filename = Encoding.ASCII.GetString(r.ReadBytes(32)).Trim('\0');

                        var sanitized = String.Join("_", m_entries[i].m_filename.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
                        if (sanitized != m_entries[i].m_filename)
                        {
                            Debug.LogFormat("Invalid characters in filename {0}", sanitized);
                            m_entries[i].m_filename = sanitized;
                        }

                        m_entries[i].m_ext = Path.GetExtension(m_entries[i].m_filename);
                        r.BaseStream.Seek(16, SeekOrigin.Current);//date/time etc.
                    }
                }
            }

            if (!gotFilenames)
            {
                for (int i = 0; i < num; i++)
                {
                    m_entries[i].m_filename = "File" + i.ToString("D3");
                    r.BaseStream.Seek(m_entries[i].m_offset, SeekOrigin.Begin);
                    m_entries[i].m_ext = Encoding.ASCII.GetString(r.ReadBytes(4)).Trim('\0');
                }
            }
        }
    }
}