using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace MedvedChat
{
    class ValueFile
    {
        const string EXTENSION = ".yunotxt";
        const string lkFileBase = "lock";
        string lkFile;

        string prefix;
        char crew;
        bool locked = false;

        public ValueFile(string prefix = "MedvedChat.var\\", char crew = 'v')
        {
            this.prefix = prefix;
            this.crew = crew;
            Directory.CreateDirectory(prefix);
            lkFile = crew + "_"  + lkFileBase;

            if (File.Exists(prefix + lkFile))
            {
                int pid = 0;
                using (var f = new BinaryReader(new FileStream(prefix + lkFile, FileMode.Open)))
                {
                    pid = f.ReadInt32();
                }
                if (Process.GetProcesses().Any(x => x.Id == pid)) locked = true;
                else WriteLockFile();
            }

            else WriteLockFile();
        }

        private void WriteLockFile()
        {
            using (var f = new BinaryWriter(new FileStream(prefix + lkFile, FileMode.Create)))
            {
                f.Write(Process.GetCurrentProcess().Id);
            }
        }

        public void DestroyLockFile()
        {
            if (!locked && File.Exists(prefix + lkFile)) File.Delete(prefix + lkFile);
        }

        private string MakePath(string valueName)
        {
            return prefix + crew + '_' + valueName + EXTENSION;
        }

        public string Read(string valueName)
        {
            try
            {
                using (FileStream f = new FileStream(MakePath(valueName), FileMode.Open))
                {
                    if (f.ReadByte() != 0) return null;
                    var b = f.ReadByte();
                    if (b < 0) return null;
                    byte csum = (byte)b;

                    MemoryStream ms = new MemoryStream();
                    while (true)
                    {
                        b = f.ReadByte();
                        if (b < 0)
                        {
                            if (csum == 0) return Encoding.UTF8.GetString(ms.ToArray());
                            return null;
                        }

                        ms.WriteByte((byte)b);
                        csum -= (byte)b;
                    }
                }
            }

            catch(IOException)
            {
                return null;
            }
        }

        public bool Write(string valueName, string value)
        {
            if (locked) return false;

            try
            {
                using (FileStream f = new FileStream(MakePath(valueName), FileMode.Create))
                {
                    f.WriteByte(0);
                    f.WriteByte(0);
                    byte csum = 0;

                    foreach(byte b in Encoding.UTF8.GetBytes(value))
                    {
                        f.WriteByte(b);
                        csum += b;
                    }

                    f.Seek(1, SeekOrigin.Begin);
                    f.WriteByte(csum);
                    return true;
                }
            }

            catch(IOException)
            {
                return false;
            }
        }
    }
}
