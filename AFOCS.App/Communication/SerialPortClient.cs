using System.ComponentModel.Composition;
using System.IO.Ports;
using System.Text;
using Serilog;

namespace AFOCS.App.Communication
{
    [Export(typeof(ISerialPortClient))]
    [method: ImportingConstructor]
    public class SerialPortClient(ILogger logger) : ISerialPortClient, IDisposable
    {
        private SerialPort? _serialPort;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly CancellationTokenSource _readCts = new();
        private Task? _continuousReadTask;
        private bool _disposed;
        private readonly Encoding _encoding = Encoding.ASCII;

        private readonly StringBuilder _receiveBuffer = new();
        private readonly Lock _bufferLock = new();
        private TaskCompletionSource<string>? _responseTcs;
        private string? _currentTerminator;

        public bool IsOpen => _serialPort?.IsOpen ?? false;
        public string LineEnding { get; private set; } = "\r\n";

        public event EventHandler<string>? DataReceived;
        public event EventHandler<Exception>? ErrorOccurred;
        public event EventHandler? PortClosed;
        public event EventHandler? PortOpened;

        public async Task<bool> OpenAsync(SerialPortConfig config, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                if (IsOpen)
                {
                    logger?.Warning("串口已经打开，请先关闭当前连接");
                    return false;
                }

                _serialPort = new SerialPort
                {
                    PortName = config.PortName,
                    BaudRate = config.BaudRate,
                    DataBits = config.DataBits,
                    StopBits = (StopBits)config.StopBits,
                    Parity = (Parity)config.Parity,
                    ReadTimeout = config.ReadTimeout,
                    WriteTimeout = config.WriteTimeout,
                    ReadBufferSize = config.ReceiveBufferSize,
                    Handshake = Handshake.None,
                    DtrEnable = false,
                    RtsEnable = false,
                    Encoding = _encoding
                };

                LineEnding = config.LineEnding;

                _serialPort.Open();
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();

                lock (_bufferLock)
                {
                    _receiveBuffer.Clear();
                }

                _continuousReadTask = ContinuousReadAsync(_readCts.Token);

                logger?.Information($"串口 {config.PortName} 打开成功，结束符: {EscapeLineEnding(LineEnding)}");
                PortOpened?.Invoke(this, EventArgs.Empty);

                return true;
            }
            catch (Exception ex)
            {
                logger?.Error(ex, $"打开串口 {config.PortName} 失败");
                ErrorOccurred?.Invoke(this, ex);
                _serialPort?.Dispose();
                _serialPort = null;
                return false;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task CloseAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!IsOpen) return;

                _readCts.Cancel();

                if (_continuousReadTask != null)
                {
                    await Task.WhenAny(_continuousReadTask, Task.Delay(2000)).ConfigureAwait(false);
                }

                _serialPort?.Close();
                _serialPort?.Dispose();
                _serialPort = null;

                logger?.Information("串口已关闭");
                PortClosed?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "关闭串口时发生错误");
                ErrorOccurred?.Invoke(this, ex);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task ContinuousReadAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested && IsOpen)
                {
                    try
                    {
                        if (_serialPort!.BytesToRead > 0)
                        {
                            var buffer = new byte[_serialPort.BytesToRead];
                            var bytesRead = await _serialPort.BaseStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);

                            if (bytesRead > 0)
                            {
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
                        }
                        else
                        {
                            await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                        }
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

        public async Task<int> WriteAsync(string text, CancellationToken cancellationToken = default)
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException("串口未打开");
            }

            await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var data = _encoding.GetBytes(text);
                await _serialPort!.BaseStream.WriteAsync(data, 0, data.Length, cancellationToken).ConfigureAwait(false);
                await _serialPort.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);

                logger?.Debug($"发送数据: {data.Length} 字节 - {EscapeText(text)}");
                return data.Length;
            }
            catch (Exception ex)
            {
                logger?.Error(ex, "发送数据失败");
                ErrorOccurred?.Invoke(this, ex);
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
            if (!IsOpen)
            {
                throw new InvalidOperationException("串口未打开");
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
            if (!IsOpen) return;

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                _serialPort?.DiscardInBuffer();
                _serialPort?.DiscardOutBuffer();

                lock (_bufferLock)
                {
                    _receiveBuffer.Clear();
                }

                logger?.Debug("串口缓冲区已清空");
            }
            finally
            {
                _lock.Release();
            }
        }

        public string[] GetAvailablePorts()
        {
            return SerialPort.GetPortNames();
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
            return text.Replace("\r", "\\r").Replace("\n", "\\n\\r").Replace("\t", "\\t");
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;
            _readCts.Cancel();
            _readCts.Dispose();

            _serialPort?.Close();
            _serialPort?.Dispose();
            _serialPort = null;

            _lock.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}