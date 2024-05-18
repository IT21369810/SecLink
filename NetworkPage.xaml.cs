using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SecLinkApp
{
    public partial class NetworkPage : Page
    {
        private UdpClient udpClient;
        private UdpClient listener;
        private const int Port = 45678;
        private const string MulticastGroupAddress = "239.0.0.222";
        private string uniqueIdentifier;
        private const string broadcastMessagePrefix = "SecLinkApp|"; 
        private CancellationTokenSource cts;
        private bool isClosing = false; // Flag to indicate the app is closing

        public ObservableCollection<User> Users { get; set; } = new ObservableCollection<User>();

        public NetworkPage()
        {
            InitializeComponent();
            Loaded += Network_Loaded;  

        }

        private bool IsPortAvailable(int port)
        {
            bool isAvailable = true;

            
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IPEndPoint[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpListeners();

            foreach (IPEndPoint endpoint in tcpConnInfoArray)
            {
                if (endpoint.Port == port)
                {
                    isAvailable = false;
                    break;
                }
            }

            return isAvailable;
        }

        private void StartListeningForBroadcasts()
        {
            if (!IsPortAvailable(Port))
            {
                MessageBox.Show($"Port {Port} is not available. Please choose a different port.");
                Console.WriteLine($"Port {Port} is not available. Please choose a different port.");
                return;
            }

            if (listener == null)
            {
                listener = new UdpClient();
                listener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                listener.ExclusiveAddressUse = false; 
                listener.Client.Bind(new IPEndPoint(IPAddress.Any, Port));
                listener.JoinMulticastGroup(IPAddress.Parse(MulticastGroupAddress));
                Task.Run(() => ListenForBroadcasts());
            }
        }

        private List<string> GetLocalIPAddresses()
        {
            var ipList = new List<string>();
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork) // IPv4 addresses only
                {
                    ipList.Add(ip.ToString());
                }
            }
            return ipList;
        }


        private async void ListenForBroadcasts()
        {
            var from = new IPEndPoint(IPAddress.Any, 0); // Listening on any IP
            var localIPs = GetLocalIPAddresses(); // list down the local IP addresses
            try
            {
                while (true)
                {
                    var result = await listener.ReceiveAsync();
                    var senderIP = result.RemoteEndPoint.Address.ToString();

                    // Skip the sender's IP address 
                    if (localIPs.Contains(senderIP))
                    {
                        continue;
                    }

                    string message = Encoding.UTF8.GetString(result.Buffer);
                    ProcessReceivedMessage(message);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show($"Error listening for broadcasts: {ex.Message}"));
            }
        }

        private void ProcessReceivedMessage(string message)
        {
            if (message.StartsWith(broadcastMessagePrefix))
            {
                var parts = message.Split('|');
                if (parts.Length >= 3) // Assuming message format: "SecLinkApp|Username|IPAddress"
                {
                    var receivedIdentifier = parts[0];
                    var username = parts[1];
                    var ipAddress = parts[2];

                    Dispatcher.Invoke(() =>
                    {
                        var userExists = Users.Any(u => u.Name == username && u.IPAddress == ipAddress);
                        if (!userExists)
                        {
                            Users.Add(new User { Name = username, Status = "Online", IPAddress = ipAddress });
                        }
                    });
                }
            }
        }

        // refresh
        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear existing users before scanning
            Users.Clear();
        }

        public class User
        {
            public string Name { get; set; }
            public string Status { get; set; }
            // Add an IP address or hostname for direct connections
            public string IPAddress { get; set; }
        }

        PresenceBroadcaster broadcaster;
        private void Network_Loaded(object sender, RoutedEventArgs e)
        {
            // Retrieve the username from the SQLite database
            string username = DatabaseHelper.GetUsername();
            if (username != null)
            {
                Console.WriteLine("Loged in as : " + username);
            }
            else
            {
                Console.WriteLine("Username not set");
            }
            usernameTextBox.Text = username ?? "Username not set";


            // Get the current WiFi name
            
            Console.WriteLine(NetworkHelper.GetConnectedNetworkDetails().ToString());
            wifiNameTextBox.Text = NetworkHelper.GetConnectedNetworkDetails().Name;
            networktype.Text = NetworkHelper.GetConnectedNetworkDetails().Type;



            UsersListView.ItemsSource = Users;
            uniqueIdentifier = $"{Environment.MachineName}_{Guid.NewGuid()}"; // Ensure uniqueness across app instances
            udpClient = new UdpClient();
            StartListeningForBroadcasts();

            broadcaster = new PresenceBroadcaster(Port, MulticastGroupAddress);
            broadcaster.StartBroadcasting();

        }
    }

}