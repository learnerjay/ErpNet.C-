namespace ErpNet.FP.Server.Controllers
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using ErpNet.FP.Core.Configuration;
    using ErpNet.FP.Core.Service;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Extensions.Hosting;
	using Microsoft.Extensions.Logging;

    // PrintersController, example: //host/service/[controller]
    [Route("[controller]")]
    [ApiController]
    public class ServiceController : ControllerBase
    {
		private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        private readonly IServiceController context;
        private readonly IHostApplicationLifetime serviceLifeTime;
        private readonly ServerVariables serverVariables = new ServerVariables();

        public ServiceController(IServiceController context, IHostApplicationLifetime serviceLifeTime)
        {
			_logger.LogDebug(">>> Entering ServiceController(context={Context},serviceLifeTime={ServiceLifeTime})", context, serviceLifeTime);
			_logger.LogDebug("ServiceController(context={Context},serviceLifeTime={ServiceLifeTime}): serverVariables.WebAccess → {Arg2}", context, serviceLifeTime, serverVariables.WebAccess);
			_logger.LogDebug("ServiceController(context={Context},serviceLifeTime={ServiceLifeTime}): serviceLifeTime → {ServiceLifeTime1}", context, serviceLifeTime, serviceLifeTime);
			_logger.LogDebug("ServiceController(context={Context},serviceLifeTime={ServiceLifeTime}): serverVariables.ServerId → {Arg2}", context, serviceLifeTime, serverVariables.ServerId);
			_logger.LogDebug("ServiceController(context={Context},serviceLifeTime={ServiceLifeTime}): serverVariables.AutoDetect → {Arg2}", context, serviceLifeTime, serverVariables.AutoDetect);
			_logger.LogDebug("ServiceController(context={Context},serviceLifeTime={ServiceLifeTime}): serverVariables.UdpBeaconPort → {Arg2}", context, serviceLifeTime, serverVariables.UdpBeaconPort);
			_logger.LogDebug("ServiceController(context={Context},serviceLifeTime={ServiceLifeTime}): serverVariables.ExcludePortList → {Arg2}", context, serviceLifeTime, serverVariables.ExcludePortList);
			_logger.LogDebug("ServiceController(context={Context},serviceLifeTime={ServiceLifeTime}): serverVariables.DetectionTimeout → {Arg2}", context, serviceLifeTime, serverVariables.DetectionTimeout);
            this.context = context;
            this.serviceLifeTime = serviceLifeTime;
			_logger.LogDebug("ServiceController(context={Context},serviceLifeTime={ServiceLifeTime}): this.context → {Arg2}", context, serviceLifeTime, this.context);

            var assembly = Assembly.GetExecutingAssembly();
			_logger.LogDebug("ServiceController(context={Context},serviceLifeTime={ServiceLifeTime}): assembly → {Assembly}", context, serviceLifeTime, assembly);
			_logger.LogDebug("ServiceController(context={Context},serviceLifeTime={ServiceLifeTime}): this.serviceLifeTime → {Arg2}", context, serviceLifeTime, this.serviceLifeTime);
            var version = assembly.GetName().Version;
			_logger.LogDebug("ServiceController(context={Context},serviceLifeTime={ServiceLifeTime}): version → {Version}", context, serviceLifeTime, version);
            serverVariables.Version = (version != null) ? version.ToString() : "unknown";
            serverVariables.ServerId = context.ServerId;
            serverVariables.AutoDetect = context.AutoDetect;
            serverVariables.UdpBeaconPort = context.UdpBeaconPort;
            serverVariables.ExcludePortList = context.ExcludePortList;
            serverVariables.DetectionTimeout = context.DetectionTimeout;
            serverVariables.WebAccess = context.WebAccess;
			_logger.LogDebug("ServiceController(context={Context},serviceLifeTime={ServiceLifeTime}): context → {Context1}", context, serviceLifeTime, context);
        }

        // GET vars
        [HttpGet("vars")]
        public ActionResult<ServerVariables> Vars()
            => serverVariables;
			_logger.LogDebug("<<< Exiting Vars()");
			_logger.LogDebug(">>> Entering Vars()");

        // GET toggleautodetect
        [HttpGet("toggleautodetect")]
        public ActionResult<ServerVariables> ToggleAutoDetect()
        {
			_logger.LogDebug(">>> Entering ToggleAutoDetect()");
            context.AutoDetect = !context.AutoDetect;
            serverVariables.AutoDetect = context.AutoDetect;
			_logger.LogDebug("ToggleAutoDetect(): serverVariables.AutoDetect → {Arg0}", serverVariables.AutoDetect);
			_logger.LogDebug("ToggleAutoDetect(): context.AutoDetect → {Arg0}", context.AutoDetect);
			_logger.LogDebug(">>> Entering Detect()");
			_logger.LogDebug("<<< Exiting Detect()");
            return serverVariables;
        }

        // POST excludeports
        [HttpPost("excludeports")]
        public ActionResult<ServerVariables> ExcludePorts(Dictionary<string, string> param) 
        {
			_logger.LogDebug(">>> Entering ExcludePorts(param={Param})", param);
            //context.AutoDetect = !context.AutoDetect;
            string? newValue;
            if (param.TryGetValue("ExcludePortList", out newValue))
            {
                var excludePortArray = newValue.ToUpper()
                        .Split(new char[] { ',', ';', ':', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries);
                var excludePortString = string.Join(", ", excludePortArray);
				_logger.LogDebug("ExcludePorts(param={Param}): excludePortString → {ExcludePortString}", param, excludePortString);
				_logger.LogDebug("ExcludePorts(param={Param}): serverVariables.ExcludePortList → {Arg1}", param, serverVariables.ExcludePortList);
				_logger.LogDebug("ExcludePorts(param={Param}): context.ExcludePortList → {Arg1}", param, context.ExcludePortList);
                serverVariables.ExcludePortList = excludePortString;
                context.ExcludePortList = excludePortString;
            }
			_logger.LogDebug("<<< Exiting ExcludePorts(param={Param})", param);
            return serverVariables;
        }

        // POST detectiontimeout
        [HttpPost("detectiontimeout")]
        public ActionResult<ServerVariables> DetectionTimeout(Dictionary<string, string> param)
        {
			_logger.LogDebug(">>> Entering DetectionTimeout(param={Param})", param);
            string? newValue;
            if (param.TryGetValue("DetectionTimeout", out newValue))
            {
                if (int.TryParse(newValue, out int value))
                {
                    if (value > 0)
                    {
                        serverVariables.DetectionTimeout = value;
						_logger.LogDebug("DetectionTimeout(param={Param}): serverVariables.DetectionTimeout → {Arg1}", param, serverVariables.DetectionTimeout);
						_logger.LogDebug("DetectionTimeout(param={Param}): context.DetectionTimeout → {Arg1}", param, context.DetectionTimeout);
                        context.DetectionTimeout = value;
                    }
                }
            }
			_logger.LogDebug("<<< Exiting DetectionTimeout(param={Param})", param);
            return serverVariables;
        }

        // GET detect
        [HttpGet("detect")]
        public ActionResult Detect()
        {
            if (context.Detect(true))
            {
                return StatusCode(StatusCodes.Status200OK);
            }
            return StatusCode(StatusCodes.Status423Locked);
        }

        // GET stop
        [HttpGet("stop")]
        public ActionResult Stop()
        {
			_logger.LogDebug(">>> Entering Stop()");
            try
            {
                serviceLifeTime.StopApplication();
                return StatusCode(StatusCodes.Status200OK);
            }
            catch
            {
				_logger.LogError("Exception in Stop(): {Arg0}", ex.Message);
				_logger.LogDebug("<<< Exiting Stop()");
                return StatusCode(StatusCodes.Status423Locked);
            }
        }

        // GET printersprops
        [HttpGet("printersprops")]
        public ActionResult<Dictionary<string, PrinterProperties>> GetPrinterSpecificProperties()
            => context.PrintersProperties;

        // POST printersprops
        [HttpPost("printersprops")]
        public ActionResult<Dictionary<string, PrinterProperties>> PrinterSpecificProperties(Dictionary<string, PrinterProperties> printersProperties)
        {
            context.PrintersProperties = printersProperties;
            return context.PrintersProperties;
        }

        [HttpPost("webaccess")]
        public ActionResult<ServerVariables> UpdateWebAccess([FromBody] WebAccessOptions newWebAccess)
        {
			_logger.LogDebug(">>> Entering UpdateWebAccess(newWebAccess={NewWebAccess})", newWebAccess);
            if (newWebAccess == null)
                return BadRequest("Invalid configuration data.");

            context.WebAccess = newWebAccess;
			_logger.LogDebug("UpdateWebAccess(newWebAccess={NewWebAccess}): context.WebAccess → {Arg1}", newWebAccess, context.WebAccess);
			_logger.LogDebug("UpdateWebAccess(newWebAccess={NewWebAccess}): serverVariables.WebAccess → {Arg1}", newWebAccess, serverVariables.WebAccess);
            serverVariables.WebAccess = context.WebAccess;

			_logger.LogDebug("<<< Exiting UpdateWebAccess(newWebAccess={NewWebAccess})", newWebAccess);
            return Ok(serverVariables);
        }

        // GET printers
        [HttpGet("printers")]
        public ActionResult<Dictionary<string, PrinterConfig>> Configured()
            => context.ConfiguredPrinters;

        // POST printers/configure
        [HttpPost("printers/configure")]
        public ActionResult Configure(
            [FromBody] PrinterConfigWithId printerConfigWithId
        )
        {
			_logger.LogDebug(">>> Entering Configure(printerConfigWithId={PrinterConfigWithId})", printerConfigWithId);
            if (context.ConfigurePrinter(printerConfigWithId))
            {
			_logger.LogDebug("<<< Exiting Configure(printerConfigWithId={PrinterConfigWithId})", printerConfigWithId);
                return StatusCode(StatusCodes.Status200OK);
            }
            return StatusCode(StatusCodes.Status423Locked);
        }

        // POST printers/delete
        [HttpPost("printers/delete")]
        public ActionResult Delete(
            [FromBody] PrinterConfigWithId printerConfigWithId
        )
        {
			_logger.LogDebug(">>> Entering Delete(printerConfigWithId={PrinterConfigWithId})", printerConfigWithId);
            if (context.DeletePrinter(printerConfigWithId))
            {
			_logger.LogDebug("<<< Exiting Delete(printerConfigWithId={PrinterConfigWithId})", printerConfigWithId);
                return StatusCode(StatusCodes.Status200OK);
            }
            return StatusCode(StatusCodes.Status423Locked);
        }
    }
}