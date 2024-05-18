using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Cms;
using SecLinkApp;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

public class WebSocketFileServer
{
    private readonly int _port;
    private long totalFileSize = 0;
    private byte[] sharedSecret = null;
    private string ivBase64 = null;
    private int TotalFiles = 0;
    private List<string> fileNamesList = new List<string>();
    private int currentIndex = -1; // Initialize to -1, indicating no file is currently being processed
    private Dictionary<string, long> fileSizes = new Dictionary<string, long>();
    private Dictionary<string, long> receivedFileSizes = new Dictionary<string, long>();
    private IProgress<DownloadProgressInfo> _progress;
    public event Action<DownloadProgressInfo> ProgressChanged;
    string currentFileName = "";
    public event EventHandler<StatusMessageEventArgs> StatusMessageUpdated;



    public WebSocketFileServer(int port, IProgress<DownloadProgressInfo> progress)
    {
        _port = port;
        _progress = progress;
    }
    public class StatusMessageEventArgs : EventArgs
    {
        public string Message { get; set; }
        public Brush Color { get; set; }

        public StatusMessageEventArgs(string message, Brush color)
        {
            Message = message;
            Color = color;
        }
    }


    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var httpListener = new HttpListener();
        httpListener.Prefixes.Add($"http://+:{_port}/");
        httpListener.Start();
        Console.WriteLine($"WebSocket server started at ws://localhost:{_port}/");
        StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs($"WebSocket server started at ws://localhost:{_port}/", Brushes.Green));

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await httpListener.GetContextAsync();
                if (context.Request.IsWebSocketRequest)
                {
                    Console.WriteLine("Accepting WebSocket connection...");
                    var webSocketContext = await context.AcceptWebSocketAsync(subProtocol: null);
                    Console.WriteLine("WebSocket connection established.");
                    StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs("WebSocket connection established.", Brushes.Green));
                    var webSocket = webSocketContext.WebSocket;

                    // Handle the WebSocket connection
                    await ReceiveFilesAsync(webSocket, cancellationToken);
                }
                else
                {
                    context.Response.StatusCode = 400;
                    context.Response.Close();
                }
            }
        }
        catch (Exception ex)
        {
            StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs($"Server error: {ex.Message}", Brushes.Red));
            Console.WriteLine($"Server error: {ex.Message}"); Console.WriteLine(ex.ToString());
        }
        finally
        {
            httpListener.Stop();
        }

    }
    private async Task ReceiveFilesAsync(WebSocket webSocket, CancellationToken cancellationToken)
    {
        string defaultDirectory = DatabaseHelper.GetDefaultDirectory() ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var buffer = new byte[4096];
        bool isFileTransferAccepted = false;
        long keyRetriveSize = 0;
        long totalBytesReceived = 0;
        string sender = "";

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var memoryStream = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    memoryStream.Write(buffer, 0, result.Count);

                    if (currentFileName != "" && fileSizes.ContainsKey(currentFileName))
                    {
                        receivedFileSizes[currentFileName] += result.Count;
                        long currentFileSize = fileSizes[currentFileName];
                        long currentFileReceived = Math.Min(receivedFileSizes[currentFileName], currentFileSize); // Ensure it does not exceed file size
                        double percentageReceived = currentFileSize > 0 ? (double)currentFileReceived / currentFileSize * 100 : 0;

                        DownloadProgressInfo progressInfo = new DownloadProgressInfo
                        {
                            FileName = currentFileName,
                            BytesReceived = currentFileReceived,
                            TotalBytes = currentFileSize
                        }; 
                        ProgressChanged?.Invoke(progressInfo);
                    }
                }
                while (!result.EndOfMessage);

                memoryStream.Seek(0, SeekOrigin.Begin);

                if (result.MessageType == WebSocketMessageType.Text)
                {

                    string message = Encoding.UTF8.GetString(memoryStream.ToArray());
                    Console.WriteLine($"Received message: {message}");


                    if (message == "EOF")
                    {
                        Console.WriteLine("End of file transfer.");
                        isFileTransferAccepted = false; // Reset for the next operation
                    }
                    else
                    {
                        try
                        {
                            dynamic metadata = JsonConvert.DeserializeObject(message);

                            if (metadata?.Type == "Metadata" && metadata.PublicKey != null)
                            {

                                Console.WriteLine("Processing Metadata message");
                                sender = metadata.Sender;
                                long totalFileSize = metadata.TotalFileSize;
                                string senderPublicKeyBase64 = metadata.PublicKey;
                                totalFileSize = metadata.TotalFileSize;
                                this.TotalFiles = metadata.TotalFiles;
                                StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs($"You have a file transfer request from : {sender}", Brushes.Blue));

                                foreach (var file in metadata.Files)
                                {
                                    string fileName = file.Name;
                                    long fileSize = file.Value;
                                    fileSizes[fileName] = fileSize;
                                    receivedFileSizes[fileName] = 0; // Initialize received bytes counter for this file
                                }

                                byte[] senderPublicKey = Convert.FromBase64String(senderPublicKeyBase64);
                                Console.WriteLine($"Receiver: Received Sender's Public Key - {senderPublicKeyBase64}");
                                Console.WriteLine($"Public Key Length (before sending): {senderPublicKey.Length}");

                                var (receiverPublicKey, receiverEcdh) = EcdhHelper.GenerateKeyPair();
                                //shared Secret calculation
                                this.sharedSecret = receiverEcdh.DeriveKeyMaterial(ECDiffieHellmanCngPublicKey.FromByteArray(senderPublicKey, CngKeyBlobFormat.EccPublicBlob));
                                Console.WriteLine($"Shared Secret (Receiver Side): {BitConverter.ToString(sharedSecret)}");

                                //Sending Publick key to sender
                                string receiverPublicKeyBase64 = Convert.ToBase64String(receiverEcdh.PublicKey.ToByteArray());
                                Console.WriteLine($"Receiver: Sending Public Key64 back to Sender - {receiverPublicKeyBase64}");

                                // Extract file names and sizes
                                Dictionary<string, long> files = metadata.Files.ToObject<Dictionary<string, long>>();

                                // Extract file names
                                List<string> receivedFilesList = files.Keys.ToList();

                                currentFileName = receivedFilesList[0]; // Assuming there's at least one file

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    AcceptDeclineWindow acceptDeclineWindow = new AcceptDeclineWindow(sender, receivedFilesList, totalFileSize);
                                    // This line blocks until the window is closed, capturing the user's decision correctly
                                    var dialogResult = acceptDeclineWindow.ShowDialog();

                                    isFileTransferAccepted = acceptDeclineWindow.Accepted; // This captures the user's decision from the dialog
                                    Console.WriteLine($"Window closed. User decision: {(isFileTransferAccepted ? "Accepted" : "Declined")}");
                                });

                                // When sending the decision back to the sender
                                string decision = isFileTransferAccepted ? "Accepted" : "Declined";
                                var response = new
                                {
                                    Decision = decision,
                                    ReceiverPublicKey = Convert.ToBase64String(receiverPublicKey) // Assuming receiverPublicKey is available
                                };
                                string jsonResponse = JsonConvert.SerializeObject(response);
                                byte[] decisionBuffer = Encoding.UTF8.GetBytes(jsonResponse);
                                await webSocket.SendAsync(new ArraySegment<byte>(decisionBuffer), WebSocketMessageType.Text, true, cancellationToken);

                                Console.WriteLine($"Sent decision to sender and PublicKey: {decision} {receiverPublicKey}");
                                StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs($"Receivers Decision: {decision}", Brushes.Black));
                                fileNamesList.Clear();
                                fileNamesList.AddRange(files.Keys);
                                currentIndex = -1; // Reset to -1 before starting file transfers
                                MoveToNextFile(); // Call this method to start processing the first file

                            }

                            else if (metadata?.Type == "EncryptedEphemeralKey")
                            {
                                Console.WriteLine("Processing EncryptedEphemeralKey message");

                                string encryptedKeyBase64 = metadata.EncryptedKey;
                                this.ivBase64 = metadata.IV;
                                Console.WriteLine($"encryptedephemeralKeyIV Base64: {ivBase64} and encryptedEphemeralKeyBase64: {encryptedKeyBase64}");

                                // Convert from Base64
                                byte[] encryptedephemeralKey = Convert.FromBase64String(encryptedKeyBase64);
                                byte[] encryptedephemeralKeyIV = Convert.FromBase64String(ivBase64);

                                //Decrypt the Encrypted Ephemeral key
                                byte[] decryptedEphemeralKey = SecureEphemeralKeyExchange.DecryptEphemeralKey(encryptedephemeralKey, sharedSecret, encryptedephemeralKeyIV);
                                string decryptedEphemeralKeyBase64 = Convert.ToBase64String(decryptedEphemeralKey);
                                Console.WriteLine($"Decrypted ephemeral key(stored):{decryptedEphemeralKeyBase64}");

                                // Securely store the decrypted ephemeral key
                                string keyName = $"_decryptedEphemeralKey_{sender}_{ivBase64}";
                                CryptoUtils.StoreSecurely(decryptedEphemeralKey, keyName);

                                Console.WriteLine($"Decrypted ephemeral key received and stored securely.:{keyName}");

                            }

                        }
                        catch (JsonReaderException)
                        {
                            StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs("Error parsing JSON metadata.", Brushes.Red));
                            Console.WriteLine("Error parsing JSON metadata.");
                        }
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Binary && isFileTransferAccepted)
                {
                    string encryptedFilePath = Path.Combine(defaultDirectory, currentFileName);
                    byte[] fileData = memoryStream.ToArray();
                    await File.WriteAllBytesAsync(encryptedFilePath, fileData);
                    Console.WriteLine($"Encrypted file saved: {encryptedFilePath}");

                    // Immediately decrypt the file
                    DecryptAndSaveFile(encryptedFilePath, currentFileName, sender);

                    receivedFileSizes[currentFileName] += buffer.Length; // Update received bytes for the current file

                    // After successfully saving  a file
                    if (receivedFileSizes[currentFileName] >= fileSizes[currentFileName])
                    {
                        StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs($"File {currentFileName} transfer complete. Total bytes received: {receivedFileSizes[currentFileName]} bytes.", Brushes.Green));
                        Console.WriteLine($"File {currentFileName} transfer complete. Total bytes received: {receivedFileSizes[currentFileName]} bytes.");
                        MoveToNextFile(); // Move to the next file
                    }


                }
            }
        }
        catch (WebSocketException ex) when (ex.InnerException is HttpListenerException)
        {
            StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs("The sender has closed the connection unexpectedly.", Brushes.Red));
            Console.WriteLine("The sender has closed the connection unexpectedly.");
            // Attempt to close the WebSocket connection gracefully
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);

            }
        }
        catch (Exception ex)
        {
            StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs($"An unexpected error occurred: {ex.Message}", Brushes.Red));
            Console.WriteLine($"An unexpected error occurred: {ex.Message}");
            // Handle other exceptions as needed
        }
        finally
        {
            if (webSocket.State != WebSocketState.Closed)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InternalServerError, "An error occurred", CancellationToken.None);
            }
        }
    }
    private int filesReceived = 0;
    private void MoveToNextFile()
    {
        currentIndex++;
        if (currentIndex < fileNamesList.Count)
        {
            currentFileName = fileNamesList[currentIndex];
            StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs($"Moving to receive next file: {currentFileName}", Brushes.Black));
            Console.WriteLine($"Moving to next file: {currentFileName}");
        }
        else
        {
            StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs("All files received.", Brushes.Green));
            Console.WriteLine("All files received.");
        }
    }


    private void DecryptAndSaveFile(string encryptedFilePath, string originalFileName, string sender)
    {
        // Extract IV and encrypted content
        byte[] encryptedContentWithIv = File.ReadAllBytes(encryptedFilePath);
        byte[] iv = new byte[FileCryptoManager.IvSize];
        byte[] encryptedContent = new byte[encryptedContentWithIv.Length - iv.Length];

        Array.Copy(encryptedContentWithIv, 0, iv, 0, iv.Length);
        Array.Copy(encryptedContentWithIv, iv.Length, encryptedContent, 0, encryptedContent.Length);

        string decryptedFilePath = Path.Combine(Path.GetDirectoryName(encryptedFilePath), Path.GetFileNameWithoutExtension(originalFileName));
        string directoryPath = Path.GetDirectoryName(decryptedFilePath);

        Console.WriteLine($"Attempting to save decrypted file to: {decryptedFilePath}");
        if (File.Exists(decryptedFilePath))
        {
            StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs("Warning: Overwriting existing file.", Brushes.Red));
            Console.WriteLine("Warning: Overwriting existing file.");

        }

        string keyName = $"_decryptedEphemeralKey_{sender}_{ivBase64}";
        byte[] encryptionKey = CryptoUtils.RetrieveSecurely(keyName);
        if (encryptionKey == null)
        {
            StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs("Failed to retrieve the encryption key.", Brushes.Red));
            Console.WriteLine("Failed to retrieve the encryption key.");
            return;
        }

        string encryptionKeyBase64 = Convert.ToBase64String(encryptionKey);
        string keyFilePath = Path.Combine($"{keyName}.key");

        Console.WriteLine($"Decrypted ephemeral key(Retrieve):{encryptionKeyBase64}");
        // Perform decryption 
        StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs("Starting decryption process...", Brushes.Green));
        Console.WriteLine("Starting decryption process...");
        Console.WriteLine($"Starting decryption. Output file: {decryptedFilePath}, Key length: {encryptionKey.Length}, IV length: {iv.Length}");

        try
        {
            FileCryptoManager.DecryptFileDirect(encryptedContent, decryptedFilePath, encryptionKey, iv);
            Console.WriteLine($"Decryption successful. File saved: {decryptedFilePath}");
            StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs($"Decryption successful. File saved: {decryptedFilePath}", Brushes.Green));


        }
        catch (Exception ex)
        {
            StatusMessageUpdated?.Invoke(this, new StatusMessageEventArgs($"Exception during decryption or file saving: {ex.Message}", Brushes.Red));
            Console.WriteLine($"Exception during decryption or file saving: {ex.Message}");
        }

        // delete the encrypted file after successful decryption
        File.Delete(encryptedFilePath);
        
        filesReceived++; // Increment for each file decrypted
        if (filesReceived >= TotalFiles)
        {
            OverwriteAndDeleteKey(keyFilePath); // Delete key after last file
        }
    }
    

    private void OverwriteAndDeleteKey(string keyFileName)
    {
        // Correctly build the path to the key file
        string appDataFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string keyFolderPath = Path.Combine(appDataFolder, "SecLinkApp", "Keys");
        string keyFilePath = Path.Combine(keyFolderPath, keyFileName);


        try
        {
            Console.WriteLine($"Attempting to securely overwrite and delete key file: {keyFilePath}");

            // Check if the file exists
            if (File.Exists(keyFilePath))
            {
                // Get the length of the file
                long length = new FileInfo(keyFilePath).Length;


                // Overwrite with random data
                using (var randomData = new RNGCryptoServiceProvider())
                {
                    byte[] data = new byte[length];
                    randomData.GetBytes(data);
                    File.WriteAllBytes(keyFilePath, data);
                }

                // Now delete the file
                File.Delete(keyFilePath);
                Console.WriteLine("Key file securely deleted.");
            }
            else
            {
                Console.WriteLine("Key file does not exist.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to securely delete the key file at {keyFilePath}: {ex.Message}");
        }
    }
}