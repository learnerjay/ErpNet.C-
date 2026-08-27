namespace ErpNet.FP.Core.Service
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using ErpNet.FP.Core.Configuration;
    using Serilog;

    public interface IServiceController
    {
        Dictionary<string, DeviceInfo> PrintersInfo { get; }

        Dictionary<string, IFiscalPrinter> Printers { get; }

        Dictionary<string, PrinterConfig> ConfiguredPrinters { get; }

        Dictionary<string, PrinterProperties> PrintersProperties { get; set; }

        Task<object?> RunAsync(PrintJob printJob);

        TaskInfoResult GetTaskInfo(string taskId);

        bool Detect(bool forceAutoDetect = false);

        bool IsReady { get; }

        string ServerId { get; }

        bool AutoDetect { get; set; }

        int UdpBeaconPort { get; set; }

        WebAccessOptions WebAccess { get; set; }

        string ExcludePortList { get; set; }

        int DetectionTimeout { get; set; }

        bool ConfigurePrinter(PrinterConfigWithId printerConfigWithId);

        bool DeletePrinter(PrinterConfigWithId printerConfigWithId);
    }

    public abstract class ServiceControllerContext : IServiceController
    {

        private Task? consumer;
        private readonly object taskSyncLock = new object();
        private readonly object consumerSyncLock = new object();
        protected volatile bool isReady = false;

        protected ServiceOptions configOptions = new ServiceOptions();
        public string ServerId { get; private set; } = string.Empty;
        public Provider.Provider Provider { get; protected set; }
        public Dictionary<string, DeviceInfo> PrintersInfo { get; } = new Dictionary<string, DeviceInfo>();
        public Dictionary<string, IFiscalPrinter> Printers { get; } = new Dictionary<string, IFiscalPrinter>();
        public Dictionary<string, PrinterConfig> ConfiguredPrinters
        {
            get => configOptions.Printers;
        }

        public Dictionary<string, PrinterProperties> PrintersProperties
        {
            get => configOptions.PrintersProperties;
            set
            {
                configOptions.PrintersProperties = value;
                WriteOptions();
            }
        }
        public ConcurrentQueue<string> TaskQueue { get; } = new ConcurrentQueue<string>();
        public ConcurrentDictionary<string, PrintJob> Tasks { get; } = new ConcurrentDictionary<string, PrintJob>();
        public bool IsReady { get => isReady; set => isReady = value; }

        public bool AutoDetect
        {
            get => configOptions.AutoDetect;

            set
            {
                configOptions.AutoDetect = value;
                WriteOptions();
            }
        }

        public int UdpBeaconPort
        {
            get => configOptions.UdpBeaconPort;

            set
            {
                configOptions.UdpBeaconPort = value;
                WriteOptions();
            }
        }

        public string ExcludePortList
        {
            get => configOptions.ExcludePortList;
            set
            {
                configOptions.ExcludePortList = value;
                WriteOptions();
            }
        }

        public int DetectionTimeout
        {
            get => configOptions.DetectionTimeout;
            set
            {
                configOptions.DetectionTimeout = value;
                WriteOptions();
            }
        }

        public WebAccessOptions WebAccess
        {
            get => configOptions.WebAccess;
            set
            {
                configOptions.WebAccess = value;
                WriteOptions();
            }
        }

        public ServiceControllerContext()
        {
			Log.Debug(">>> Entering ServiceControllerContext()");
            Provider = new Provider.Provider(configOptions);
			Log.Debug("ServiceControllerContext(): Provider → {Provider}", Provider);
        }

        protected abstract void SetupProvider();
        protected abstract void WriteOptions();

        protected virtual void Setup()
        {
			Log.Debug(">>> Entering Setup()");
            ReadOptions();
            SetupProvider();
            isReady = true;
			Log.Debug("Setup(): isReady → {IsReady}", isReady);
            Task.Run(() => Detect());
			Log.Debug("<<< Exiting Setup()");
        }

        protected virtual void ReadOptions()
        {
			Log.Debug(">>> Entering ReadOptions()");
            // Server ID
            if (String.IsNullOrEmpty(configOptions.ServerId))
            {
                // serverId is RFC7515 Guid
                var serverId = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                    .Substring(0, 22)
                    .Replace("/", "_")
                    .Replace("+", "-");
					Log.Debug("<<< Exiting ReadOptions()");
					Log.Debug("ReadOptions(): configOptions.ServerId → {Arg0}", configOptions.ServerId);
                configOptions.ServerId = serverId;
            }
            this.ServerId = configOptions.ServerId;
			Log.Debug(">>> Entering Detect(forceAutoDetect={ForceAutoDetect})", forceAutoDetect);
			Log.Debug("Detect(forceAutoDetect={ForceAutoDetect}): isReady → {IsReady}", forceAutoDetect, isReady);
			Log.Debug("<<< Exiting Detect(forceAutoDetect={ForceAutoDetect})", forceAutoDetect);
        }

        public bool Detect(bool forceAutoDetect = false)
        {
            lock (taskSyncLock)
            {
                if (TaskQueue.Count == 0 && isReady)
                {
                    isReady = false;

                    try
                    {

                        PrintersInfo.Clear();
                        Printers.Clear();

                        // Detecting configured printers
                        if (configOptions.Printers != null && configOptions.Printers.Count != 0)
                        {
                            Log.Information("Detecting configured printers...");
                            foreach (var printerSetting in configOptions.Printers)
                            {
                                string logString = $"Trying {printerSetting.Key}: {printerSetting.Value.Uri}";
                                var uri = printerSetting.Value.Uri;
                                if (uri.Length > 0)
                                {
                                    try
                                    {
                                        var printer = Provider.Connect(printerSetting.Value.Uri, configOptions, false, null);
										Log.Debug("Detect(forceAutoDetect={ForceAutoDetect}): printer → {Printer}", forceAutoDetect, printer);
                                        Log.Information($"{logString}, OK");
                                        PrintersInfo.Add(printerSetting.Key, printer.DeviceInfo);
                                        Printers.Add(printerSetting.Key, printer);
                                    }
                                    catch
                                    {
                                        Log.Information($"{logString}, failed");
                                        // Do not add this printer, it fails to connect.
                                    }
                                }
                            }

                            /*
                            // Auto cache to config all listed printers, for future use
                            // It is possible to have aliases, i.e. different PrinterId with the same Uri
                            foreach (var printer in Printers)
                            {
                                configOptions.Printers[printer.Key] = new PrinterConfig { Uri = printer.Value.DeviceInfo.Uri };
                            }
                            */
                        }
                        
                        var excludePortList = configOptions.ExcludePortList
                            .Split(new char[] { ',', ';', ':', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries)
                            .ToList();
                        foreach (var printer in Printers)
                        {
                            // use Uris instead of Channels
                            var usedChannel = printer.Value.DeviceInfo.Uri.Split("//")[1].Split(':')[0];
							Log.Debug("Detect(forceAutoDetect={ForceAutoDetect}): usedChannel → {UsedChannel}", forceAutoDetect, usedChannel);
                            if (!excludePortList.Contains(usedChannel) && usedChannel.StartsWith("COM"))
                                excludePortList.Add(usedChannel);
                        }
                        // Autodetecting
                        var autoDetectedPrinters = new Dictionary<string, PrinterConfig>();
						Log.Debug("Detect(forceAutoDetect={ForceAutoDetect}): autoDetectedPrinters → {AutoDetectedPrinters}", forceAutoDetect, autoDetectedPrinters);
                        if (forceAutoDetect || configOptions.AutoDetect)
                        {
                            Log.Information("Autodetecting local printers...");
                            var printers = Provider.DetectAvailablePrinters(excludePortList);
							Log.Debug("Detect(forceAutoDetect={ForceAutoDetect}): printers → {Printers}", forceAutoDetect, printers);
                            foreach (KeyValuePair<string, IFiscalPrinter> printer in printers)
                            {
                                AddPrinter(printer.Value);
                            }
                        }

                        // configOptions.AutoDetect = Printers.Count == 0;

                        WriteOptions();

                        Log.Information($"Detecting done. Found {Printers.Count} available printer(s).");

                    }
                    finally
                    {
                        isReady = true;
                    }

                    return true;
                }
                return false;
            }
        }

        public TaskInfoResult GetTaskInfo(string taskId)
        {
			Log.Debug(">>> Entering GetTaskInfo(taskId={TaskId})", taskId);
            lock (taskSyncLock)
            {
                var taskInfoResult = new TaskInfoResult();
				Log.Debug("GetTaskInfo(taskId={TaskId}): taskInfoResult → {TaskInfoResult}", taskId, taskInfoResult);
                {
                    if (Tasks.TryGetValue(taskId, out var printJob)
                        && printJob is not null)
                    {
                        taskInfoResult.TaskStatus = printJob.TaskStatus;
						Log.Debug("GetTaskInfo(taskId={TaskId}): taskInfoResult.TaskStatus → {Arg1}", taskId, taskInfoResult.TaskStatus);
                        if (printJob.Result != null)
                        {
                            taskInfoResult.Result = printJob.Result;
							Log.Debug("GetTaskInfo(taskId={TaskId}): taskInfoResult.Result → {Arg1}", taskId, taskInfoResult.Result);
                        }
                    }
                }
				Log.Debug("<<< Exiting GetTaskInfo(taskId={TaskId})", taskId);
                return taskInfoResult;
            }
        }

        public async Task<object?> RunAsync(PrintJob printJob)
        {
			Log.Debug(">>> Entering RunAsync(printJob={PrintJob})", printJob);
            try
            {
                var taskId = Enqueue(printJob);
				Log.Debug("RunAsync(printJob={PrintJob}): taskId → {TaskId}", printJob, taskId);

                if (printJob.AsyncTimeout == 0)
                {
                    return new TaskIdResult { TaskId = taskId };
                }
                return await Task.Run(() => RunTask(taskId, printJob.AsyncTimeout));
            }
            catch (StandardizedStatusMessageException e)
            {
				Log.Error("Exception in RunAsync(printJob={PrintJob}): {Arg1}", printJob, e.Message);
                var status = new DeviceStatus();
				Log.Debug("RunAsync(printJob={PrintJob}): status → {Status}", printJob, status);
                status.AddError(e.Code, e.Message);
				Log.Debug("<<< Exiting RunAsync(printJob={PrintJob})", printJob);
				Log.Debug(">>> Entering RunTask(taskId={TaskId},asyncTimeout={AsyncTimeout})", taskId, asyncTimeout);
				Log.Debug("<<< Exiting RunTask(taskId={TaskId},asyncTimeout={AsyncTimeout})", taskId, asyncTimeout);
                return status;
            }
        }

        public object? RunTask(string taskId, int asyncTimeout)
        {
			Log.Debug(">>> Entering RunTask(taskId={TaskId},asyncTimeout={AsyncTimeout})", taskId, asyncTimeout);
            const int timeoutMinimalStep = 50; // check the queue every 50 ms
            if (asyncTimeout < 0) asyncTimeout = PrintJob.DefaultTimeout;
            if (Tasks.TryGetValue(taskId, out var printJob)
                && printJob is not null)
            {
                // While the print job is not finished
                while (printJob.Finished == null)
                {
                    // We give the device some time to process the job
                    Thread.Sleep(timeoutMinimalStep);
                    asyncTimeout -= timeoutMinimalStep;
                    if (asyncTimeout <= 0) // Async timeout occured, so return taskId
                    {
                        return new TaskIdResult { TaskId = taskId };
                    }
                }
                return printJob.Result;
            }
            else
            {
                return null;
            }
        }

        private void EnsureConsumer()
        {
			Log.Debug(">>> Entering EnsureConsumer()");
            lock (consumerSyncLock)
            {
                if (consumer == null || consumer.IsCompleted || consumer.IsFaulted)
                {
                    consumer = Task.Factory.StartNew(() => ConsumeTaskQueue(), TaskCreationOptions.LongRunning);
                }
            }
			Log.Debug("<<< Exiting EnsureConsumer()");
        }

        public void ConsumeTaskQueue()
        {
			Log.Debug(">>> Entering ConsumeTaskQueue()");
            // Run all tasks from the TaskQueue
            while (TaskQueue.TryDequeue(out var taskId))
            {
                if (string.IsNullOrEmpty(taskId))
                    continue;

                // Resolve printJob by taskId
                if (Tasks.TryGetValue(taskId, out var printJob))
                {
                    printJob?.Run();
                }
            }
			Log.Debug("<<< Exiting ConsumeTaskQueue()");
        }

        public string Enqueue(PrintJob printJob)
        {
			Log.Debug(">>> Entering Enqueue(printJob={PrintJob})", printJob);
            // CASE: Clearing Expired Tasks
            // ClearExpiredTasks();

            // taskId is RFC7515 Guid
            var taskId = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Substring(0, 22)
                .Replace("/", "_")
                .Replace("+", "-");

            if (!string.IsNullOrEmpty(printJob.TaskId))
            {
                // override generated taskId with user supplied taskId
                taskId = printJob.TaskId;

                // maximum length of 60 characters validation
                if (taskId.Length > 60)
                {
					Log.Warning("Throwing StandardizedStatusMessageException from Enqueue(printJob={PrintJob})", printJob);
                    throw new StandardizedStatusMessageException($"Task Id is too long", "E110");
                }

                // printable characters validation
                var nonPrintableCharactersPattern = @"[\x01-\x1F]";
				Log.Debug("Enqueue(printJob={PrintJob}): nonPrintableCharactersPattern → {NonPrintableCharactersPattern}", printJob, nonPrintableCharactersPattern);
                Match match = Regex.Match(taskId, nonPrintableCharactersPattern, RegexOptions.IgnoreCase);
				Log.Debug("Enqueue(printJob={PrintJob}): match → {Match}", printJob, match);
                if (match.Success)
                {
                    throw new StandardizedStatusMessageException($"Use only printable charaters for Task Id", "E110");
                }
            }

            if (Tasks.ContainsKey(taskId))
            {
                throw new StandardizedStatusMessageException($"Task Id \"{taskId}\" is already used", "E109");
            }

            Tasks[taskId] = printJob;
            TaskQueue.Enqueue(taskId);

            // Ensure that there is a running ConsumerThread, that will process the queue
            EnsureConsumer();

			Log.Debug("<<< Exiting Enqueue(printJob={PrintJob})", printJob);
            return taskId;
        }

        public void AddPrinter(IFiscalPrinter printer)
        {
			Log.Debug(">>> Entering AddPrinter(printer={Printer})", printer);
            // We use serial number of local connected fiscal printers as Printer ID
            var baseID = printer.DeviceInfo.SerialNumber.ToLowerInvariant();

            var printerID = baseID;
            int duplicateNumber = 0;
            while (PrintersInfo.ContainsKey(printerID))
            {
                if (PrintersInfo[printerID].Uri == printer.DeviceInfo.Uri)
                {
                    // Do not add enumeration for duplicated Id and Uri
                    return;
                }
                duplicateNumber++;
                printerID = $"{baseID}_{duplicateNumber}";
            }
            PrintersInfo.Add(printerID, printer.DeviceInfo);
            Printers.Add(printerID, printer);
            Log.Information($"Found {printerID}: {printer.DeviceInfo.Uri}");
			Log.Debug("<<< Exiting AddPrinter(printer={Printer})", printer);
        }

        public bool ConfigurePrinter(PrinterConfigWithId printerConfigWithId)
        {
			Log.Debug(">>> Entering ConfigurePrinter(printerConfigWithId={PrinterConfigWithId})", printerConfigWithId);
            if (String.IsNullOrEmpty(printerConfigWithId.Id) || String.IsNullOrEmpty(printerConfigWithId.Uri))
            {
                return false;
            }
            lock (taskSyncLock)
            {
                ConfiguredPrinters.Add(printerConfigWithId.Id, printerConfigWithId);
                WriteOptions();
				Log.Debug("<<< Exiting ConfigurePrinter(printerConfigWithId={PrinterConfigWithId})", printerConfigWithId);
                return true;
            }
        }

        public bool DeletePrinter(PrinterConfigWithId printerConfigWithId)
        {
			Log.Debug(">>> Entering DeletePrinter(printerConfigWithId={PrinterConfigWithId})", printerConfigWithId);
            if (String.IsNullOrEmpty(printerConfigWithId.Id))
            {
                return false;
            }
            lock (taskSyncLock)
            {
                if (!ConfiguredPrinters.Remove(printerConfigWithId.Id))
                {
                    return false;
                }
                WriteOptions();
				Log.Debug("<<< Exiting DeletePrinter(printerConfigWithId={PrinterConfigWithId})", printerConfigWithId);
                return true;
            }
        }
    }
}
