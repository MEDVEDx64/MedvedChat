using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace TrueServer
{
    class TrueServer
    {
        static void Main(string[] args)
        {
            new TrueServer();
        }

        List<SrvUser> users = new List<SrvUser>();

        public TrueServer()
        {
            const ushort PORT = 11070;
            var localEP = new IPEndPoint(new IPAddress(0), PORT);
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Bind(localEP);
                socket.Listen(10);

                Thread daemon = new Thread(UserListDeamon);
                daemon.IsBackground = true;
                daemon.Start();

                while (true)
                {
                    var s = socket.Accept();
                    Console.WriteLine("Incoming connection " + s.RemoteEndPoint);
                    users.Add(new SrvUser(s, users));
                }
            }
        }

        private void UserListDeamon()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(5000);
                    SrvUser.BroadcastUserList(users);
                    Thread.Sleep(5000);
                    foreach (var u in users)
                    {
                        if (!u.IsConnected) users.Remove(u);
                    }
                }
                catch (Exception) { }
            }
        }
    }

    enum IncomingTypes
    {
        Message = 0,
        Login = 1,
        PrivateMessage = 2,
        UserlistRequest = 3
    }

    enum OutcomingTypes
    {
        Notification = 32,
        Message = 33,
        UserlistResponse = 34
    }

    class SrvUser
    {
        Socket socket;
        List<SrvUser> users;
        string nick = null;

        public string Nickname { get { return nick; } }

        public SrvUser(Socket s, List<SrvUser> users)
        {
            socket = s;
            this.users = users;
            new Thread(Run).Start();
        }

        void Run()
        {
            try
            {
                SendMessage("*** TrueServer alpha - Welcome aboard! ***", OutcomingTypes.Notification);
                byte[] buffer = new byte[0x4002];

                while (true)
                {
                    int command = 0, payload = 0, pl = 0;

                    if (socket.Receive(buffer, 3, SocketFlags.None) <= 0) break;
                    command = (buffer[0] & 254) >> 1;
                    payload = buffer[0] & 1;
                    if (payload == 0)
                    {
                        SendMessage("Binary messages is not supported.", OutcomingTypes.Notification);
                        continue;
                    }
                    pl = (buffer[1] << 8) | buffer[2];

                    int read = 0;
                    while(read < pl)
                    {
                        var l = socket.Receive(buffer, read + 3, pl - read, SocketFlags.None);
                        if (l == 0) break;
                        read += l;
                    }

                    string text = Encoding.UTF8.GetString(buffer, 3, pl);
                    var c = (IncomingTypes)command;

                    if (c == IncomingTypes.Message)
                    {
                        if (nick == null)
                        {
                            SendMessage("Not logged in.", OutcomingTypes.Notification);
                            continue;
                        }

                        foreach (var u in users)
                        {
                            if(u.CanAcceptMessages())
                                u.SendMessage(nick + '\n' + text);
                        }
                    }

                    else if (c == IncomingTypes.Login)
                    {
                        string tmp = text.Trim();
                        foreach (char ch in tmp)
                        {
                            if (ch < 0x20)
                            {
                                SendMessage("Invalid nickname.", OutcomingTypes.Message);
                                continue;
                            }
                        }

                        if(FindUser(tmp) != null)
                        {
                            SendMessage("Nickname is already in use.", OutcomingTypes.Notification);
                            continue;
                        }

                        if(nick == null) BroadcastMessage(tmp + " has joined", OutcomingTypes.Notification);
                        else BroadcastMessage(nick + " is now known as " + tmp, OutcomingTypes.Notification);
                        nick = tmp;
                        BroadcastUserList();
                    }

                    else if (c == IncomingTypes.PrivateMessage)
                    {
                        if (nick == null)
                        {
                            SendMessage("Not logged in.", OutcomingTypes.Notification);
                            continue;
                        }

                        int nlIdx = text.IndexOf('\n');
                        if (nlIdx < 0)
                        {
                            SendMessage("Malformed message.", OutcomingTypes.Notification);
                            continue;
                        }

                        string dest = text.Substring(0, nlIdx);
                        string msg = text.Substring(nlIdx + 1);
                        var u = FindUser(dest);
                        if (u == null)
                        {
                            SendMessage("No such user.", OutcomingTypes.Notification);
                            continue;
                        }

                        var m = nick + '\n' + msg;
                        SendMessage(m);
                        u.SendMessage(m);
                    }

                    else if (c == IncomingTypes.UserlistRequest)
                    {
                        SendUserList();
                    }

                    Console.WriteLine("> " + (nick == null ? "" : "(" + nick + ") ") + c + ": " + text);
                }
            }

            catch(Exception e)
            {
                Console.WriteLine("ex: " + e.GetType() + " " + e.Message);
            }

            finally
            {
                Console.WriteLine("Disconnected " + socket.RemoteEndPoint);
                if (nick != null) BroadcastMessage(nick + " has left", OutcomingTypes.Notification);
                BroadcastUserList();
                users.Remove(this);
            }
        }

        public static void BroadcastUserList(List<SrvUser> users)
        {
            var arr = users.ToArray();
            foreach (var u in arr) u.SendUserList();
        }

        public void BroadcastUserList()
        {
            BroadcastUserList(users);
        }

        public void SendUserList()
        {
            string list = "";
            foreach (var u in users)
            {
                if (!u.IsConnected || u.Nickname == null) continue;
                if (list.Length > 0) list += '\n';
                list += u.Nickname;
            }

            SendMessage(list, OutcomingTypes.UserlistResponse);
        }

        public void BroadcastMessage(string text, OutcomingTypes type = OutcomingTypes.Message)
        {
            var arr = users.ToArray();
            foreach (var u in arr) u.SendMessage(text, type);
        }

        public void SendMessage(string text, OutcomingTypes type = OutcomingTypes.Message)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            int len = (bytes.Length > 0x3fff ? 0x3fff : bytes.Length);
            byte[] buffer = new byte[len + 3];
            
            buffer[0] = (byte)(((int)type << 1) | 1);
            buffer[1] = (byte)(len >> 8);
            buffer[2] = (byte)len;
            Array.Copy(bytes, 0, buffer, 3, len);

            try
            {
                socket.Send(buffer);
            }
            catch (SocketException) { }
        }

        public bool CanAcceptMessages()
        {
            if (!socket.Connected || nick == null) return false;
            return true;
        }

        SrvUser FindUser(string nickname)
        {
            foreach (var u in users)
                if (socket.Connected && u.Nickname == nickname) return u;
            return null;
        }

        public bool IsConnected { get { return socket.Connected; } }
    }
}
