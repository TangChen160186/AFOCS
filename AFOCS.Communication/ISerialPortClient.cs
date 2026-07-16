namespace AFOCS.Communication
{
    public class SerialPortConfig
    {
        public string PortName { get; set; } = "COM1";
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public StopBitsOption StopBits { get; set; } = StopBitsOption.One;
        public ParityOption Parity { get; set; } = ParityOption.None;
        public int ReadTimeout { get; set; } = 1000;
        public int WriteTimeout { get; set; } = 1000;
        public int ReceiveBufferSize { get; set; } = 4096;
        public string LineEnding { get; set; } = "\r\n";
    }

    public enum StopBitsOption
    {
        None = 0,
        One = 1,
        OnePointFive = 3,
        Two = 2
    }

    public enum ParityOption
    {
        None = 0,
        Odd = 1,
        Even = 2,
        Mark = 3,
        Space = 4
    }

    public interface ISerialPortClient : IDisposable
    {
        bool IsOpen { get; }
        string LineEnding { get; }

        event EventHandler<string>? DataReceived;
        event EventHandler<Exception>? ErrorOccurred;
        event EventHandler? PortClosed;
        event EventHandler? PortOpened;

        Task<bool> OpenAsync(SerialPortConfig config, CancellationToken cancellationToken = default);
        Task CloseAsync();
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
        string[] GetAvailablePorts();
    }
}