using System.Threading.Tasks;

public interface IServer : IChatConnection
{
    public bool isServerRunning { get; }
    public Task StartServer(int port);
}
