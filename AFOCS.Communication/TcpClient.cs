using System.ComponentModel.Composition;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace AFOCS.Communication
{
    [Export(typeof(ITcpClient))]
    [PartCreationPolicy(CreationPolicy.NonShared)] // 通常与 NonShared 配合使用
    [method: ImportingConstructor]
    public class TcpClient(ILogger logger) : ITcpClient, IDisposable
    {
        private System.Net.Sockets.TcpClient? _tcpClient;
        private NetworkStream? _networkStream;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private CancellationTokenSource? _readCts;
        private CancellationTokenSource? _reconnectCts;
        private Task? _continuousReadTask;
        private Task? _reconnectTask;
        private bool _disposed;
        private bool _isManualDisconnect;
        private readonly Encoding _encoding = Encoding.ASCII;

        private readonly StringBuilder _receiveBuffer = new();
        private readonly Lock _bufferLock = new();
        private TaskCompletionSource<string>? _responseTcs;
        private string? _currentTerminator;

        private TcpClientConfig? _config;
        private int _reconnectAttempts;

        public bool IsConnected => _tcpClient?.Connected ?? false;
        public string RemoteEndPoint => _tcpClient?.Client?.RemoteEndPoint?.ToString() ?? "Not Connected";
        public string LineEnding { get; private set; } = "\r\n";

        public event EventHandler<string>? DataReceived;
        public event EventHandler<Exception>? ErrorOccurred;
        public event EventHandler? Disconnected;
        public event EventHandler? Connected;
        public event EventHandler<int>? Reconnecting;

        public async Task<bool> ConnectAsync(TcpClientConfig config, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (IsConnected)
                {
                    logger?.Warning("TCP 客户端已连接，请先断开当前连接");
                    return false;
                }

                _config = config;
                _isManualDisconnect = false;
                LineEnding = config.LineEnding;

                _tcpClient = new System.Net.Sockets.TcpClient
                {
                    ReceiveBufferSize = config.ReceiveBufferSize,
                    SendTimeout = config.WriteTimeout,
                    ReceiveTimeout = config.ReadTimeout,
                    NoDelay = true,
                    LingerState = new LingerOption(true, 0)
                };

                var connectTask = _tcpClient.ConnectAsync(config.IpAddress, config.Port);
                var timeoutTask = Task.Delay(10000, cancellationToken);

                if (await Task.WhenAny(connectTask, timeoutTask).ConfigureAwait(false) == timeoutTask)
                {
                    _tcpClient?.Close();
                    _tcpClient?.Dispose();
                    _tcpClient = null;

                    logger?.Error($"连接 {config.IpAddress}:{config.Port} 超时");
                    throw new TimeoutException($"连接超时 ({config.ConnectTimeout}ms)");
                }

                await connectTask.ConfigureAwait(false);

                _networkStream = _tcpClient.GetStream();

                _readCts = new CancellationTokenSource();
                _reconnectCts = new CancellationTokenSource();

                lock (_bufferLock)
                {
                    _receiveBuffer.Clear();
                }

                _continuousReadTask = ContinuousReadAsync(_readCts.Token);

                logger?.Information($"已连接到 {config.IpAddress}:{config.Port}，结束符: {EscapeLineEnding(LineEnding)}");
                Connected?.Invoke(this, EventArgs.Empty);

                return true;
            }
            catch (Exception ex)
            {
                logger?.Error(ex, $"连接 {config.IpAddress}:{config.Port} 失败，{ex.Message}");
                ErrorOccurred?.Invoke(this, ex);

                CleanupConnection();
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!IsConnected) return;

                _isManualDisconnect = true;

                StopBackgroundTasks();

                CleanupConnection();

                logger?.Information("TCP 连接已断开");
                Disconnected?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "断开连接时发生错误");
                ErrorOccurred?.Invoke(this, ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        private void StopBackgroundTasks()
        {
            _readCts?.Cancel();
            _reconnectCts?.Cancel();

            if (_continuousReadTask != null)
            {
                Task.WhenAny(_continuousReadTask, Task.Delay(2000)).Wait();
            }

            if (_reconnectTask != null)
            {
                Task.WhenAny(_reconnectTask, Task.Delay(1000)).Wait();
            }
        }

        private void CleanupConnection()
        {
            _networkStream?.Close();
            _networkStream?.Dispose();
            _networkStream = null;

            _tcpClient?.Close();
            _tcpClient?.Dispose();
            _tcpClient = null;

            _readCts?.Dispose();
            _reconnectCts?.Dispose();

            _readCts = null;
            _reconnectCts = null;
        }

        private async Task ContinuousReadAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && IsConnected && _networkStream != null)
                {
                    try
                    {
                        if (_networkStream.DataAvailable)
                        {
                            var buffer = new byte[4096];
                            var bytesRead = await _networkStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);

                            if (bytesRead == 0)
                            {
                                logger?.Warning("连接已关闭（接收到 0 字节）");
                                await HandleDisconnectionAsync().ConfigureAwait(false);
                                break;
                            }

                            var receivedText = _encoding.GetString(buffer, 0, bytesRead);

                            lock (_bufferLock)
                            {
                                _receiveBuffer.Append(receivedText.TrimStart());

                                if (_responseTcs != null && !_responseTcs.Task.IsCompleted && _currentTerminator != null)
                                {
                                    var terminatorIndex = _receiveBuffer.ToString().IndexOf(_currentTerminator, StringComparison.Ordinal);
                                    if (terminatorIndex >= 0)
                                    {
                                        var responseText = _receiveBuffer.ToString(0, terminatorIndex);
                                        _receiveBuffer.Remove(0, terminatorIndex + _currentTerminator.Length);
                                        _responseTcs.TrySetResult(responseText);
                                    }
                                }
                            }

                            logger?.Debug($"接收到数据: {bytesRead} 字节 - {EscapeText(receivedText)}");
                            DataReceived?.Invoke(this, receivedText);
                        }
                        else
                        {
                            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                        }
                    }
                    catch (IOException ex)
                    {
                        logger?.Warning(ex, "网络流读取异常，连接可能已断开");
                        await HandleDisconnectionAsync().ConfigureAwait(false);
                        break;
                    }
                    catch (SocketException ex)
                    {
                        logger?.Error(ex, "Socket 异常");
                        await HandleDisconnectionAsync().ConfigureAwait(false);
                        break;
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        logger?.Error(ex, "读取数据时发生错误");
                        ErrorOccurred?.Invoke(this, ex);
                        await Task.Delay(100, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "连续读取任务发生严重错误");
                ErrorOccurred?.Invoke(this, ex);
            }
        }

        private async Task HandleDisconnectionAsync()
        {
            if (_isManualDisconnect) return;

            lock (_bufferLock)
            {
                _receiveBuffer.Clear();
                _responseTcs?.TrySetException(new IOException("连接已断开"));
                _responseTcs = null;
                _currentTerminator = null;
            }

            CleanupConnection();

            Disconnected?.Invoke(this, EventArgs.Empty);

            if (_config?.AutoReconnect == true && !_isManualDisconnect)
            {
                _reconnectAttempts = 0;
                _reconnectTask = AutoReconnectAsync();
            }
        }

        private async Task AutoReconnectAsync()
        {
            while (_config?.AutoReconnect == true && !_isManualDisconnect && !_disposed)
            {
                if (_reconnectAttempts >= _config!.MaxReconnectAttempts)
                {
                    logger?.Error($"自动重连失败，已达到最大尝试次数 ({_config.MaxReconnectAttempts})");
                    ErrorOccurred?.Invoke(this, new Exception($"自动重连失败，已尝试 {_reconnectAttempts} 次"));
                    break;
                }

                _reconnectAttempts++;
                Reconnecting?.Invoke(this, _reconnectAttempts);

                logger?.Information($"尝试自动重连 (第 {_reconnectAttempts} 次)...");

                await Task.Delay(_config!.ReconnectInterval).ConfigureAwait(false);

                try
                {
                    if (await ConnectAsync(_config).ConfigureAwait(false))
                    {
                        logger?.Information("自动重连成功");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    logger?.Warning(ex, $"自动重连失败 (第 {_reconnectAttempts} 次)");
                }
            }
        }

        public async Task<int> WriteAsync(string text, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || _networkStream == null)
            {
                throw new InvalidOperationException("TCP 客户端未连接");
            }

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var data = _encoding.GetBytes(text);
                await _networkStream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
                await _networkStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                logger?.Debug($"发送数据: {data.Length} 字节 - {EscapeText(text)}");
                return data.Length;
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "发送数据失败");
                ErrorOccurred?.Invoke(this, ex);

                if (ex is IOException || ex is SocketException)
                {
                    await HandleDisconnectionAsync().ConfigureAwait(false);
                }

                throw;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<int> WriteLineAsync(string text, CancellationToken cancellationToken = default)
        {
            return await WriteAsync(text + LineEnding, cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> ReadUntilAsync(string terminator, int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            if (!IsConnected || _networkStream == null)
            {
                throw new InvalidOperationException("TCP 客户端未连接");
            }

            if (string.IsNullOrEmpty(terminator))
            {
                throw new ArgumentException("结束符不能为空", nameof(terminator));
            }

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                lock (_bufferLock)
                {
                    _currentTerminator = terminator;
                    _responseTcs = new TaskCompletionSource<string>();
                }

                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(timeoutMs);

                try
                {
                    var response = await _responseTcs.Task.ConfigureAwait(false);
                    logger?.Debug($"接收到响应: {response.Length} 字符 - {EscapeText(response)}");
                    return response;
                }
                catch (OperationCanceledException)
                {
                    logger?.Warning($"等待响应超时 ({timeoutMs}ms)");
                    throw new TimeoutException($"在 {timeoutMs}ms 内未收到以 {EscapeLineEnding(terminator)} 结尾的响应");
                }
            }
            finally
            {
                lock (_bufferLock)
                {
                    _responseTcs = null;
                    _currentTerminator = null;
                }
                _lock.Release();
            }
        }

        public async Task<string> ReadLineAsync(int timeoutMs = 5000, CancellationToken cancellationToken = default)
        {
            return await ReadUntilAsync(LineEnding, timeoutMs, cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> SendAndReceiveAsync(
            string command,
            int timeoutMs = 3000,
            CancellationToken cancellationToken = default)
        {
            await WriteLineAsync(command, cancellationToken).ConfigureAwait(false);
            return await ReadLineAsync(timeoutMs, cancellationToken).ConfigureAwait(false);
        }

        public async Task<string> SendAndReceiveAsync(
            string command,
            string terminator,
            int timeoutMs = 5000,
            CancellationToken cancellationToken = default)
        {
            await WriteLineAsync(command, cancellationToken).ConfigureAwait(false);
            return await ReadUntilAsync(terminator, timeoutMs, cancellationToken).ConfigureAwait(false);
        }

        public async Task FlushAsync()
        {
            if (!IsConnected) return;

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                lock (_bufferLock)
                {
                    _receiveBuffer.Clear();
                }

                logger?.Debug("接收缓冲区已清空");
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> PingAsync(int timeoutMs = 1000)
        {
            if (!IsConnected) return false;

            try
            {
                var response = await SendAndReceiveAsync("PING", timeoutMs).ConfigureAwait(false);
                return !string.IsNullOrEmpty(response);
            }
            catch
            {
                return false;
            }
        }

        private string EscapeLineEnding(string lineEnding)
        {
            return lineEnding switch
            {
                "\r\n" => "\\r\\n",
                "\r" => "\\r",
                "\n" => "\\n",
                _ => $"\"{lineEnding}\""
            };
        }

        private string EscapeText(string text)
        {
            return text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _isManualDisconnect = true;

            StopBackgroundTasks();
            CleanupConnection();

            _lock.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}