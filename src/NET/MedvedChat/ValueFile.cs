using System.IO;
using System.Text;

namespace MedvedChat
{
    class ValueFile
    {
        const string EXTENSION = ".yunotxt";
        string prefix;
        char crew;

        public ValueFile(string prefix = "", char crew = 'v')
        {
            this.prefix = prefix;
            this.crew = crew;
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
