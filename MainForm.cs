using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace LanMessager
{
    public partial class MainForm : Form
    {
        private PeerDiscovery discovery;
        private TcpMessenger messenger;
        private Timer timer;

        // Tray components
        private NotifyIcon notifyIcon1;
        private ContextMenuStrip trayMenu;
        private Dictionary<string, DateTime> peerLastSeen =   new Dictionary<string, DateTime>();


        public MainForm()
        {
            InitializeComponent();

            // ===============================
            // SYSTEM TRAY INITIALIZATION
            // ===============================

            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Show", null, Show_Click);
            trayMenu.Items.Add("Exit", null, Exit_Click);

            notifyIcon1 = new NotifyIcon();
            notifyIcon1.Icon = this.Icon; // Default app icon
            notifyIcon1.Text = "LanMessager";
            notifyIcon1.Visible = false;
            notifyIcon1.ContextMenuStrip = trayMenu;
            notifyIcon1.DoubleClick += (s, e) => ShowFromTray();

            // ===============================
            // PEER DISCOVERY
            // ===============================
            discovery = new PeerDiscovery();
            discovery.PeerFound += PeerFoundHandler;
            discovery.Start();

            // ===============================
            // TCP MESSENGER
            // ===============================
            messenger = new TcpMessenger();
            messenger.MessageReceived += MessageReceivedHandler;
            messenger.FileProgressChanged += FileProgressHandler;
            messenger.StartServer();

            // Auto refresh every 2 seconds
            timer = new Timer();
            timer.Interval = 3000;

            timer.Tick += (s, e) =>
            {
                discovery.BroadcastPresence();
                RemoveInactivePeers();
            };


            timer.Start();
        }


        private void RemoveInactivePeers()
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action(RemoveInactivePeers));
                return;
            }

            var inactivePeers = new List<string>();

            foreach (var peer in peerLastSeen)
            {
                // If not seen for 4 seconds → remove
                if ((DateTime.Now - peer.Value).TotalSeconds > 4)
                {
                    inactivePeers.Add(peer.Key);
                }
            }

            foreach (var peer in inactivePeers)
            {
                peerLastSeen.Remove(peer);
                lstClients.Items.Remove(peer);
            }
        }


        // ===============================
        // SYSTEM TRAY METHODS
        // ===============================

        private void HideToTray()
        {
            notifyIcon1.Visible = true;
            notifyIcon1.BalloonTipTitle = "LanMessager";
            notifyIcon1.BalloonTipText = "Application is running in background.";
            notifyIcon1.ShowBalloonTip(2000);

            this.Hide();
            this.ShowInTaskbar = false;
        }

        private void ShowFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            notifyIcon1.Visible = false;
        }

        private void Show_Click(object sender, EventArgs e)
        {
            ShowFromTray();
        }

        private void Exit_Click(object sender, EventArgs e)
        {
            notifyIcon1.Visible = false;
            Application.Exit();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (this.WindowState == FormWindowState.Minimized)
            {
                HideToTray();
            }
        }

        // ===============================
        // PEER DISCOVERY
        // ===============================   
        private void PeerFoundHandler(string peer)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action(() => PeerFoundHandler(peer)));
                return;
            }

            // Update last seen time
            peerLastSeen[peer] = DateTime.Now;

            // Add to list if not already
            if (!lstClients.Items.Contains(peer))
                lstClients.Items.Add(peer);
        }



        // ===============================
        // MESSAGE RECEIVED
        // ===============================

        private void MessageReceivedHandler0(string message)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action(() => MessageReceivedHandler(message)));
            }
            else
            {
                if (message.StartsWith("File received:"))
                {
                    string timestamp = DateTime.Now.ToString("[HH:mm:ss] ");
                    txtLog.AppendText(timestamp + message + Environment.NewLine);
                }
                else
                {
                    txtLog.AppendText(message + Environment.NewLine);
                }
            }
        }

        private void MessageReceivedHandler(string message)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action(() => MessageReceivedHandler(message)));
                return;
            }

            string timestamp = DateTime.Now.ToString("[HH:mm:ss] ");

            // Show in chat window
            if (message.StartsWith("File received:"))
            {
                txtLog.AppendText(timestamp + message + Environment.NewLine);
            }
            else
            {
                txtLog.AppendText(message + Environment.NewLine);
            }

            // ===============================
            // SYSTEM TRAY NOTIFICATION
            // ===============================

            /*notifyIcon1.Visible = true; // Ensure tray icon is visible

            notifyIcon1.BalloonTipTitle = "New Message";
            notifyIcon1.BalloonTipText = message.Length > 100
                ? message.Substring(0, 100) + "..."
                : message;

            notifyIcon1.ShowBalloonTip(3000);*/



            if (this.WindowState == FormWindowState.Minimized || !this.Visible)
            {
                notifyIcon1.Visible = true;

                notifyIcon1.BalloonTipTitle = "New Message";
                notifyIcon1.BalloonTipText = message.Length > 100
                    ? message.Substring(0, 100) + "..."
                    : message;

                notifyIcon1.ShowBalloonTip(3000);
            }

        }


        // ===============================
        // FILE PROGRESS
        // ===============================

        private void FileProgressHandler(int percent)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action(() => FileProgressHandler(percent)));
            }
            else
            {
                pbFileTransfer.Value = percent;
                if (percent >= 100)
                    pbFileTransfer.Value = 0;
            }
        }

        // ===============================
        // SEND MESSAGE
        // ===============================

        private void btnSend_Click(object sender, EventArgs e)
        {
            if (lstClients.SelectedItem != null && !string.IsNullOrEmpty(txtMessage.Text))
            {
                string ip = lstClients.SelectedItem.ToString().Split('(')[1].TrimEnd(')');
                messenger.SendMessage(ip, txtMessage.Text);

                string timestamp = DateTime.Now.ToString("[HH:mm:ss] ");
                txtLog.AppendText(timestamp + "Me: " + txtMessage.Text + Environment.NewLine);

                txtMessage.Clear();
            }
            else
            {
                MessageBox.Show("Select a client and enter a message.");
            }
        }

        // ===============================
        // SEND FILE OR MESSAGE
        // ===============================

        private void btnSendFile_Click(object sender, EventArgs e)
        {
            if (lstClients.SelectedItem == null)
            {
                MessageBox.Show("Select a client to send message or files.");
                return;
            }

            string ip = lstClients.SelectedItem.ToString().Split('(')[1].TrimEnd(')');

            if (lstQueuedFiles.Items.Count > 0)
            {
                foreach (string filePath in lstQueuedFiles.Items)
                {
                    messenger.SendFile(ip, filePath);
                    string timestamp = DateTime.Now.ToString("[HH:mm:ss] ");
                    txtLog.AppendText(timestamp + "File sent: " +
                        System.IO.Path.GetFileName(filePath) + Environment.NewLine);
                }

                lstQueuedFiles.Items.Clear();
            }
            else if (!string.IsNullOrEmpty(txtMessage.Text))
            {
                messenger.SendMessage(ip, txtMessage.Text);

                string timestamp = DateTime.Now.ToString("[HH:mm:ss] ");
                txtLog.AppendText(timestamp + "Me: " + txtMessage.Text + Environment.NewLine);

                txtMessage.Clear();
            }
            else
            {
                MessageBox.Show("Enter a message or drag files to send.");
            }
        }

        // ===============================
        // REFRESH
        // ===============================

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            if (discovery != null)
                discovery.BroadcastPresence();
        }

        // ===============================
        // DRAG & DROP
        // ===============================

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

            foreach (string filePath in files)
            {
                if (File.Exists(filePath))
                {
                    if (!lstQueuedFiles.Items.Contains(filePath))
                        lstQueuedFiles.Items.Add(filePath);
                }
            }
        }

        // ===============================
        // CLEAR
        // ===============================

        private void btnClear_Click(object sender, EventArgs e)
        {
            txtLog.Clear();
            txtMessage.Clear();
        }

        // ===============================
        // HIDE BUTTON CLICK
        // ===============================

        private void btnHide_Click(object sender, EventArgs e)
        {
            HideToTray();
        }
    }
}
