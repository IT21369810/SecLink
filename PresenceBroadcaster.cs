using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SecLinkApp
{
    class PresenceBroadcaster
    {
        private readonly int _port; // The port to broadcast on
        private readonly string _multicastGroupAddress; // The multicast group address
        private CancellationTokenSource _broadcastCancellation; // Source for cancellation token

        public PresenceBroadcaster(int port, string multicastGroupAddress)
        {
            _port = port;
            _multicastGroupAddress = multicastGroupAddress;
        }

        public void StartBroadcasting()
        {
            _broadcastCancellation = new CancellationTokenSource();
            Task.Run(() => BroadcastPresence(_broadcastCancellation.Token));
        }

        public void StopBroadcasting()
        {
            _broadcastCancellation?.Cancel();
        }

        private async Task BroadcastPresence(CancellationToken cancellationToken)
        {
            string username = DatabaseHelper.GetUsername(); // retrieves a valid username
            string ipAddress = GetLocalIPAddress(); // Get the local IP address of the device

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    using (var broadcaster = new UdpClient())
                    {
                        broadcaster.EnableBroadcast = true;
                        var endPoint = new IPEndPoint(IPAddress.Parse(_multicastGroupAddress), _port);
                        // Include the actual IP address in the broadcast message
                        string message = $"SecLinkApp|{username}|{ipAddress}";
                        byte[] bytes = Encoding.UTF8.GetBytes(message);

                        await broadcaster.SendAsync(bytes, bytes.Length, endPoint);
                        Console.WriteLine($"Broadcasting message: {message}");
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Broadcasting canceled.");
                    break; 
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Broadcast error: {ex.Message}. Retrying...");
                }

                try
                {
                    // Await a delay or the cancellation
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // Catch cancellation and gracefully exit the loop
                    Console.WriteLine("Broadcasting stopped due to cancellation.");
                    break;
                }
            }
        }
        private string GetLocalIPAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
            throw new Exception("No network adapters with an IPv4 address in the system!");
        }

    }
}
