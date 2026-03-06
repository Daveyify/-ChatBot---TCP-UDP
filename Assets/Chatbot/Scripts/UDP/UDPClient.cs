using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class UDPClient : MonoBehaviour, IClient
{
    private UdpClient udpClient;
    private IPEndPoint remoteEndPoint;

    public event Action<string> OnMessageReceived;
    public event Action OnConnected;
    public event Action OnDisconnected;

    public bool isConnected { get; private set; }

    public async Task ConnectToServer(string ipAddress, int port)
    {
        udpClient = new UdpClient();
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);

        isConnected = true;
        _ = ReceiveLoop();

        await SendMessageAsync("CONNECT");
    }

    private async Task ReceiveLoop()
    {
        try
        {
            while (isConnected)
            {
                UdpReceiveResult result = await udpClient.ReceiveAsync();
                string message = Encoding.UTF8.GetString(result.Buffer);

                if (message == "CONNECTED")
                {
                    Debug.Log("[Client] Server Answered");
                    OnConnected?.Invoke();
                    continue;
                }

                Debug.Log("[Client] Received: " + message);
                OnMessageReceived?.Invoke(message);
            }
        }
        catch (ObjectDisposedException)
        {
            // El cliente fue cerrado intencionalmente, no es un error
            Debug.Log("[Client] ReceiveLoop detenido (cliente cerrado).");
        }
        catch (SocketException ex)
        {
            // Error de red real — solo desconectar si seguía conectado
            if (isConnected)
            {
                Debug.LogWarning("[Client] SocketException: " + ex.Message);
                Disconnect();
            }
        }
        catch (Exception ex)
        {
            // Cualquier otro error inesperado
            if (isConnected)
            {
                Debug.LogWarning("[Client] Error inesperado en ReceiveLoop: " + ex.Message);
                Disconnect();
            }
        }
    }

    public async Task SendMessageAsync(string message)
    {
        if (!isConnected)
        {
            Debug.Log("[Client] Not connected to server.");
            return;
        }

        byte[] data = Encoding.UTF8.GetBytes(message);
        await udpClient.SendAsync(data, data.Length, remoteEndPoint);

        Debug.Log("[Client] Sent: " + message);
    }

    public void Disconnect()
    {
        if (!isConnected)
        {
            Debug.Log("[Client] The client is not connected");
            return;
        }

        isConnected = false;

        udpClient?.Close();
        udpClient?.Dispose();
        udpClient = null;

        Debug.Log("[Client] Disconnected");
        OnDisconnected?.Invoke();
    }

    private void OnDestroy()
    {
        Disconnect();
    }
}