using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SecLinkApp
{

    public partial class SideWindowSend : Window
    {
        private ECDiffieHellman senderEcdh;
        private UdpClient udpClient;
        private UdpClient listener;
        private const int Port = 45678;
        private const string MulticastGroupAddress = "239.0.0.222";
        private string uniqueIdentifier;
        private const string broadcastMessagePrefix = "SecLinkApp|"; 
        private CancellationTokenSource cts;
        private bool isClosing = false; // Flag to indicate the app is closing

        public ObservableCollection<User> Users { get; set; } = new ObservableCollection<User>();

        private string[] _filePaths;
        private byte[] _ephemeralKey;

        public SideWindowSend(string[] filePaths, IProgress<UploadProgressInfo> progress, byte[] ephemeralKey) 
        {
            InitializeComponent();
            Loaded += Network_Loaded;
            Unloaded += Network_Unloaded;
            _filePaths = filePaths;
            UsersListView.ItemsSource = Users;
            _progress = progress;
            _ephemeralKey = ephemeralKey;

        }
        private IProgress<UploadProgressInfo> _progress;

        private async Task<bool> SendMetadataAsync(ClientWebSocket client, string targetIPAddress, int targetPort, string[] filePaths)
        {
            try
            {
                long totalFileSize = 0;

                foreach (var filePath in filePaths)
                {
                    FileInfo fileInfo = new FileInfo(filePath);
                    totalFileSize += fileInfo.Length;
                }
                // Serialize public key to a format that can be included in JSON

                var keyPair = EcdhHelper.GenerateKeyPair();
                senderEcdh = keyPair.ecdh; // Store the ECDiffieHellman instance 
                string publicKeyBase64 = Convert.ToBase64String(keyPair.publicKey);
                Console.WriteLine($"Sender: Sending Public Key to Receiver - {publicKeyBase64}");

                var filesMetadata = filePaths.Select(filePath => new FileInfo(filePath))
            .ToDictionary(fileInfo => fileInfo.Name, fileInfo => fileInfo.Length);

                var metadata = new
                {
                    Type = "Metadata",
                    Sender = DatabaseHelper.GetUsername(),
                    Files = filePaths.Select(filePath => new FileInfo(filePath))
                     .ToDictionary(fileInfo => fileInfo.Name, fileInfo => fileInfo.Length),
                    TotalFileSize = filePaths.Select(filePath => new FileInfo(filePath))
                             .Sum(fileInfo => fileInfo.Length),
                    TotalFiles = filePaths.Length,
                    PublicKey = publicKeyBase64
                };

                string jsonMetadata = JsonConvert.SerializeObject(metadata);
                byte[] metadataBuffer = Encoding.UTF8.GetBytes(jsonMetadata);
                await client.SendAsync(new ArraySegment<byte>(metadataBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                Console.WriteLine("Metadata and public key sent successfully.");

                var responseBuffer = new byte[1024];
                WebSocketReceiveResult response = await client.ReceiveAsync(new ArraySegment<byte>(responseBuffer), CancellationToken.None);
                string responseString = Encoding.UTF8.GetString(responseBuffer, 0, response.Count);

                dynamic jsonResponse = JsonConvert.DeserializeObject(responseString);
                if (jsonResponse != null)
                {
                    string receiverPublicKeyBase64 = jsonResponse.ReceiverPublicKey;
                    string decision = jsonResponse.Decision;
                    Console.WriteLine($"Decision: {decision}, Receiver's Public Key: {receiverPublicKeyBase64}");
                    byte[] receiverPublicKey = Convert.FromBase64String(receiverPublicKeyBase64);// Decode the receiver's public key from Base64

                    if (decision == "Accepted")
                    {
                        // Use the stored senderEcdh to derive the shared secret
                        var sharedSecret = senderEcdh.DeriveKeyMaterial(ECDiffieHellmanCngPublicKey.FromByteArray(receiverPublicKey, CngKeyBlobFormat.EccPublicBlob));
                        Console.WriteLine($"Shared Secret (Sender Side): {BitConverter.ToString(sharedSecret)}");
                    
                        byte[] ephemeralKeyIv;
                        byte[] encryptedEphemeralKey = SecureEphemeralKeyExchange.EncryptEphemeralKey(_ephemeralKey, sharedSecret, out ephemeralKeyIv);

                        string encryptedEphemeralKeyBase64 = Convert.ToBase64String(encryptedEphemeralKey);
                        string ephemeralKeyIvBase64 = Convert.ToBase64String(ephemeralKeyIv);

                        Console.WriteLine($"ephemeralKeyIv:{ephemeralKeyIv} ephemeralKeyIvBase64: {ephemeralKeyIvBase64} and encryptedEphemeralKey: {encryptedEphemeralKey} encryptedEphemeralKeyBase64: {encryptedEphemeralKeyBase64}");

                        var keyMessage = new
                        {
                            Type = "EncryptedEphemeralKey",
                            EncryptedKey = encryptedEphemeralKeyBase64,
                            IV = ephemeralKeyIvBase64
                        };

                        string jsonKeyMessage = JsonConvert.SerializeObject(keyMessage);
                        byte[] keyMessageBuffer = Encoding.UTF8.GetBytes(jsonKeyMessage);

                        // Send the message
                        await client.SendAsync(new ArraySegment<byte>(keyMessageBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                        Console.WriteLine("Encrypted ephemeral key and IV sent successfully.");


                        // Proceed based on the decision
                        return decision == "Accepted";
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during sending metadata: {ex.Message}");
                return false;
            }
        }
        
        private async Task SendFileAsync(ClientWebSocket client, string targetIPAddress, int targetPort, IProgress<UploadProgressInfo> progress)
        {
            Stopwatch stopwatch = new Stopwatch();
            int fileIndex = 0;

            foreach (var filePath in _filePaths)
            {
                FileInfo fileInfo = new FileInfo(filePath);
                long totalBytes = fileInfo.Length, totalBytesSent = 0;
                byte[] buffer = new byte[4096];

                using (FileStream fs = File.OpenRead(filePath))
                {
                    int bytesRead;
                    stopwatch.Restart(); // Restart stopwatch for each file

                    while ((bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await client.SendAsync(new ArraySegment<byte>(buffer, 0, bytesRead), WebSocketMessageType.Binary, fs.Position == fs.Length, CancellationToken.None);
                        totalBytesSent += bytesRead;

                        double percentage = (double)totalBytesSent / totalBytes * 100;
                        TimeSpan timeTaken = stopwatch.Elapsed;
                        TimeSpan timeRemaining = TimeSpan.FromMilliseconds(timeTaken.TotalMilliseconds / totalBytesSent * (totalBytes - totalBytesSent));

                        _progress.Report(new UploadProgressInfo
                        {
                            FileIndex = fileIndex,
                            FileName = fileInfo.Name,
                            BytesSent = totalBytesSent,
                            TotalBytes = totalBytes,
                            Percentage = percentage,
                            TimeRemaining = timeRemaining.ToString(@"hh\:mm\:ss")
                        });
                    }
                }

                fileIndex++; // Move to the next file
            }

            foreach (var filePath in _filePaths)
            {
                try
                {
                    File.Delete(filePath);
                    Console.WriteLine($"Deleted encrypted file: {filePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to delete encrypted file {filePath}: {ex.Message}");
                }
            }

            MessageBox.Show("Files sent successfully.");
        }

        private async void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersListView.SelectedItem is User selectedUser)
            {
                using (ClientWebSocket client = new ClientWebSocket())
                {
                    try
                    {
                        // Connect to the selected user's WebSocket server
                        await client.ConnectAsync(new Uri($"ws://{selectedUser.IPAddress}:{45679}/"), CancellationToken.None);
                        Console.WriteLine("Connected to the WebSocket server for metadata.");

                        // Send metadata and wait for the receiver's response
                        bool accepted = await SendMetadataAsync(client, selectedUser.IPAddress, 45679, _filePaths);

                        if (accepted)
                        {
                            this.Close();
                            Console.WriteLine("Receiver accepted, attempting to send files...");
                            await SendFileAsync(client, selectedUser.IPAddress, 45679, _progress);

                            MessageBox.Show("Files sent successfully.");
                        }
                        else
                        {

                            this.Close();
                            MessageBox.Show("Receiver declined the file transfer.");
                        }
                    }

                    catch (WebSocketException ex)
                    {
                        MessageBox.Show($"Communication error: {ex.Message}");
                    }
                    finally
                    {
                        // Ensure the WebSocket is closed properly
                        if (client.State == WebSocketState.Open)
                        {
                            await client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                        }
                    }
                }

            }

            else
            {
                MessageBox.Show("Please select a user to send files to.");
            }
        }


        private void Network_Unloaded(object sender, RoutedEventArgs e)
        {
            if (broadcaster != null)
            {
                broadcaster.StopBroadcasting();
            }
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

                    // Skip if the sender's IP address
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

        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear existing users before scanning
            Users.Clear();
        }

        public class User
        {
            public string Name { get; set; }
            public string Status { get; set; }
            public string IPAddress { get; set; }
        }

        PresenceBroadcaster broadcaster;
        private void Network_Loaded(object sender, RoutedEventArgs e)
        {
            string username = DatabaseHelper.GetUsername();
            if (username != null)
            {
                Console.WriteLine("Loged in as : " + username);
            }
            else
            {
                Console.WriteLine("Username not set");
            }


            Console.WriteLine(NetworkHelper.GetConnectedNetworkDetails().ToString());

            UsersListView.ItemsSource = Users;
            uniqueIdentifier = $"{Environment.MachineName}_{Guid.NewGuid()}"; // Ensure uniqueness across app instances
            udpClient = new UdpClient();
            StartListeningForBroadcasts();

            broadcaster = new PresenceBroadcaster(Port, MulticastGroupAddress);
            broadcaster.StartBroadcasting();

        }
    }

}