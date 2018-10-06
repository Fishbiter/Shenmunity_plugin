using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Shenmunity
{
    public class MOTN
    {
        public struct KeyFrame
        {
            public uint m_frame;
            public float m_value0;
            public float m_value1;
            public float m_value2;
        }

        public struct Sequence
        {
            public string m_name;
            public uint m_address;
            public uint m_addressExtraData;
            public uint[] m_extraData;
            public uint m_flags;

            public KeyFrame[][] m_keyFrames;
            public byte[][] m_valueRead;

            public List<uint> m_boneIds;

            public float[] GetPose(float t)
            {
                float[] pose = new float[(m_boneIds.Count + 1) * 3];

                for(int i = 0; i < (m_boneIds.Count + 1) * 3; i++)
                {
                    pose[i] = GetPose(t, m_keyFrames[i]);
                }

                return pose;
            }

            float GetPose(float t, KeyFrame[] frames)
            {
                int frame = 0;
                for(; frame < frames.Length - 2; frame++)
                {
                    if(t <= frames[frame+1].m_frame)
                    {
                        break;
                    }
                }

                float frac = (t - frames[frame].m_frame) / (frames[frame + 1].m_frame - frames[frame].m_frame);
                float ret = Mathf.Lerp(frames[frame].m_value0, frames[frame + 1].m_value0, frac);
                ret += Mathf.Lerp(frames[frame].m_value1, frames[frame + 1].m_value1, frac);
                ret += Mathf.Lerp(frames[frame].m_value2, frames[frame + 1].m_value2, frac);
                return ret;
            }
        }

        public Sequence[] m_sequences;
        StreamWriter m_out;
        Dictionary<float, int> m_valFreq = new Dictionary<float, int>();

        public MOTN(BinaryReader br)
        {
            long motionDataIndexAddr = br.ReadUInt32();
            long stringTableAddr = br.ReadUInt32();
            long motionDataAddr = br.ReadUInt32();
            int numAnimations = br.ReadByte() - 1;
            byte[] unknown1 = br.ReadBytes(3);
            long size = br.ReadUInt32();

            m_sequences = new Sequence[numAnimations];

            ReadStringTable(br, stringTableAddr);

            m_out = File.CreateText("MOTN");

            ReadMotionDataIndex(br, motionDataIndexAddr, motionDataAddr);

            foreach (var seq in m_sequences)
            {
                m_out.WriteLine("Seq: {0} Duration:{1}", seq.m_name, (float)seq.m_flags / 30);
            }

            foreach (var v in m_valFreq.Keys.OrderBy(x => x))
            {
                m_out.WriteLine("{0}: {1}", v.ToString(), new string('*', m_valFreq[v]));
            }

            m_out.Close();
        }

        void ReadStringTable(BinaryReader br, long address)
        {
            br.BaseStream.Seek(address, SeekOrigin.Begin);

            for (int i = 0; i < m_sequences.Length; i++)
            {
                m_sequences[i].m_name = br.ReadAbsOffsetString();
            }
        }

        void ReadMotionDataIndex(BinaryReader br, long indexAddress, long motionDataAddr)
        {
            br.BaseStream.Seek(indexAddress, SeekOrigin.Begin);

            for (int i = 0; i < m_sequences.Length; i++)
            {
                m_sequences[i].m_address = br.ReadUInt32();
                m_sequences[i].m_addressExtraData = br.ReadUInt32();
            }

            for (int i = 0; i < m_sequences.Length; i++)
            {
                br.BaseStream.Seek((long)m_sequences[i].m_addressExtraData, SeekOrigin.Begin);
                m_sequences[i].m_extraData = new uint[4];
                for (int d = 0; d < 4; d++)
                {
                    m_sequences[i].m_extraData[d] = br.ReadUInt32();
                }
            }

            Array.Sort(m_sequences, (x, y) => (int)x.m_address - (int)y.m_address);

            for (int i = 0; i < m_sequences.Length; i++)
            {
                br.BaseStream.Seek(motionDataAddr + m_sequences[i].m_address, SeekOrigin.Begin);

                ReadMotionData(br, (i < m_sequences.Length - 1 ? m_sequences[i + 1].m_address : (uint)br.BaseStream.Length) - m_sequences[i].m_address, ref m_sequences[i]);
            }
        }

        void ReadMotionData(BinaryReader br, uint seqlen, ref Sequence seq)
        {
            long start = br.BaseStream.Position;
            seq.m_flags = br.ReadUInt16(); //suspect flags is length + some extra bytes

            m_out.WriteLine("Seq: {0} Ofs:{1} Len:{2} flags:{3:X}", seq.m_name, br.BaseStream.Position, seqlen, seq.m_flags);
            m_out.WriteLine("\tExtraData {0}", string.Join(" ", seq.m_extraData.Select(x => x.ToString()).ToArray()));

            //5 2-byte offsets relative to start of this motion data
            uint[] block = new uint[5];
            for (int i = 0; i < 5; i++)
            {
                block[i] = br.ReadUInt16();
            }

            //Now a zero-terminated list of bones... probably. 
            //I don't know what these IDs mean. Look likely to be an index and a flags field or type. 
            //They don't seem to match up with the actual bone IDs on the rigs.
            seq.m_boneIds = new List<uint>();
            do
            {
                uint bone = br.ReadUInt16();
                if (bone == 0)
                    break;
                seq.m_boneIds.Add(bone);
            }
            while (true);

            m_out.WriteLine("\tBones ({0}) {1}", seq.m_boneIds.Count, string.Join(" ", seq.m_boneIds.Select(x => x.ToString("X")).ToArray()));

            //Dump raw blocks for debugging
            for (int i = 1; i < 5; i++)
            {
                br.BaseStream.Seek(start + block[i], SeekOrigin.Begin);

                int len = (i == 4 ? (int)seqlen : (int)block[i + 1]) - (int)block[i];

                if (len > 0)
                {
                    var bytes = br.ReadBytes(len);

                    m_out.WriteLine("\tBlock {0} ({1}) (total:{2} Min:{3} Max: {4}): {5}", i, len, bytes.Sum(x => (int)x), bytes.Min(), bytes.Max(), BitConverter.ToString(bytes));
                }
                else
                {
                    m_out.WriteLine("\tSkipped because len was {0}", len);
                }
            }

            //block 1 is a set of counts, 3 for each bone (plus one extra bone... maybe root motion?)
            //The counts are variable width based on flags field
            //I think these are likely frame counts
            int bytesPerPropCount = seq.m_flags < 0x1000 ? 1 : 2;
    
            int boneProps = (seq.m_boneIds.Count + 1) * 3;
            seq.m_keyFrames = new KeyFrame[boneProps][];
            br.BaseStream.Seek(start + block[1], SeekOrigin.Begin);
            for (int i = 0; i < boneProps; i++)
            {
                seq.m_keyFrames[i] = new KeyFrame[(bytesPerPropCount == 1 ? br.ReadByte() : br.ReadUInt16()) + 2]; //we seem to get 2 extra values in block 2/3 (likely start/end)
                seq.m_keyFrames[i][seq.m_keyFrames[i].Length - 1].m_frame = seq.m_flags;
            }

            //block 2 contains a number for each "frame" above. These are ascending order and look likely to be frame time stamps
            int bytesPerProp = seq.m_flags < 0x100 ? 1 : 2;
            br.BaseStream.Seek(start + block[2], SeekOrigin.Begin);

            for (int i = 0; i < boneProps; i++)
            {
                for (int t = 1; t < seq.m_keyFrames[i].Length-1; t++)
                {
                    seq.m_keyFrames[i][t].m_frame = bytesPerProp == 1 ? br.ReadByte() : br.ReadUInt16();
                }
            }

            //block 3 is a set of bit fields per frame from block 1
            //Seems to be 2 bits per frame
            br.BaseStream.Seek(start + block[3], SeekOrigin.Begin);

            ReadValueBits(br, boneProps, ref seq);

            //block 4 seems to be 2-byte values, where 0, 1, 2 or 3 values are read based on the flags in block 3
            //My best guess is the numbers are half precision floats
            //I think this is probably a curve (hence muliple values per frame)
            br.BaseStream.Seek(start + block[4], SeekOrigin.Begin);

            int bitsOn = 0;

            for (int i = 0; i < boneProps; i++)
            {
                var read = seq.m_valueRead[i];

                int oldBits = bitsOn;

                foreach(var r in read)
                {
                    for(int b = 0; b < 8; b++)
                    {
                        if((r & (1 << b)) != 0)
                        {
                            bitsOn++;
                        }
                    }
                }

                if(oldBits == bitsOn)
                {
                    continue;
                }

                for (int t = 0; t < seq.m_keyFrames[i].Length; t++)
                {
                    int byteIndex = t / 4;
                    int bitIndex = 3 - (t % 4);

                    byte b = read[byteIndex];
                    b >>= bitIndex * 2;

                    int values = b & 0x3;
                    if (values > 0)
                    {
                        seq.m_keyFrames[i][t].m_value0 = GetVal(br);
                    }
                    if (values > 1)
                    {
                        seq.m_keyFrames[i][t].m_value1 = GetVal(br);
                    }
                    if (values > 2)
                    {
                        seq.m_keyFrames[i][t].m_value2 = GetVal(br);
                    }

                }
            }

            m_out.WriteLine("Read {0} bytes of values for {1} bits", br.BaseStream.Position - start - block[4], bitsOn);

            for (int i = 0; i < boneProps; i++)
            {
                int bone = i / 3;
                int track = i % 3;
                string boneId = i >= 3 ? seq.m_boneIds[i/3 - 1].ToString("X") : "";

                m_out.WriteLine("\t\tBone Props {0} ({1}) {2} Value 0 [{3}]: {4}", bone, boneId, track,
                    BitConverter.ToString(seq.m_valueRead[i]),
                    string.Join(" ", seq.m_keyFrames[i].Select(x => x.m_frame.ToString() + ":" + x.m_value0.ToString("N2")).ToArray()));
                m_out.WriteLine("\t\tBone Props {0} ({1}) {2} Value 1 [{3}]: {4}", bone, boneId, track,
                    BitConverter.ToString(seq.m_valueRead[i]),
                    string.Join(" ", seq.m_keyFrames[i].Select(x => x.m_frame.ToString() + ":" + x.m_value1.ToString("N2")).ToArray()));
                m_out.WriteLine("\t\tBone Props {0} ({1}) {2} Value 2 [{3}]: {4}\n", bone, boneId, track,
                    BitConverter.ToString(seq.m_valueRead[i]),
                    string.Join(" ", seq.m_keyFrames[i].Select(x => x.m_frame.ToString() + ":" + x.m_value2.ToString("N2")).ToArray()));
            }
        }

        float GetVal(BinaryReader br)
        {
            float v = br.ReadHalf();
            if(!m_valFreq.ContainsKey(v))
            {
                m_valFreq[v] = 1;
            }
            else
            {
                m_valFreq[v]++;
            }
            return v;
        }

        void ReadValueBits(BinaryReader br, int numTracks, ref Sequence seq)
        {
            long start = br.BaseStream.Position;
            seq.m_valueRead = new byte[numTracks][];

            for (int i = 0; i < numTracks; i++)
            {
                int numBytes = Math.Max(1, (seq.m_keyFrames[i].Length * 2 + 7) / 8);

                var bytes = br.ReadBytes(numBytes);
                seq.m_valueRead[i] = bytes.ToArray();
            }
        }
    }
}