using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MedvedChat
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        TrueClient client = new TrueClient();
        string currentAddr = null;
        ValueFile v;

        public MainWindow()
        {
            InitializeComponent();
            
            client.TextMessageAccepted += OnMessage;
            client.NotificationAccepted += OnNotification;
            client.UserListMessageAccepted += OnUserList;
            client.Log += OnLog;

            v = new ValueFile();
            addrTextBox.Text = (v.Read("server") ?? "themassacre.org");
            nicknameTextBox.Text = (v.Read("nickname") ?? "User" + new Random().Next(100, 999));
            inputBox.Text = (v.Read("input") ?? "");
            chatBox.Text = (v.Read("log") ?? "");
            if (chatBox.Text.Length > 0) PrintLine("");
            currentAddr = addrTextBox.Text;
            Connect();

            Thread keepalive = new Thread(KeepaliveThread);
            keepalive.IsBackground = true;
            keepalive.Start();

            Thread saving = new Thread(SavingThread);
            saving.IsBackground = true;
            saving.Start();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                DragMove();
            }
            catch (InvalidOperationException) { }
        }

        private void HoverEnter(object sender, MouseEventArgs e)
        {
           ((FrameworkElement)sender).Opacity = 1;
        }

        private void HoverLeave(object sender, MouseEventArgs e)
        {
            ((FrameworkElement)sender).Opacity = 0.5;
        }

        private void closeImg_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Close();
        }

        private void minimizeImg_MouseDown(object sender, MouseButtonEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        void OnMessage(string text, string sender = null)
        {
            PrintLine((sender == null ? "" : "[" + sender + "]: ") + text);
        }

        void OnNotification(string text, string sender = null)
        {
            PrintLine(text);
        }

        void OnLog(string text)
        {
            PrintLine("--- " + text);
        }

        void OnUserList(string[] users)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                userBox.Items.Clear();
                foreach (string u in users) userBox.Items.Add(u);
            }));
        }

        private void PrintLine(string s)
        {
            Dispatcher.Invoke((Action)(() =>
            {
                chatBox.Text += (chatBox.Text.Length == 0 ? "" : "\n") + s;
                if (chatBox.VerticalOffset + chatBox.ViewportHeight > chatBox.ExtentHeight - 32) chatBox.ScrollToEnd();
            }));
            if (!client.IsConnected) OnUserList(new string[0]);
        }

        private bool connecting = false;

        private void Connect()
        {
            if (connecting) return;
            connecting = true;
            string addr = currentAddr;
            string nick = null;
            Dispatcher.Invoke((Action)(() =>
            {
                nick = nicknameTextBox.Text;
            }));
            new Thread(delegate ()
            {
                client.Connect(addr);
                client.SendLogin(nick);
                client.SendUserListRequest();
                connecting = false;
            }).Start();
        }

        private void connectButton_Click(object sender, RoutedEventArgs e)
        {
            currentAddr = addrTextBox.Text;
            Connect();
        }

        private void inputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Return || inputBox.Text.Length == 0) return;
            string text = inputBox.Text;
            inputBox.Text = "";
            new Thread(delegate ()
            {
                client.SendMessage(text);
            }).Start();
        }

        private void loginButton_Click(object sender, RoutedEventArgs e)
        {
            if (nicknameTextBox.Text.Length == 0)
            {
                OnLog("Ник не может быть пустым.");
                return;
            }
            client.SendLogin(nicknameTextBox.Text);
            client.SendUserListRequest();
        }

        private void KeepaliveThread()
        {
            while(true)
            {
                try
                {
                    Thread.Sleep(10000);
                    if (!client.IsConnected) Connect();
                }
                catch (Exception) { }
            }
        }

        private void SaveValues()
        {
            string nick = null, input = null, log = null;
            Dispatcher.Invoke((Action)(() =>
            {
                nick = nicknameTextBox.Text;
                input = inputBox.Text;
                log = chatBox.Text;
            }));

            v.Write("server", currentAddr);
            v.Write("nickname", nick);
            v.Write("input", input);
            v.Write("log", log);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveValues();
            v.DestroyLockFile();
        }

        private void SavingThread()
        {
            while(true)
            {
                Thread.Sleep(60000);
                SaveValues();
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            chatBox.ScrollToEnd();
        }
    }
}
