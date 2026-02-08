using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace LanMessager
{
    public class TcpMessenger
    {
        private TcpListener listener;
        private int port = 2426;

        public event Action<string> MessageReceived;
        public event Action<int> FileProgressChanged;

        public void StartServer()
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            System.Threading.Thread acceptThread = new System.Threading.Thread(AcceptLoop);
            acceptThread.IsBackground = true;
            acceptThread.Start();
        }

        private void AcceptLoop()
        {
            while (true)
            {
                var client = listener.AcceptTcpClient();
                System.Threading.Thread clientThread = new System.Threading.Thread(() => HandleClient(client));
                clientThread.IsBackground = true;
                clientThread.Start();
            }
        }

        private void HandleClient(TcpClient client)
        {
            NetworkStream ns = client.GetStream();
            BinaryReader br = new BinaryReader(ns, System.Text.Encoding.UTF8);
            try
            {
                string header = br.ReadString();
                if (header.StartsWith("MSG|"))
                {
                    string msg = header.Substring(4);
                    if (MessageReceived != null)
                        MessageReceived(msg);
                }
                else if (header.StartsWith("FILE|"))
                {
                    string[] parts = header.Split('|');
                    string fileName = parts[1];
                    long fileSize = long.Parse(parts[2]);

                    string saveFolder = Path.Combine(Application.StartupPath, "ReceivedFiles");
                    if (!Directory.Exists(saveFolder))
                        Directory.CreateDirectory(saveFolder);

                    string savePath = GetUniqueFilePath(saveFolder, fileName);

                    using (FileStream fs = new FileStream(savePath, FileMode.Create, FileAccess.Write))
                    {
                        byte[] buffer = new byte[8192];
                        long received = 0;
                        while (received < fileSize)
                        {
                            int read = ns.Read(buffer, 0, (int)Math.Min(buffer.Length, fileSize - received));
                            fs.Write(buffer, 0, read);
                            received += read;

                            if (FileProgressChanged != null)
                                FileProgressChanged((int)((received * 100) / fileSize));
                        }
                    }
                    if (MessageReceived != null)
                        MessageReceived("File received: " + savePath);
                }
            }
            catch { }
            finally
            {
                client.Close();
            }
        }

        public void SendMessage(string ip, string message)
        {
            TcpClient client = new TcpClient(ip, port);
            NetworkStream ns = client.GetStream();
            BinaryWriter bw = new BinaryWriter(ns, System.Text.Encoding.UTF8);
            bw.Write("MSG|" + message);
            bw.Flush();
            client.Close();
        }

        public void SendFile0(string ip, string filePath)
        {
            FileInfo fi = new FileInfo(filePath);
            TcpClient client = new TcpClient(ip, port);
            NetworkStream ns = client.GetStream();
            BinaryWriter bw = new BinaryWriter(ns, System.Text.Encoding.UTF8);

            // String.Format or concatenation instead of $"
            bw.Write("FILE|" + fi.Name + "|" + fi.Length);
            bw.Flush();

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] buffer = new byte[8192];
                int read;
                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ns.Write(buffer, 0, read);
                }
            }
            client.Close();
        }

        public void SendFile(string ip, string filePath)
        {
            FileInfo fi = new FileInfo(filePath);
            TcpClient client = new TcpClient(ip, port);
            NetworkStream ns = client.GetStream();
            BinaryWriter bw = new BinaryWriter(ns, System.Text.Encoding.UTF8);

            bw.Write("FILE|" + fi.Name + "|" + fi.Length);
            bw.Flush();

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                byte[] buffer = new byte[8192];
                long sent = 0;
                int read;
                while ((read = fs.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ns.Write(buffer, 0, read);
                    sent += read;

                    // Raise progress event
                    if (FileProgressChanged != null)
                    {
                        int percent = (int)((sent * 100) / fi.Length);
                        FileProgressChanged(percent);
                    }
                }
            }

            if (FileProgressChanged != null)
                FileProgressChanged(100); // complete

            client.Close();
        }



        private string GetUniqueFilePath(string folder, string fileName)
        {
            string fullPath = Path.Combine(folder, fileName);
            int count = 1;
            while (File.Exists(fullPath))
            {
                string nameOnly = Path.GetFileNameWithoutExtension(fileName);
                string ext = Path.GetExtension(fileName);
                fullPath = Path.Combine(folder, string.Format("{0}({1}){2}", nameOnly, count, ext));
                count++;
            }
            return fullPath;
        }
    }
}
