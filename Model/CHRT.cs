using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Shenmunity
{
    public class CHRTNode
    {
        public CHRTNode Clone()
        {
            return (CHRTNode)MemberwiseClone();
        }

        public string m_id;
        public string m_model;
        public string m_image;

        public Vector3 m_position;
        public Vector3 m_eulerAngles;
    };

    public class CHRT
    {
        BinaryReader m_reader;

        public List<CHRTNode> m_nodes = new List<CHRTNode>();
        public List<CHRTNode> m_defImage = new List<CHRTNode>();

        public CHRT(BinaryReader reader)
        {
            m_reader = reader;

            var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (magic != "CHRS")
            {
                throw new DecoderFallbackException(string.Format("Expected CHRS got {0}", magic));
            }

            long endBlock = reader.BaseStream.Position + reader.ReadUInt32() - 4;

            CHRTNode node = null;

            using (var outS = File.CreateText("CHRT"))
            {
                while (reader.BaseStream.Position < endBlock)
                {
                    var prop = m_reader.ReadOffsetString();
                    switch (prop.ToUpper())
                    {
                        case "DEFIMAGE":
                            m_reader.Expect(35, outS);
                            string id = m_reader.ReadOffsetString();
                            uint unknown = m_reader.ReadUInt32();
                            outS.WriteLine(string.Format("{0}: {1} {2}", prop, id, unknown));

                            node = new CHRTNode();
                            node.m_id = id;
                            m_defImage.Add(node);

                            break;
                        case "IMAGE":
                            uint i1 = m_reader.ReadUInt32();
                            if(i1 == 25) //Model?
                            {
                                float i2 = m_reader.ReadSingle();
                                string model = m_reader.ReadOffsetString();
                                outS.WriteLine(string.Format("{0}: {1} {2} {3}", prop, i1, i2, model));
                                model = model.TrimStart(new char[] { '$', '@' });
                                node.m_model = Path.GetFileNameWithoutExtension(model);
                            }
                            else if(i1 == 3)
                            {
                                string i2 = m_reader.ReadOffsetString();
                                outS.WriteLine(string.Format("{0}: {1} {2}", prop, i1, i2));

                                foreach(var n in m_defImage)
                                {
                                    if(n.m_id == i2)
                                    {
                                        node.m_model = n.m_model;
                                        node.m_image = n.m_id;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                Debug.LogFormat("Unexpected Image type {0}", i1);
                            }

                            break;
                        case "CHARACTER":
                            m_reader.Expect(34, outS);
                            string ii = m_reader.ReadOffsetString();
                            uint ii2 = m_reader.ReadUInt32();
                            outS.WriteLine(string.Format("{0}: {1} {2}", prop, ii, ii2));
                            node = new CHRTNode();
                            node.m_id = ii;
                            m_nodes.Add(node);
                            break;
                        case "POSITION":
                            uint unk = m_reader.ReadUInt32(); //buffer?
                            float x = m_reader.ReadSingle();
                            float y = m_reader.ReadSingle();
                            float z = m_reader.ReadSingle();
                            node.m_position = new Vector3(x * ShenmueModel.SHENMUE_FLIP, y, z);
                            outS.WriteLine(string.Format("{0}: {1}, {2}, {3}", prop, x, y, z));
                            break;
                        case "ANGLE":
                            float type = m_reader.ReadUInt32();
                            float rotx = 0, roty, rotz = 0;
                            if (type == 1)
                            {
                                roty = m_reader.ReadSingle();
                            }
                            else
                            {
                                rotx = m_reader.ReadSingle();
                                roty = m_reader.ReadSingle();
                                rotz = m_reader.ReadSingle();
                            }
                            node.m_eulerAngles = new Vector3(rotx, roty * ShenmueModel.SHENMUE_FLIP, rotz * ShenmueModel.SHENMUE_FLIP);
                            outS.WriteLine(string.Format("{0}: {1}, {2}", prop, rotx, roty, rotz));
                            break;
                        case "SCALE":
                            float scx = m_reader.ReadSingle();
                            float scy = m_reader.ReadSingle();
                            float scz = m_reader.ReadSingle();
                            float scw = m_reader.ReadSingle();
                            outS.WriteLine(string.Format("{0}: {1}, {2}, {3}, {4}", prop, scx, scy, scz, scw));
                            break;
                        case "SIZE":
                            float sizex = m_reader.ReadSingle();
                            float sizey = m_reader.ReadSingle();
                            float sizez = m_reader.ReadSingle();
                            outS.WriteLine(string.Format("{0}: {1}, {2}", prop, sizex, sizey, sizez));
                            break;
                        case "HEIGHT":
                            float hgtx = m_reader.ReadSingle();
                            float hgty = m_reader.ReadSingle();
                            float hgtz = m_reader.ReadSingle();
                            uint hgtp1 = m_reader.ReadUInt32();
                            string hgtp2 = m_reader.ReadOffsetString();
                            uint hgtp3 = m_reader.ReadUInt32();
                            outS.WriteLine(string.Format("{0}: {1}, {2}, {3}, {4}, {5}, {6}", prop, hgtx, hgty, hgtz, hgtp1, hgtp2, hgtp3));
                            break;
                        case "RANGE":
                            uint start = m_reader.ReadUInt32();
                            uint end = m_reader.ReadUInt32();
                            outS.WriteLine(string.Format("{0}: {1}, {2}", prop, start, end));
                            break;
                        case "OBJECT":
                            uint obj = m_reader.ReadUInt32();
                            outS.WriteLine(string.Format("{0}: {1}", prop, obj));
                            break;
                        case "ADJUST":
                            uint adj = m_reader.ReadUInt32();
                            string adj2 = m_reader.ReadOffsetString();
                            outS.WriteLine(string.Format("{0}: {1} {2}", prop, adj, adj2));
                            break;
                        case "DISP":
                            outS.WriteLine(string.Format("{0}", prop));
                            break;
                        case "SLEEP":
                            outS.WriteLine(string.Format("{0}", prop));
                            break;
                        case "FLAGS":
                            uint flags = m_reader.ReadUInt32();
                            outS.WriteLine(string.Format("{0}: {1}", prop, flags));
                            break;
                        case "SHADOWOFF":
                            uint shadowOff = m_reader.ReadUInt32();
                            outS.WriteLine(string.Format("{0}: {1}", prop, shadowOff));
                            break;
                        case "SHADOW":
                            uint shadow = m_reader.ReadUInt32();
                            uint shadow2 = m_reader.ReadUInt32();
                            outS.WriteLine(string.Format("{0}: {1} {2}", prop, shadow, shadow2));
                            break;
                        case "COLIOFF":
                            uint coliOff = m_reader.ReadUInt32();
                            outS.WriteLine(string.Format("{0}: {1}", prop, coliOff));
                            break;
                        case "COLI":
                            uint coli = m_reader.ReadUInt32();
                            uint coli2 = m_reader.ReadUInt32();
                            outS.WriteLine(string.Format("{0}: {1} {2}", prop, coli, coli2));
                            break;

                        default:
                            outS.WriteLine(string.Format("Unknown property {0}", prop));
                            break;
                    }
                }
            }
        }

        public IEnumerable<string> GetModelNames()
        {
            return m_defImage.Select(x => x.m_model);
        }
    }
}