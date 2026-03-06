using System;
using System.Threading.Tasks;

public interface IChatConnection
{
    event Action<string> OnMessageReceived;
    event Action OnConnected;
    event Action OnDisconnected;

    public Task SendMessageAsync(string message);
    public void Disconnect();
}
