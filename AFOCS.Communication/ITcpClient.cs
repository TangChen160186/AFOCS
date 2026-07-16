namespace AFOCS.Communication
{
    public class TcpClientConfig
    {
        public string IpAddress { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 8000;
        public int ConnectTimeout { get; set; } = 5000;
        public int ReadTimeout { get; set; } = 3000;
        public int WriteTimeout { get; set; } = 3000;
        public int ReceiveBufferSize { get; set; } = 8192;
        public bool AutoReconnect { get; set; } = false;
        public int ReconnectInterval { get; set; } = 3000;
        public int MaxReconnectAttempts { get; set; } = 5;
        public string LineEnding { get; set; } = "\r\n";
    }

    public interface ITcpClient : IDisposable
    {
        bool IsConnected { get; }
        string RemoteEndPoint { get; }
        string LineEnding { get; }

        event EventHandler<string>? DataReceived;
        event EventHandler<Exception>? ErrorOccurred;
        event EventHandler? Disconnected;
        event EventHandler? Connected;
        event EventHandler<int>? Reconnecting;

        Task<bool> ConnectAsync(TcpClientConfig config, CancellationToken cancellationToken = default);
        Task DisconnectAsync();
        Task<int> WriteAsync(string text, CancellationToken cancellationToken = default);
        Task<int> WriteLineAsync(string text, CancellationToken cancellationToken = default);
        Task<string> ReadUntilAsync(string terminator, int timeoutMs = 5000, CancellationToken cancellationToken = default);
        Task<string> ReadLineAsync(int timeoutMs = 5000, CancellationToken cancellationToken = default);
        Task<string> SendAndReceiveAsync(
            string command,
            int timeoutMs = 3000,
            CancellationToken cancellationToken = default);
        Task<string> SendAndReceiveAsync(
            string command,
            string terminator,
            int timeoutMs = 5000,
            CancellationToken cancellationToken = default);
        Task FlushAsync();
        Task<bool> PingAsync(int timeoutMs = 1000);
    }
}