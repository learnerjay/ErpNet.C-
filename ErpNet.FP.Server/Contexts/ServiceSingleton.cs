namespace ErpNet.FP.Server.Contexts
{
    using System.Collections.Generic;
    using ErpNet.FP.Core.Configuration;
    using ErpNet.FP.Core.Drivers.BgDaisy;
    using ErpNet.FP.Core.Drivers.BgDatecs;
    using ErpNet.FP.Core.Drivers.BgEltrade;
    using ErpNet.FP.Core.Drivers.BgIcp;
    using ErpNet.FP.Core.Drivers.BgIncotex;
    using ErpNet.FP.Core.Drivers.BgTremol;
    using ErpNet.FP.Core.Drivers.BgSis;
    using ErpNet.FP.Core.Provider;
    using ErpNet.FP.Core.Service;
    using ErpNet.FP.Core.Transports;
    using ErpNet.FP.Server.Configuration;
	using Microsoft.Extensions.Logging;

    /// <summary>
    /// ServiceSingleton is wrapper around ServiceControllerContext, that
    /// introduces writableConfigOptions through dependency injection, 
    /// and setting up the Provider
    /// </summary>
    public class ServiceSingleton : ServiceControllerContext
    {
	private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        private readonly IWritableOptions<ServiceOptions> _writableConfigOptions;

        public ServiceSingleton(
            IWritableOptions<ServiceOptions> writableConfigOptions)
            : base()
        {
			_logger.LogDebug(">>> Entering ServiceSingleton(writableConfigOptions={WritableConfigOptions})", writableConfigOptions);
            _writableConfigOptions = writableConfigOptions;
			_logger.LogDebug("<<< Exiting ServiceSingleton(writableConfigOptions={WritableConfigOptions})", writableConfigOptions);
			_logger.LogDebug("ServiceSingleton(writableConfigOptions={WritableConfigOptions}): _writableConfigOptions → {_writableConfigOptions}", writableConfigOptions, _writableConfigOptions);
            configOptions = writableConfigOptions.Value;
			_logger.LogDebug("ServiceSingleton(writableConfigOptions={WritableConfigOptions}): configOptions → {ConfigOptions}", writableConfigOptions, configOptions);
            Setup();
        }

        protected override void SetupProvider()
        {
			_logger.LogDebug(">>> Entering SetupProvider()");
            // Transports
            var comTransport = new ComTransport();
			_logger.LogDebug("SetupProvider(): comTransport → {ComTransport}", comTransport);
            var tcpTransport = new TcpTransport();
			_logger.LogDebug("SetupProvider(): tcpTransport → {TcpTransport}", tcpTransport);
            var httpSisJsonTransport = new BgSisJsonHttpTransport();
			_logger.LogDebug("SetupProvider(): httpSisJsonTransport → {HttpSisJsonTransport}", httpSisJsonTransport);

            // Drivers
            var datecsXIsl = new BgDatecsXIslFiscalPrinterDriver();
			_logger.LogDebug("SetupProvider(): datecsXIsl → {DatecsXIsl}", datecsXIsl);
            var datecsPIsl = new BgDatecsPIslFiscalPrinterDriver();
			_logger.LogDebug("SetupProvider(): datecsPIsl → {DatecsPIsl}", datecsPIsl);
            var datecsCIsl = new BgDatecsCIslFiscalPrinterDriver();
			_logger.LogDebug("SetupProvider(): datecsCIsl → {DatecsCIsl}", datecsCIsl);
            var eltradeIsl = new BgEltradeIslFiscalPrinterDriver();
			_logger.LogDebug("SetupProvider(): eltradeIsl → {EltradeIsl}", eltradeIsl);
            var daisyIsl = new BgDaisyIslFiscalPrinterDriver();
			_logger.LogDebug("SetupProvider(): daisyIsl → {DaisyIsl}", daisyIsl);
            var incotexIsl = new BgIncotexIslFiscalPrinterDriver();
			_logger.LogDebug("SetupProvider(): incotexIsl → {IncotexIsl}", incotexIsl);
            var islIcp = new BgIslIcpFiscalPrinterDriver();
			_logger.LogDebug("SetupProvider(): islIcp → {IslIcp}", islIcp);
            var tremolZfp = new BgTremolZfpFiscalPrinterDriver();
			_logger.LogDebug("SetupProvider(): tremolZfp → {TremolZfp}", tremolZfp);
            var tremolV2Zfp = new BgTremolZfpV2FiscalPrinterDriver();
			_logger.LogDebug("SetupProvider(): tremolV2Zfp → {TremolV2Zfp}", tremolV2Zfp);
            var bgSisJson = new BgSisJsonFiscalPrinterDriver();
			_logger.LogDebug("SetupProvider(): bgSisJson → {BgSisJson}", bgSisJson);

            // Add drivers and their compatible transports to the provider.
            Provider = new Provider(configOptions)
                // Isl X Frame
				_logger.LogDebug("SetupProvider(): Provider → {Provider}", Provider);
                .Register(datecsXIsl, comTransport)
                .Register(datecsXIsl, tcpTransport)
                // Isl Frame
                .Register(datecsCIsl, comTransport)
                .Register(datecsCIsl, tcpTransport)
                .Register(datecsPIsl, comTransport)
                .Register(datecsPIsl, tcpTransport)
                .Register(eltradeIsl, comTransport)
                .Register(eltradeIsl, tcpTransport)
                // Isl Frame + constants
                .Register(daisyIsl, comTransport)
                .Register(daisyIsl, tcpTransport)
                .Register(incotexIsl, comTransport)
                .Register(incotexIsl, tcpTransport)
                // Icp Frame
                .Register(islIcp, comTransport)
                .Register(islIcp, tcpTransport)
                // Zfp Frame
                .Register(tremolZfp, comTransport)
                .Register(tremolZfp, tcpTransport)
                .Register(tremolV2Zfp, comTransport)
                .Register(tremolV2Zfp, tcpTransport)
                // SIS Fiscal Module (JSON-RPC over HTTP)
                .Register(bgSisJson, httpSisJsonTransport);
				_logger.LogDebug("<<< Exiting SetupProvider()");
        }

        protected override void WriteOptions()
        {
			_logger.LogDebug(">>> Entering WriteOptions()");
            _writableConfigOptions.Update(updatedConfigOptions =>
            {
                updatedConfigOptions.AutoDetect = configOptions.AutoDetect;
				_logger.LogDebug("WriteOptions(): updatedConfigOptions.AutoDetect → {Arg0}", updatedConfigOptions.AutoDetect);
				_logger.LogDebug("WriteOptions(): updatedConfigOptions.ServerId → {Arg0}", updatedConfigOptions.ServerId);
				_logger.LogDebug("WriteOptions(): updatedConfigOptions.ExcludePortList → {Arg0}", updatedConfigOptions.ExcludePortList);
				_logger.LogDebug("WriteOptions(): updatedConfigOptions.DetectionTimeout → {Arg0}", updatedConfigOptions.DetectionTimeout);
				_logger.LogDebug("WriteOptions(): updatedConfigOptions.Printers → {Arg0}", updatedConfigOptions.Printers);
				_logger.LogDebug("WriteOptions(): updatedConfigOptions.PrintersProperties → {Arg0}", updatedConfigOptions.PrintersProperties);
				_logger.LogDebug("WriteOptions(): updatedConfigOptions.WebAccess → {Arg0}", updatedConfigOptions.WebAccess);
                updatedConfigOptions.ServerId = configOptions.ServerId;
                updatedConfigOptions.ExcludePortList = configOptions.ExcludePortList;
                updatedConfigOptions.DetectionTimeout = configOptions.DetectionTimeout;
                updatedConfigOptions.Printers = configOptions.Printers ?? [];
                updatedConfigOptions.PrintersProperties = configOptions.PrintersProperties ?? [];
                updatedConfigOptions.WebAccess = configOptions.WebAccess;
            });
			_logger.LogDebug("<<< Exiting WriteOptions()");
        }
    }

}