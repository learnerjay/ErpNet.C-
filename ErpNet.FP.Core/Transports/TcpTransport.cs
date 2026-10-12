namespace ErpNet.FP.Core.Transports
{
    using System;
    using System.Collections.Generic;
    using System.Net.Sockets;
    using Serilog;

    /// <summary>
    /// TCP/IP transport.
    /// </summary>
    /// <seealso cref="ErpNet.FP.Core.Transport" />
    public class TcpTransport : Transport
    {
        public override string TransportName => "tcp";

        protected readonly int DefaultPort = 9100;

        private readonly IDictionary<string, TcpTransport.Channel?> openedChannels =
            new Dictionary<string, TcpTransport.Channel?>();

        public override IChannel OpenChannel(string address)
        {
			Log.Information(">>> Entering OpenChannel(address={Address})", address);
            if (openedChannels.TryGetValue(address, out Channel? channel))
            {
                if (channel == null)
                {
					Log.Error("Throwing TimeoutException from OpenChannel(address={Address})", address);
                    throw new TimeoutException("disabled due to timeout");
                }
                return channel;
            }
            else
            {
                try
                {
                    var (hostName, port) = ParseAddress(address);
                    channel = new Channel(hostName, port);
                    openedChannels.Add(address, channel);
                    return channel;
                }
                catch (TimeoutException)
                {
                    openedChannels.Add(address, null);
                    throw;
                }
            }
			Log.Debug("<<< Exiting OpenChannel(address={Address})", address);
        }

        public override void Drop(IChannel channel)
        {
			Log.Information(">>> Entering Drop(channel={Channel})", channel);
            ((Channel)channel).Close();
			Log.Debug("<<< Exiting Drop(channel={Channel})", channel);
        }

        protected (string, int) ParseAddress(string address)
        {
			Log.Information(">>> Entering ParseAddress(address={Address})", address);
            var parts = address.Split(':');
            if (parts.Length == 1) return (address, DefaultPort);
			Log.Debug("ParseAddress(address={Address}): parts → {Parts}", address, parts);
            var hostName = parts[0];
            var port = parts.Length > 1 ? int.Parse(parts[1]) : DefaultPort;
			Log.Debug("ParseAddress(address={Address}): port → {Port}", address, port);
			Log.Debug("<<< Exiting ParseAddress(address={Address})", address);
            return (hostName, port);
        }

        public class Channel : IChannel
        {
            private TcpClient tcpClient;
            private NetworkStream netStream;

            private string HostName { get; }
            private int Port { get; }

            public string Descriptor => $"{HostName}:{Port}";

            public Channel(string hostName, int port)
            {
                HostName = hostName;
                Port = port;
                tcpClient = new TcpClient();
                netStream = ConnectAndGetStream();
            }

            protected NetworkStream ConnectAndGetStream()
            {
				Log.Information(">>> Entering ConnectAndGetStream()");
                var __logStart = System.Diagnostics.Stopwatch.StartNew();
                var task = tcpClient.ConnectAsync(HostName, Port);
				Log.Debug("ConnectAndGetStream(): task → {Task}", task);
                if (task.Wait(Math.Max(200, tcpClient.ReceiveTimeout)))
                {
                    return tcpClient.GetStream();
                }
                var errorMessage = $"Timeout occured while connecting to {HostName}:{Port}";
				Log.Debug("ConnectAndGetStream(): errorMessage → {ErrorMessage}", errorMessage);
				Log.Information("ConnectAndGetStream(): external service call took {Arg0} ms", __logStart.ElapsedMilliseconds);
                Log.Error(errorMessage);
				Log.Debug("<<< Exiting ConnectAndGetStream()");
                throw new TimeoutException(errorMessage);
            }

            public void Dispose()
            {
				Log.Information(">>> Entering Dispose()");
                Close();
				Log.Debug("<<< Exiting Dispose()");
            }

            public void Close()
            {
				Log.Information(">>> Entering Close()");
                tcpClient.Close();
                // Closing the tcpClient instance does not close the network stream.
                netStream.Close();
				Log.Debug("<<< Exiting Close()");
            }

            /// <summary>
            /// Reads data from the tcp connection.
            /// </summary>
            /// <returns>The data which was read.</returns>
            public byte[] Read()
            {
				Log.Information(">>> Entering Read()");
                var buffer = new byte[tcpClient.ReceiveBufferSize];
                var task = netStream.ReadAsync(buffer, 0, buffer.Length);
				Log.Debug("Read(): task → {Task}", task);
                if (task.Wait(netStream.ReadTimeout))
                {
                    var result = new byte[task.Result];
					Log.Debug("Read(): result → {Result}", result);
                    Array.Copy(buffer, result, task.Result);
                    return result;
                }
                var errorMessage = $"Timeout occured while reading from tcp connection {HostName}:{Port}";
				Log.Debug("Read(): errorMessage → {ErrorMessage}", errorMessage);
                Log.Error(errorMessage);
				Log.Debug("<<< Exiting Read()");
                throw new TimeoutException(errorMessage);
            }

            private void Reconnect()
            {
				Log.Information(">>> Entering Reconnect()");
                try
                {
                    Close();
                }
                finally
                {
					Log.Debug("<<< Exiting Reconnect()");
                    // Closing TcpClient disposes instance
                    tcpClient = new TcpClient();
                    netStream = ConnectAndGetStream();
					Log.Debug("Reconnect(): netStream → {NetStream}", netStream);
					Log.Debug("Reconnect(): tcpClient → {TcpClient}", tcpClient);
                }
            }

            /// <summary>
            /// Writes the specified data to the tcp connection.
            /// </summary>
            /// <param name="data">The data to write.</param>
            public void Write(byte[] data)
            {
				Log.Information(">>> Entering Write(data={Data})", data);
                if (!tcpClient.Connected)
                {
                    try
                    {
                        Reconnect();
                    } 
                    catch (Exception inner)
                    {
                        var errorMessage = $"Cannot reconnect to {HostName}:{Port}";
						Log.Debug("Write(data={Data}): errorMessage → {ErrorMessage}", data, errorMessage);
                        Log.Error(errorMessage);
                        throw new TimeoutException(errorMessage, inner);
                    }
                }
                var bytesToWrite = data.Length;
                while (bytesToWrite > 0)
                {
                    var writeSize = Math.Min(bytesToWrite, tcpClient.SendBufferSize);
					Log.Debug("Write(data={Data}): writeSize → {WriteSize}", data, writeSize);
                    var task = netStream.WriteAsync(
                        data,
                        data.Length - bytesToWrite,
                        writeSize
                    );
                    if (task.Wait(netStream.WriteTimeout))
                    {
                        bytesToWrite -= writeSize;
                    }
                    else
                    {
                        var errorMessage = $"Timeout occured while writing to tcp connection {HostName}:{Port}";
                        Log.Error(errorMessage);
                        throw new TimeoutException(errorMessage);
                    }
                }
				Log.Debug("<<< Exiting Write(data={Data})", data);
            }
        }

    }
}