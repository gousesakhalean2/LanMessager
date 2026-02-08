using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace LanMessager
{
    public class PeerDiscovery
    {
        private UdpClient udpClient;
        private int port = 5000; // IPMsg default port

        public event Action<string> PeerFound;

        public void Start()
        {
            udpClient = new UdpClient(port);
            udpClient.EnableBroadcast = true;
            Thread receiveThread = new Thread(ReceiveLoop);
            receiveThread.IsBackground = true;
            receiveThread.Start();

            BroadcastPresence();
        }

        public void BroadcastPresence()
        {
            IPEndPoint ep = new IPEndPoint(IPAddress.Broadcast, port);
            string msg = "IPMSG_PEER_ONLINE|" + Dns.GetHostName();
            byte[] data = Encoding.UTF8.GetBytes(msg);
            udpClient.Send(data, data.Length, ep);
        }

        private void ReceiveLoop()
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, port);
            while (true)
            {
                try
                {
                    byte[] data = udpClient.Receive(ref remoteEP);
                    string message = Encoding.UTF8.GetString(data);
                    if (message.StartsWith("IPMSG_PEER_ONLINE|"))
                    {
                        string name = message.Split('|')[1];

                        // No null-conditional ?. => classic check
                        if (PeerFound != null)
                        {
                            PeerFound(string.Format("{0} ({1})", name, remoteEP.Address));
                        }
                    }
                }
                catch { }
            }
        }

    }
}
