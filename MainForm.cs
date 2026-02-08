using System;
using System.IO;
using System.Windows.Forms;

namespace LanMessager
{
    public partial class MainForm : Form
    {
        private PeerDiscovery discovery;
        private TcpMessenger messenger;
        private Timer timer;

        public MainForm()
        {
            InitializeComponent();

            // Initialize Peer Discovery
            discovery = new PeerDiscovery();
            discovery.PeerFound += PeerFoundHandler;
            discovery.Start();

            // Initialize TCP Messenger
            messenger = new TcpMessenger();
            messenger.MessageReceived += MessageReceivedHandler;
            messenger.FileProgressChanged += FileProgressHandler; // NEW
            messenger.StartServer();

            // Auto-refresh every 30s
            timer = new Timer();
            timer.Interval = 2000; // 30 seconds
            timer.Tick += (s, e) => discovery.BroadcastPresence();
            timer.Start();
        }

        // Peer discovery
        private void PeerFoundHandler(string peer)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action(() => PeerFoundHandler(peer)));
            }
            else
            {
                if (!lstClients.Items.Contains(peer))
                    lstClients.Items.Add(peer);
            }
        }

        // Message received  

        private void MessageReceivedHandler(string message)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action(() => MessageReceivedHandler(message)));
            }
            else
            {
                // If the message starts with "File received:", add timestamp
                if (message.StartsWith("File received:"))
                {
                    string timestamp = DateTime.Now.ToString("[HH:mm:ss] ");
                    txtLog.AppendText(timestamp + message + Environment.NewLine);
                }
                else
                {
                    // Just append the message as-is for normal text
                    txtLog.AppendText(message + Environment.NewLine);
                }
            }
        }



        // File transfer progress
        private void FileProgressHandler(int percent)
        {
            if (InvokeRequired)
            {
                this.Invoke(new Action(() => FileProgressHandler(percent)));
            }
            else
            {
                pbFileTransfer.Value = percent;
                if (percent >= 100) pbFileTransfer.Value = 0;
            }
        }

        // Send message
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

        // Send file
        private void btnSendFile_Click(object sender, EventArgs e)
        {
            if (lstClients.SelectedItem == null)
            {
                MessageBox.Show("Select a client to send message or files.");
                return;
            }

            string ip = lstClients.SelectedItem.ToString().Split('(')[1].TrimEnd(')');

            // 1️⃣ If files are queued, send them
            if (lstQueuedFiles.Items.Count > 0)
            {
                foreach (string filePath in lstQueuedFiles.Items)
                {
                    messenger.SendFile(ip, filePath);
                    string timestamp = DateTime.Now.ToString("[HH:mm:ss] ");
                    txtLog.AppendText(timestamp + "File sent: " + System.IO.Path.GetFileName(filePath) + Environment.NewLine);
                }

                // Clear the queue after sending
                lstQueuedFiles.Items.Clear();
            }
            // 2️⃣ Else, send text message
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

        // Manual refresh
        private void btnRefresh_Click(object sender, EventArgs e)
        {
            if (discovery != null)
                discovery.BroadcastPresence();
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            // Check if data is a file
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
                if (System.IO.File.Exists(filePath))
                {
                    // Add to listbox if not already added
                    if (!lstQueuedFiles.Items.Contains(filePath))
                        lstQueuedFiles.Items.Add(filePath);
                }
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {

        }

    }
}
