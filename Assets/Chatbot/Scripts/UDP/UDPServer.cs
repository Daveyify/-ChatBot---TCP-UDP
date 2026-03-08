using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class UDPServer : MonoBehaviour, IServer
{
    private UdpClient udpServer; // UDP client to handle network communication
    private IPEndPoint remoteEndPoint; // Endpoint to identify the remote client

    public event Action<string> OnMessageReceived;
    public event Action OnConnected;
    public event Action OnDisconnected;

    public bool isServerRunning { get; private set; }

    public Task StartServer(int port)
    {
        udpServer = new UdpClient(port);
        Debug.Log("[Server] Server started. Waiting for messages...");
        isServerRunning = true;

        _ = ReceiveLoop();
        return Task.CompletedTask;
    }

    private async Task ReceiveLoop()
    {
        try
        {
            while (isServerRunning)
            {
                UdpReceiveResult result = await udpServer.ReceiveAsync();// Waits for incoming messages from the server asynchronously
                string message = Encoding.UTF8.GetString(result.Buffer); // Converts the received bytes into a string message using UTF-8 encoding

                if (message == "CONNECT")
                {
                    Debug.Log("[Server] Client connected: " + result.RemoteEndPoint);
                    remoteEndPoint = result.RemoteEndPoint;
                    await SendMessageAsync("CONNECTED"); // Sends a welcome message back to the client to confirm the handshake
                    OnConnected?.Invoke(); // Invokes the OnConnected event, notifying any subscribed listeners that a client has connected
                    continue; // Skip the rest of the loop and wait for the next message
                }

                Debug.Log("[Server] Received: " + message);
                OnMessageReceived?.Invoke(message);//Invokes the OnMessageReceived event, passing the received message to any subscribed listeners
            }
        }
        finally
        {
            Disconnect();
        }
    }

    public async Task SendMessageAsync(string message)
    {
        if (!isServerRunning) // Checks if there is an active connection to the server
        {
            Debug.Log("[Server] The server isn't running");
            return;
        }

        byte[] data = Encoding.UTF8.GetBytes(message);// Converts the message string into a byte array
        await udpServer.SendAsync(data, data.Length, remoteEndPoint); // Sends the byte array to the server using UDP asynchronously

        Debug.Log("[Server] Sent: " + message);
    }


    public void Disconnect()
    {
        if (!isServerRunning)
        {
            Debug.Log("[Server] The server is not running");
            return;
        }

        isServerRunning = false;

        udpServer?.Close();
        udpServer?.Dispose();// Closes the UDP client and releases any resources associated with it
        udpServer = null;

        Debug.Log("[Server] Disconnected");
        OnDisconnected?.Invoke();// Invokes the OnDisconnected event, notifying any subscribed listeners that the client has disconnected from the server

    }

    private async void OnDestroy()
    {
        Disconnect();
        await Task.Delay(100);
    }
}