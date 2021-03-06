using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

using UnityEngine;

namespace Shenmunity
{
    public class TACReader
    {
        public static string s_shenmuePath;

        public enum FileType
        {
            TEXTURE,
            IDX,
            AFS,
            WAV,
            DXBC,
            PAKS,
            PAKF,
            MODEL,
            SND,
            PVR,
            PAWN,
            CHRT,
            SCN3,
            MOTN,
            ATTR,

            COUNT
        }

        static string[][] s_identifier = new string[(int)FileType.COUNT][]
        {
            new string[] { "DDS" }, //TEXTURE,
            new string[] { "IDX" }, //IDX,
            new string[] { "AFS" }, //AFS,
            new string[] { "RIFF" }, //WAV,
            new string[] { "DXBC" }, //DXBC
            new string[] { "PAKS" }, //PAKS
            new string[] { "PAKF" }, //PAKF
            new string[] { "MDP7", "MDC7", "HRCM", "CHRM", "MAPM",
                "MDOX", 
//                "MDLX",
 //               "MDCX",
 //               "MDPX"
            }, //MODEL,
            new string[] { "DTPK" },//SND
            new string[] { "GBIX", "TEXN" },//PVR
            new string[] { "PAWN" },//PAWN
            new string[] { "CHRT" },
            new string[] { "SCN3" },
            new string[] { "MOTN", " " },
            new string[] { "ATTR" },
        };

        public struct TextureEntry
        {
            public TACEntry m_file;
            public long m_postion;
        };

        static Dictionary<string, TextureEntry> s_textureLib = new Dictionary<string, TextureEntry>();
        static string s_textureNamespace = "";

        public class TACEntry
        {
            public string m_path;
            public string m_name;
            public string m_type;
            public string m_diskPath;
            public FileType m_fileType;
            public uint m_offset;
            public uint m_length;
            public bool m_duplicate;
            public TACEntry m_parent;

            public List<TACEntry> m_children;

            public string m_fullName
            {
                get
                {
                    string name = m_name;
                    var parent = m_parent;
                    while (parent != null)
                    {
                        name = parent.m_name + "/" + name;
                        parent = parent.m_parent;
                    }
                    return name;
                }
            }
        }

        static Dictionary<string, string> s_sources = new Dictionary<string, string>
        {
           { "Shenmue", "sm1/archives/dx11/data" },
          // { "Shenmue2", "sm2/archives/dx11/data" },
        };

        static string s_namesFile = "Assets/Plugins/Shenmunity/Names.txt";

        static Dictionary<string, Dictionary<string, TACEntry>> m_files;
        static Dictionary<FileType, List<TACEntry>> m_byType;
        static Dictionary<string, int> m_unknownTypes;
        static Dictionary<string, List<TACEntry>> m_modelToTAC = new Dictionary<string, List<TACEntry>>(StringComparer.InvariantCultureIgnoreCase);
        static Dictionary<TACEntry, Byte[]> m_gzipCache = new Dictionary<TACEntry, Byte[]>();


        static public List<TACEntry> GetFiles(FileType type)
        {
            GetFiles();

            if (!m_byType.ContainsKey(type))
            {
                m_byType[type] = new List<TACEntry>();
            }

            return m_byType[type];
        }

        static void FindShenmue()
        {
            var steamPath = (string)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null);
            if(string.IsNullOrEmpty(steamPath))
            {
                throw new FileNotFoundException("Couldn't find steam registry keys HKEY_CURRENT_USER\\Software\\Valve\\Steam\\SteamPath");
            }

            steamPath += "/" + "SteamApps";

            var libraryPaths = new List<string>();
            libraryPaths.Add(steamPath + "/common");

            var otherPathsFile = File.OpenText(steamPath + "/libraryfolders.vdf");
            string line;
            int libIndex = 1;
            while((line = otherPathsFile.ReadLine()) != null)
            {
                string[] param = line.Split('\t').Where(x => !string.IsNullOrEmpty(x)).ToArray();
                if(param[0] == "\"" + libIndex + "\"")
                {
                    libIndex++;
                    libraryPaths.Add(param[1].Replace("\"", "").Replace("\\\\", "/") + "/steamapps/common");
                }
            }

