namespace ErpNet.FP.Core.Drivers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Serilog;

    public partial class BgZfpFiscalPrinter : BgFiscalPrinter
    {
        private static readonly Random random = new Random();
        protected const byte
            MarkerSpace = 0x20,
            MarkerNACK = 0x15,
            MarkerRETRY = 0x0e,
            MarkerSTX = 0x02,
            MarkerACK = 0x06,
            MarkerETX = 0x0A;
        protected const byte
            PingAnswerDeviceReady = 0x40,
            PingAnswerDeviceBusy = 0x41,
            PingAnswerErrorOutOfPaper = 0x42,
            PingAnswerErrorOutOfPaperAndBusy = 0x43,
            PingAnswerErrorOverheated = 0x44,
            PingAnswerErrorOverheatedAndBusy = 0x45,
            PingAnswerErrorMissingExternalDisplay = 0x48,
            PingAnswerErrorMissingExternalDisplayAndBusy = 0x49,
            PingAnswerErrorWaitingForPassword = 0x50,
            PingAnswerErrorBusyWithAnotherConnection = 0x60,
            PingAnswerErrorWrongPassword = 0x70,
            SpecialCommandPing = 0x09;
        protected const byte MaxSequenceNumber = 0x9F - MarkerSpace;
        protected const byte MaxWriteRetries = 3;
        protected const byte MaxReadRetries = 200;
        // Printing a fiscal memory report for a long period keeps the device busy for minutes.
        protected const int MaxReadRetriesLongRunning = 3000;
        protected byte FrameSequenceNumber = (byte)random.Next(0, MaxSequenceNumber - 1);

        protected virtual byte[] ByteTo2Bytes(UInt16 word)
        {
			Log.Debug(">>> Entering ByteTo2Bytes(word={Word})", word);
            return new byte[]{
                (byte)((word >> 4 & 0x0f) + 0x30),
                (byte)((word >> 0 & 0x0f) + 0x30)
            };
        }
        protected virtual byte[] ComputeCS(byte[] fragment)
        {
			Log.Debug(">>> Entering ComputeCS(fragment={Fragment})", fragment);
            byte bccSum = 0;
            foreach (byte b in fragment)
            {
                bccSum ^= b;
            }
			Log.Debug("<<< Exiting ComputeCS(fragment={Fragment})", fragment);
            return ByteTo2Bytes(bccSum);
        }

        protected virtual byte[] BuildHostFrame(byte command, byte[]? data)
        {
			Log.Debug(">>> Entering BuildHostFrame(command={Command},data={Data})", command, data);
            // Frame header
            var frame = new List<byte>
            {
                MarkerSTX,
                (byte)(MarkerSpace + 3 + (data != null ? data.Length : 0)),
                (byte)(MarkerSpace + FrameSequenceNumber),
                command
            };

            // Frame data
            if (data != null)
            {
                frame.AddRange(data);
            }

            // Frame footer
            frame.AddRange(ComputeCS(frame.Skip(1).ToArray()));
            frame.Add(MarkerETX);

			Log.Debug("<<< Exiting BuildHostFrame(command={Command},data={Data})", command, data);
            return frame.ToArray();
        }

        protected void WaitForDeviceToBeReady()
        {
			Log.Debug(">>> Entering WaitForDeviceToBeReady()");
            var deviceDescriptor = string.IsNullOrEmpty(DeviceInfo.Uri) ? Channel.Descriptor : DeviceInfo.Uri;

			Log.Debug("WaitForDeviceToBeReady(): deviceDescriptor → {DeviceDescriptor}", deviceDescriptor);
            for (; ; )
            {
                byte[]? buffer = null;
                for (var w = 0; w < MaxWriteRetries; w++)
                {
                    Log.Information($"{deviceDescriptor} <<< Ping: {SpecialCommandPing:X}");
                    try
                    {
                        Channel.Write(new byte[] { SpecialCommandPing });
                    }
                    catch (TimeoutException)
                    {
                        throw;      // nothing to do
                    }
                    catch (Exception ex)
                    {
                        Log.Information($"{deviceDescriptor} Cannot write to channel: {ex.Message}");
                        throw;  // nothing to do
                    }
                    try
                    {
                        buffer = Channel.Read();
                        Log.Information($"{deviceDescriptor} >>> Pong: {BitConverter.ToString(buffer)}");
						Log.Debug("WaitForDeviceToBeReady(): buffer → {Buffer}", buffer);
                        break;
                    }
                    catch (TimeoutException)
                    {
                        // When the device is too busy and cannot even answer ping
                        // It could be read timeout, so we will ping again, until
                        // MaxWriteRetries is exausted.
                        Log.Information("Timeout, try again!");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Log.Information($"{deviceDescriptor} Cannot read from channel: {ex.Message}");
                        continue;
                    }
                }
                if (buffer == null || buffer.Length == 0)
                {
					Log.Warning("Throwing TimeoutException from WaitForDeviceToBeReady()");
                    throw new TimeoutException("ping timeout");
                }
                var b = buffer[0];
                switch (b)
                {
                    case PingAnswerDeviceReady:
                        // Device is ready to receive commands, so we will return
                        return;
                    case PingAnswerDeviceBusy:
                        continue; // continue with the loop waiting to be Ready
                    case PingAnswerErrorOutOfPaper:
                    case PingAnswerErrorOutOfPaperAndBusy:
						Log.Warning("Throwing StandardizedStatusMessageException from WaitForDeviceToBeReady()");
                        throw new StandardizedStatusMessageException("Out of paper", "E301");
                    case PingAnswerErrorOverheated:
                    case PingAnswerErrorOverheatedAndBusy:
                        throw new StandardizedStatusMessageException("Overheated", "E304");
                    case PingAnswerErrorMissingExternalDisplay:
                    case PingAnswerErrorMissingExternalDisplayAndBusy:
                        throw new StandardizedStatusMessageException("External display is required and missing", "E106");
                    case PingAnswerErrorBusyWithAnotherConnection:
                        throw new StandardizedStatusMessageException("Busy with another connection", "E108");
                    case PingAnswerErrorWaitingForPassword:
                        throw new StandardizedStatusMessageException("Waiting for password", "E408");
                    case PingAnswerErrorWrongPassword:
                        throw new StandardizedStatusMessageException("Wrong password", "E408");
                    default:
                        // Unknown ping reply code. Break the loop.
						Log.Warning("Throwing InvalidResponseException from WaitForDeviceToBeReady()");
                        throw new InvalidResponseException("invalid ping reply code");
                }
            }
			Log.Debug("<<< Exiting WaitForDeviceToBeReady()");
            throw new TimeoutException("ping timeout");
        }

        protected virtual byte[]? RawRequest(byte command, byte[]? data)
        {
			Log.Debug(">>> Entering RawRequest(command={Command},data={Data})", command, data);
            var deviceDescriptor = string.IsNullOrEmpty(DeviceInfo.Uri) ? Channel.Descriptor : DeviceInfo.Uri;

			Log.Debug("RawRequest(command={Command},data={Data}): deviceDescriptor → {DeviceDescriptor}", command, data, deviceDescriptor);
            FrameSequenceNumber++;
            if (FrameSequenceNumber > MaxSequenceNumber)
            {
                FrameSequenceNumber = 0;
				Log.Debug("RawRequest(command={Command},data={Data}): FrameSequenceNumber → {FrameSequenceNumber}", command, data, FrameSequenceNumber);
            }
            // Wait for device to be ready
            // It will generate exception if there is read timeout
            try
            {
                WaitForDeviceToBeReady();
            }
            catch (Exception ex)
            {
                Log.Information($"{deviceDescriptor} Error while waiting device to be ready: {ex.Message}");
                return null;
            }

            var request = BuildHostFrame(command, data);
			Log.Debug("RawRequest(command={Command},data={Data}): request → {Request}", command, data, request);
            for (var w = 0; w < MaxWriteRetries; w++)
            {
                // Write request frame
                Log.Information($"{deviceDescriptor} <<< {BitConverter.ToString(request)}");
                try
                {
                    Channel.Write(request);
                }
                catch (Exception ex)
                {
                    Log.Information($"{deviceDescriptor} Cannot write to channel: {ex.Message}");
                    throw;  // nothing to do
                }

                // Read response frames.
                var currentFrame = new List<byte>();
				Log.Debug("RawRequest(command={Command},data={Data}): currentFrame → {CurrentFrame}", command, data, currentFrame);
                var maxReadRetries = IsLongRunningCommand(command) ? MaxReadRetriesLongRunning : MaxReadRetries;
				Log.Debug("RawRequest(command={Command},data={Data}): maxReadRetries → {MaxReadRetries}", command, data, maxReadRetries);
                for (var r = 0; r < maxReadRetries; r++)
                {
                    byte[] buffer;

                    try
                    {
                        buffer = Channel.Read();
                    }
                    catch (Exception ex)
                    {
                        Log.Information($"{deviceDescriptor} Cannot read from channel: {ex.Message}");
                        return null;
                    }

                    // For debugging purposes only.
                    Log.Information($"{deviceDescriptor} >>> {BitConverter.ToString(buffer)}");

                    var readFrames = new List<List<byte>>();
					Log.Debug("RawRequest(command={Command},data={Data}): readFrames → {ReadFrames}", command, data, readFrames);
                    foreach (var b in buffer)
                    {
                        currentFrame.Add(b);
                        // Split buffer by following separators
                        if (b == MarkerNACK || b == MarkerRETRY || b == MarkerETX)
                        {
                            readFrames.Add(currentFrame);
                            currentFrame = new List<byte>();
                        }
                    }
                    var (wait, repeat) = (false, false);
                    foreach (var frame in readFrames)
                    {
                        switch (frame[0])
                        {
                            case MarkerNACK:
                                // Only last non-packed frame matters if there are many readed
                                // So change the state accordingly
                                (wait, repeat) = (false, true);
                                break;
                            case MarkerRETRY:
                                // Only last non-packed frame matters if there are many readed
                                // So change the state accordingly
                                (wait, repeat) = (true, false);
                                break;
                            case MarkerACK:
                                // By the protocol, it is allowed only one ack frame response per request.
                                // So if there is no error, we will wait for data frame
                                return frame.ToArray();
                            case MarkerSTX:
                                // By the protocol, it is allowed only one data frame response per request.
                                // So return first occurence of packed frame as response.
                                return frame.ToArray();
                        }
                    }
                    if (wait)
                    {
                        // The FiscalPrinter is still not ready, so make another read
                        continue;
                    }
                    if (repeat)
                    {
                        // The FiscalPrinter cannot answer, so make the request again
                        break;
                    }
                    if (buffer.Count() > 0 && buffer[0] == 0)
                    {
						Log.Warning("Throwing InvalidResponseException from RawRequest(command={Command},data={Data})", command, data);
                        throw new InvalidResponseException("The response is invalid. Probably the communication speed not match or the device is not a fiscal device!");
                    }
                }
            }
			Log.Debug(">>> Entering Request(command={Command},data={Data})", command, data);
			Log.Debug("<<< Exiting Request(command={Command},data={Data})", command, data);
			Log.Debug("<<< Exiting RawRequest(command={Command},data={Data})", command, data);
            return null;
        }

        protected virtual (string, DeviceStatus) ParseResponse(byte[]? rawResponse)
        {
			Log.Debug(">>> Entering ParseResponse(rawResponse={RawResponse})", rawResponse);
            var (response, status) = ParseResponseAsByteArray(rawResponse);
            if (response == null)
            {
			Log.Debug("<<< Exiting ParseResponse(rawResponse={RawResponse})", rawResponse);
                return ("", status);
            }
            return (PrinterEncoding.GetString(response), status);
        }

        protected virtual (byte[]?, DeviceStatus) ParseResponseAsByteArray(byte[]? rawResponse)
        {
			Log.Debug(">>> Entering ParseResponseAsByteArray(rawResponse={RawResponse})", rawResponse);
            if (rawResponse == null)
            {
				Log.Warning("Throwing InvalidResponseException from ParseResponseAsByteArray(rawResponse={RawResponse})", rawResponse);
                throw new InvalidResponseException("no response");
            }
            var ackMode = true;
			Log.Debug("ParseResponseAsByteArray(rawResponse={RawResponse}): ackMode → {AckMode}", rawResponse, ackMode);
            var (dataStartPos, dataEndPos) = (0u, 0u);
            for (var i = 0u; i < rawResponse.Length; i++)
            {
                var b = rawResponse[i];
                switch (b)
                {
                    case MarkerACK:
                        dataStartPos = i;
                        ackMode = true;
						Log.Debug("ParseResponseAsByteArray(rawResponse={RawResponse}): dataStartPos → {DataStartPos}", rawResponse, dataStartPos);
                        break;
                    case MarkerSTX:
                        dataStartPos = i;
                        ackMode = false;
                        break;
                    case MarkerETX:
                        dataEndPos = i;
						Log.Debug("ParseResponseAsByteArray(rawResponse={RawResponse}): dataEndPos → {DataEndPos}", rawResponse, dataEndPos);
                        break;
                }
            }

            if (ackMode)
            {
                // Parse ack frame
                var checkSumPos = dataEndPos - 2u;
                var (msgStartPos, msgEndPos) = (dataStartPos + 2u, checkSumPos);
                var data = rawResponse.Slice(msgStartPos, msgEndPos);
				Log.Debug("ParseResponseAsByteArray(rawResponse={RawResponse}): data → {Data}", rawResponse, data);
                var cs = rawResponse.Slice(checkSumPos, dataEndPos);
				Log.Debug("ParseResponseAsByteArray(rawResponse={RawResponse}): cs → {Cs}", rawResponse, cs);
                var computedCS = ComputeCS(rawResponse.Slice(dataStartPos + 1u, msgEndPos));
				Log.Debug("ParseResponseAsByteArray(rawResponse={RawResponse}): computedCS → {ComputedCS}", rawResponse, computedCS);
                if (cs.SequenceEqual(computedCS))
                {
                    return (null, ParseStatus(data));
                }
            }
            else
            {
                // Parse data frame
                var checkSumPos = dataEndPos - 2u;
                var (msgStartPos, msgEndPos) = (dataStartPos + 4u, checkSumPos);
                var data = rawResponse.Slice(msgStartPos, msgEndPos);
                var cs = rawResponse.Slice(checkSumPos, dataEndPos);
                var computedCS = ComputeCS(rawResponse.Slice(dataStartPos + 1u, msgEndPos));
                if (cs.SequenceEqual(computedCS))
                {
					Log.Debug("<<< Exiting ParseResponseAsByteArray(rawResponse={RawResponse})", rawResponse);
                    return (data, ParseStatus(null));
                }
            }

            throw new InvalidResponseException("The response is invalid. Checksum does not match.");
        }

        public override DeviceStatusWithRawResponse RawRequest(RequestFrame requestFrame)
        {
			Log.Debug(">>> Entering RawRequest(requestFrame={RequestFrame})", requestFrame);
            if (requestFrame.RawRequest.Length == 0)
            {
                var deviceStatus = new DeviceStatus();
				Log.Debug("RawRequest(requestFrame={RequestFrame}): deviceStatus → {DeviceStatus}", requestFrame, deviceStatus);
                deviceStatus.AddError("E401", "Request length must be at least 1 character");
                return new DeviceStatusWithRawResponse(deviceStatus);
            }
            var cmd = PrinterEncoding.GetBytes(requestFrame.RawRequest.Substring(0, 1))[0];
			Log.Debug("RawRequest(requestFrame={RequestFrame}): cmd → {Cmd}", requestFrame, cmd);
            var data = requestFrame.RawRequest.Substring(1);
			Log.Debug("RawRequest(requestFrame={RequestFrame}): data → {Data}", requestFrame, data);
            var (rawResponse, status) = Request(cmd, data);
            return new DeviceStatusWithRawResponse(status) { RawResponse = rawResponse };
        }

        protected (string, DeviceStatus) Request(byte command, string? data = null)
        {
            return Request(command, data == null ? null : PrinterEncoding.GetBytes(data));
        }

        protected (string, DeviceStatus) Request(byte command, byte[]? data)
        {
            lock (frameSyncLock)
            {
                if (DeadLine < DateTime.Now)
                {
                    var deviceStatus = new DeviceStatus();
                    deviceStatus.AddError("E999", "User timeout occured while sending the request");

                    return (string.Empty, deviceStatus);
                }

                try
                {
                    return ParseResponse(RawRequest(command, data));
                }
                catch (StandardizedStatusMessageException e)
                {
                    var deviceStatus = new DeviceStatus();
                    deviceStatus.AddError(e.Code, e.Message);

                    return (string.Empty, deviceStatus);
                }
                catch (InvalidResponseException e)
                {
                    var deviceStatus = new DeviceStatus();
                    deviceStatus.AddError("E107", e.Message);

                    return (string.Empty, deviceStatus);
                }
                catch (FileNotFoundException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    var deviceStatus = new DeviceStatus();
                    deviceStatus.AddError("E101", e.Message);

                    return (string.Empty, deviceStatus);
                }
            }
        }
    }
}
