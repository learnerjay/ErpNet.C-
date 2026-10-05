namespace ErpNet.FP.Server.Services
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using ErpNet.FP.Core.Service;
    using Serilog;

    /// <summary>
    /// KeepAliveService implements IHostedService, but can be manually used
    /// with StartAsync/StopAsync methods for implementations that 
    /// does not provide support for IHostedService
    /// </summary>
    public class KeepAliveService : IDisposable
    {
        private readonly IServiceController Context;
        private Timer? Timer;

        public KeepAliveService(
            IServiceController context)
        {
			Log.Information(">>> Entering KeepAliveService(context={Context})", context);
            this.Context = context;
			Log.Debug("<<< Exiting KeepAliveService(context={Context})", context);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.CompletedTask;
            }

            // Get Status every 120 seconds, for keeping alive the connection
            Timer = new Timer(KeepAliveWithGetStatus, null, TimeSpan.FromSeconds(120),
                TimeSpan.FromSeconds(120));

            Log.Information("Keep Alive Background Service is started.");

			Log.Debug("<<< Exiting StartAsync(cancellationToken=<redacted>)");
            return Task.CompletedTask;
        }

        private void KeepAliveWithGetStatus(object? state)
        {
			Log.Information(">>> Entering KeepAliveWithGetStatus(state={State})", state);
            if (!Context.IsReady)
            {
                Log.Information("Keep Alive Background Service run skipped(ongoing detection process)!");
                return;
            }
            Log.Information("Keep Alive Background Service running...");
            try
            {
                foreach (var printer in Context.Printers)
                {
                    if (Context.IsReady) printer.Value.CheckStatus();
                }
                Log.Information("Keep Alive Background Service done.");
            }
            catch (Exception e)
            {
                Log.Information($"Error occured while keeping alive with get status: {e.Message}");
            }
			Log.Debug("<<< Exiting KeepAliveWithGetStatus(state={State})", state);
        }

        public Task StopAsync(CancellationToken _)
        {
			Log.Information(">>> Entering StopAsync(_={_})", _);
            Timer?.Change(Timeout.Infinite, 0);

            Log.Information("Keep Alive Background Service is stopped.");

			Log.Debug("<<< Exiting StopAsync(_={_})", _);
            return Task.CompletedTask;
        }

        public void Dispose()
        {
			Log.Information(">>> Entering Dispose()");
            Timer?.Dispose();
			Log.Debug("<<< Exiting Dispose()");
        }
    }
}
