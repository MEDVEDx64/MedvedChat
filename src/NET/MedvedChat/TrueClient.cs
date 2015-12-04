using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace MedvedChat
{
    class TrueClient
    {
        Thread lsThread = null;
        Socket socket = null;

        public void Connect(string addr)
        {
            if (lsThread != null) Disconnect();
            CreateSocket(addr);
            lsThread = new Thread(ListeningThread);
            lsThread.IsBackground = true;
            lsThread.Start();
        }

        void Disconnect()
        {
            if (lsThread != null)
            {
                try
                {
                    socket.Disconnect(false);
                    socket.Dispose();
                }
                catch (Exception) { }
            }
            UserListMessageAccepted(new string[0]);
        }

        public bool IsConnected
        {
            get { return (socket == null ? false : socket.Connected); }
        }

        public void SendMessage(string text, string recipient = null)
        {
            SendMessageRaw(text);
        }

        public void SendLogin(string nick)
        {
            SendMessageRaw(nick, OutcomingTypes.Login);
        }

        public void SendUserListRequest()
        {
            try
            {
                SendMessageRaw(null, OutcomingTypes.UserlistRequest);
            }
            catch (Exception) { }
        }

        private void SendMessageRaw(string text, OutcomingTypes type = OutcomingTypes.Message)
        {
            if (socket == null || !socket.Connected) return;

            var bytes = (text == null ? null : Encoding.UTF8.GetBytes(text));
            int len = (text == null ? 0 : (bytes.Length > 0x3fff ? 0x3fff : bytes.Length));
            byte[] buffer = new byte[len + 3];

            buffer[0] = (byte)(((int)type << 1) | 1);
            buffer[1] = (byte)(len >> 8);
            buffer[2] = (byte)len;
            if(text != null) Array.Copy(bytes, 0, buffer, 3, len);

            socket.Send(buffer);
        }

        public delegate void LocalMessageHandler(string text);
        public delegate void ServerMessageHandler(string text, string sender = null);
        public delegate void UserListMessageHandler(string[] users);

        public event LocalMessageHandler Log;
        public event ServerMessageHandler TextMessageAccepted;
        public event ServerMessageHandler NotificationAccepted;
        public event UserListMessageHandler UserListMessageAccepted;

        enum OutcomingTypes
        {
            Message = 0,
            Login = 1,
            PrivateMessage = 2,
            UserlistRequest = 3
        }

        enum IncomingTypes
        {
            Notification = 32,
            Message = 33,
            UserlistResponse = 34
        }

        private void CreateSocket(string addr)
        {
            string ad = "127.0.0.1";
            ushort port = 11070;
            try
            {
                var s = addr.Split(':');
                ad = s[0];
                port = Convert.ToUInt16(s[1]);
            }
            catch(Exception) { }

            Log("Подключение к серверу " + ad + ":" + port);

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPAddress ip = null;
            string[] blocks = ad.Split('.');
            if (blocks.Length == 4)
            {
                bool valid = true;
                byte[] addressCandidate = new byte[4];
                for (int i = 0; i < blocks.Length; i++)
                {
                    if (!valid) break;
                    foreach (char c in blocks[i])
                    {
                        if (c < '0' || c > '9')
                        {
                            valid = false;
                            break;
                        }
                    }

                    addressCandidate[i] = Convert.ToByte(blocks[i]);
                }

                if (valid) ip = new IPAddress(addressCandidate);
            }

            if (ip == null) ip = Dns.GetHostEntry(ad).AddressList[0];
            try
            {
                socket.Connect(new IPEndPoint(ip, port));
            }
            catch(Exception e)
            {
                Log("Сбой подключения: " + e.Message);
            }
        }

        private void ListeningThread()
        {
            try
            {
                byte[] buffer = new byte[0x4003];

                while (true)
                {
                    int command = 0, payload = 0, pl = 0;

                    if (socket.Receive(buffer, 3, SocketFlags.None) <= 0) break;
                    command = (buffer[0] & 254) >> 1;
                    payload = buffer[0] & 1;
                    if (payload == 0) continue;
                    pl = (buffer[1] << 8) | buffer[2];

                    int read = 0;
                    while (read < pl)
                    {
                        var l = socket.Receive(buffer, read + 3, pl - read, SocketFlags.None);
                        if (l <= 0) break;
                        read += l;
                    }

                    string text = Encoding.UTF8.GetString(buffer, 3, pl);
                    var c = (IncomingTypes)command;

                    if (c == IncomingTypes.Message)
                    {
                        string sender = null;
                        string msg = text;
                        int idx = text.IndexOf('\n');
                        if (idx > 0)
                        {
                            sender = text.Substring(0, idx);
                            msg = text.Substring(idx + 1);
                        }

                        TextMessageAccepted(msg, sender);
                    }

                    else if (c == IncomingTypes.Notification) NotificationAccepted(text);
                    else if (c == IncomingTypes.UserlistResponse)
                    {
                        List<string> users = new List<string>();
                        foreach (string u in text.Split('\n'))
                        {
                            if (u == null || u.Length == 0) continue;
                            users.Add(u);
                        }
                        UserListMessageAccepted(users.ToArray());
                    }
                }
            }

            catch(Exception e)
            {
                Console.WriteLine(e);
            }

            finally
            {
                Log("Отключён.");
                lsThread = null;
            }
        }
    }
}