            foreach(var path in libraryPaths)
            {
                var smpath = path + "/" + "SMLaunch";
                if (Directory.Exists(smpath + "/" + s_sources.Values.First()))
                {
                    s_shenmuePath = smpath;
                    break;
                }
            }

            if(string.IsNullOrEmpty(s_shenmuePath))
            {
                throw new FileNotFoundException("Couldn't find shenmue installation in any steam library dir");
            }
        }

        static public TACEntry GetEntry(string path)
        {
            string[] p = path.Split('/');

            string tac = p[0] + "/" + p[1];

            if (GetFiles().ContainsKey(tac))
            {
                var tacContents = GetFiles()[tac];
                if(tacContents.ContainsKey(p[2]))
                {
                    return tacContents[p[2]];
                }
            }

            return null;
        }

        static public void SetTextureNamespace(string path)
        {
            s_textureNamespace = path;
        }

        static public void SaveNames()
        {
            using (var file = File.CreateText(s_namesFile))
            {
                foreach (var tac in GetFiles().Keys)
                {
                    foreach (var entry in m_files[tac].Values)
                    {
                        if (!string.IsNullOrEmpty(entry.m_name))
                        {
                            file.WriteLine(string.Format("{0} {1}", entry.m_path, entry.m_name));
                        }
                    }
                }
            }
        }

        static public void LoadNames()
        {
            if(!File.Exists(s_namesFile))
            {
                return;
            }
            using (var file = File.OpenText(s_namesFile))
            {
                var seen = new Dictionary<string, bool>();

                string line;
                while ((line = file.ReadLine()) != null)
                {
                    var ps = line.Split(new char[] { ' ' }, 2);
                    var e = GetEntry(ps[0]);
                    if (e != null)
                    {
                        e.m_name = ps[1];

                        //only set duplication for root entries
                        if (e.m_parent == null)
                        {
                            if (seen.ContainsKey(ps[1]))
                            {
                                SetDuplicate(e);
                            }
                            seen[ps[1]] = true;
                        }
                    }
                }
            }
        }

        static void SetDuplicate(TACEntry e)
        {
            e.m_duplicate = true;

            if (e.m_children == null)
                return;

            foreach(var c in e.m_children)
            {
                SetDuplicate(c);
            }
        }

        static public BinaryReader GetBytes(string path, out uint length, bool unzip = true)
        {
            GetFiles();

            return GetBytes(GetEntry(path), out length, unzip);
        }

        static BinaryReader GetBytes(TACEntry e, out uint length, bool unzip = true)
        {
            return new BinaryReader(new DebugStream(GetStream(e, out length, unzip)));
        }

        static Stream GetStream(TACEntry e, out uint length, bool unzip)
        { 
            if(e.m_parent != null)
            {
                var parent = GetStream(e.m_parent, out length, unzip);
                length = e.m_length;
                return new SubStream(parent, e.m_offset, e.m_length);
            }
            else
            {
                Stream stream = new FileStream(e.m_diskPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                stream = new SubStream(stream, e.m_offset, e.m_length);
                length = e.m_length;

                if (unzip)
                {
                    var header = new BinaryReader(stream).ReadBytes(2);
                    stream.Seek(-2, SeekOrigin.Current);

                    if (header[0] == 0x1f && header[1] == 0x8b)
                    {
                        byte[] bytes;

                        if (m_gzipCache.ContainsKey(e))
                        {
                            bytes = m_gzipCache[e];
                            length = (uint)bytes.Length;
                        }
                        else
                        {
                            stream.Seek(-4, SeekOrigin.End);
                            length = new BinaryReader(stream).ReadUInt32();
                            stream.Seek(0, SeekOrigin.Begin);

                            var gzip = new GZipStream(stream, CompressionMode.Decompress);
                            bytes = new byte[length];
                            gzip.Read(bytes, 0, (int)length);

                            m_gzipCache[e] = bytes;
                        }

                        stream = new MemoryStream(bytes);
                    }
                }

                return stream;
            }
        }


        static void BuildFiles()
        {
            FindShenmue();

            m_files = new Dictionary<string, Dictionary<string, TACEntry>>(StringComparer.InvariantCultureIgnoreCase);

            foreach(var s in s_sources)
            {
                BuildFilesInDirectory(s.Key, s.Value);
            }

            BuildTypes();

            LoadNames();
        }

        static void BuildFilesInDirectory(string shortForm, string dir)
        {
            string root = s_shenmuePath + "/" + dir;
            foreach (var fi in Directory.GetFiles(root))
            {
                if(fi.EndsWith(".tac"))
                {
                    if (fi.Contains("audio"))
                        continue;

                    BuildFilesInTAC(shortForm, fi.Replace("\\", "/"));
                }
            }
        }

        static void BuildFilesInTAC(string shortForm, string tac)
        {
            //Load tad file
            string tadFile = Path.ChangeExtension(tac, ".tad");

            string tacName = Path.GetFileNameWithoutExtension(tadFile);
            tacName = shortForm + "/" + tacName.Substring(0, tacName.IndexOf("_")); //remove hash (these change per release)

            if (!m_files.ContainsKey(tacName))
            {
                m_files[tacName] = new Dictionary<string, TACEntry>(StringComparer.InvariantCultureIgnoreCase); ;
            }
            var dir = m_files[tacName];
           
            using (BinaryReader reader = new BinaryReader(new FileStream(tadFile, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                //skip header
                reader.BaseStream.Seek(72, SeekOrigin.Current);

                while (true)
                {
                    var r = new TACEntry();
                    r.m_diskPath = tac;

                    reader.BaseStream.Seek(4, SeekOrigin.Current); //skip padding (at file begin)
                    r.m_offset = reader.ReadUInt32();
                    reader.BaseStream.Seek(4, SeekOrigin.Current); //skip padding
                    r.m_length = reader.ReadUInt32();
                    reader.BaseStream.Seek(4, SeekOrigin.Current); //skip padding
                    var hash = BitConverter.ToString(reader.ReadBytes(4)).Replace("-", "");
                    reader.BaseStream.Seek(8, SeekOrigin.Current); //skip padding

                    r.m_path = tacName + "/" + hash;

                    dir[hash] = r;

                    if (reader.BaseStream.Position >= reader.BaseStream.Length) break; //TODO: check the missing values at EOF
                }
            }
        }

        static Dictionary<string, Dictionary<string, TACEntry>> GetFiles()
        {
            if (m_files == null)
            {
                BuildFiles();
            }
            return m_files;
        }
        
        static public void ExtractFile(TACEntry entry, bool unzip = true)
        {
            uint len;
            using (var br = GetBytes(entry.m_path, out len, unzip))
            {
                var path = Directory.GetCurrentDirectory();
                path += "/" + entry.m_path + "." + String.Join("_", entry.m_type.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, br.ReadBytes((int)len));
            }
        }

        static void BuildTypes()
        {
            m_byType = new Dictionary<FileType, List<TACEntry>>();
            m_unknownTypes = new Dictionary<string, int>();

            foreach (var tac in m_files.Keys)
            {
                foreach (var e in m_files[tac].Values.ToArray())
                {
                    uint len;
                    using (var reader = GetBytes(e.m_path, out len))
                    {
                        e.m_type = reader.ReadAscii(4).Trim('\0');
                    }

                    AddEntryToType(tac, e.m_type, e);
                }
            }
        }

        static void AddEntryToType(string tac, string type, TACEntry e)
        {
            for (int i = 0; i < (int)FileType.COUNT; i++)
            {
                foreach (var id in s_identifier[i])
                {
                    if (string.Compare(id, 0, type, 0, id.Length) == 0)
                    {
                        GetFiles((FileType)i).Add(e);
                        e.m_fileType = (FileType)i;

                        //try
                        {
                            if (type == "PAKS")
                            {
                                uint len;
                                using (var br = GetBytes(e.m_path, out len))
                                {
                                    ReadPAKS(tac, e, br);
                                }
                            }
                            else if (type == "PAKF")
                            {
                                uint len;
                                using (var br = GetBytes(e.m_path, out len))
                                {
                                    ReadPAKF(tac, e, br);
                                }
                            }
                            else if (type == "AFS")
                            {
                                uint len;
                                using (var br = GetBytes(e.m_path, out len))
                                {
                                    ReadAFS(tac, e, br);
                                }
                            }
                        }
                        //catch(Exception exc)
                        //{
                        //    Debug.Log(exc.ToString());
                        //}

                        return;
                    }
                }
            }

            if (!m_unknownTypes.ContainsKey(type))
                m_unknownTypes[type] = 0;
            m_unknownTypes[type]++;
        }

        static void ReadPAKS(string tac, TACEntry parent, BinaryReader r)
        {
            var paks = new PAKS(r);

            if (paks.m_iPac != null)
            {
                AddIPAC(tac, parent, paks.m_iPac.m_entries);
            }
        }

        static void ReadPAKF(string tac, TACEntry parent, BinaryReader r)
        {
            var pakf = new PAKF(r);

            foreach(var tl in pakf.m_textureLocations)
            {
                var texEntry = new TextureEntry();
                texEntry.m_file = parent;
                texEntry.m_postion = tl.m_offset;

                s_textureLib[tl.m_name] = texEntry;
                s_textureLib[parent.m_path + tl.m_name] = texEntry;
            }

            if(pakf.m_iPac != null)
            {
                AddIPAC(tac, parent, pakf.m_iPac.m_entries);
            }
        }

        static void ReadAFS(string tac, TACEntry parent, BinaryReader r)
        {
            var afs = new AFS(r);

            AddIPAC(tac, parent, afs.m_entries);
        }

        static void AddIPAC(string tac, TACEntry parent, IPAC.Entry[] entries)
        {
            if (entries == null)
            {
                return;
            }

            var parentHash = parent.m_path.Split('/')[2];

            parent.m_children = new List<TACEntry>();

            foreach (var e in entries)
            {
                TACEntry newE = new TACEntry();

                newE.m_path = parent.m_path + "_" + e.m_filename;
                newE.m_name = e.m_filename + "." + e.m_ext;
                newE.m_offset = (uint)e.m_offset;
                newE.m_length = (uint)e.m_length;
                newE.m_parent = parent;
                newE.m_type = e.m_ext;

                string hash = parentHash + "_" + e.m_filename;
                int fnIndex = 1;

                while (m_files[tac].ContainsKey(hash))
                {
                    hash = parentHash + "_" + e.m_filename + fnIndex;
                    newE.m_path = parent.m_path + "_" + e.m_filename + fnIndex;
                    fnIndex++;
                }

                m_files[tac].Add(hash, newE);
                parent.m_children.Add(newE);
                AddEntryToType(tac, e.m_ext, newE);

                if (newE.m_fileType == FileType.MODEL)
                {
                    if (!m_modelToTAC.ContainsKey(e.m_filename))
                    {
                        m_modelToTAC[e.m_filename] = new List<TACEntry>();
                    }
                    m_modelToTAC[e.m_filename].Add(parent);
                }

                //ExtractFile(newE);
            }
        }

        static public BinaryReader GetTextureAddress(string name)
        {
            var e = s_textureLib.ContainsKey(s_textureNamespace + name) ? s_textureLib[s_textureNamespace + name] : s_textureLib[name];
            uint len = 0;
            var br = GetBytes(e.m_file.m_path, out len);
            br.BaseStream.Seek(e.m_postion, SeekOrigin.Current);
            return br;
        }

        //AFAICT pakf->paks joining is probably done by filename. Since we don't have filenames (curse you TAC) just find PAKS that contain the entities we're after...
        static public List<TACEntry> GetPAKSCandidates(IEnumerable<string> models)
        {
            List<TACEntry> candidates = null;
            foreach (var model in models)
            {
                if (!m_modelToTAC.ContainsKey(model))
                    continue;

                var list = m_modelToTAC[model];
                if (candidates == null)
                {
                    candidates = list.ToList();
                }
                else
                {
                    var oldList = candidates.ToList();
                    candidates.RemoveAll(x => !list.Contains(x));
                    if (candidates.Count == 0)
                    {
                        candidates = oldList; //if we're discarding our last chance... don't
                    }
                }
            }
            return candidates;
        }

        public static TACEntry GetAnyPAK(string model)
        {
            if(m_modelToTAC.ContainsKey(model))
            {
                return m_modelToTAC[model][0];
            }
            return null;
        }
    }
}