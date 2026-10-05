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
			_logger.LogInformation(">>> Entering ServiceController(context={Context},serviceLifeTime={ServiceLifeTime})", context, serviceLifeTime);
            this.context = context;
            this.serviceLifeTime = serviceLifeTime;

            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            serverVariables.Version = (version != null) ? version.ToString() : "unknown";
            serverVariables.ServerId = context.ServerId;
            serverVariables.AutoDetect = context.AutoDetect;
            serverVariables.UdpBeaconPort = context.UdpBeaconPort;
            serverVariables.ExcludePortList = context.ExcludePortList;
            serverVariables.DetectionTimeout = context.DetectionTimeout;
            serverVariables.WebAccess = context.WebAccess;
			_logger.LogDebug("<<< Exiting ServiceController(context={Context},serviceLifeTime={ServiceLifeTime})", context, serviceLifeTime);
        }

        // GET vars
        [HttpGet("vars")]
        public ActionResult<ServerVariables> Vars()
            => serverVariables;

        // GET toggleautodetect
        [HttpGet("toggleautodetect")]
        public ActionResult<ServerVariables> ToggleAutoDetect()
        {
            context.AutoDetect = !context.AutoDetect;
			_logger.LogInformation(">>> Entering ToggleAutoDetect()");
            serverVariables.AutoDetect = context.AutoDetect;
			_logger.LogInformation(">>> Entering Detect()");
			_logger.LogDebug("<<< Exiting Detect()");
			_logger.LogDebug("<<< Exiting ToggleAutoDetect()");
            return serverVariables;
        }

        // POST excludeports
        [HttpPost("excludeports")]
        public ActionResult<ServerVariables> ExcludePorts(Dictionary<string, string> param) 
        {
			_logger.LogInformation(">>> Entering ExcludePorts(param={Param})", param);
            //context.AutoDetect = !context.AutoDetect;
            string? newValue;
            if (param.TryGetValue("ExcludePortList", out newValue))
            {
                var excludePortArray = newValue.ToUpper()
                        .Split(new char[] { ',', ';', ':', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries);
                var excludePortString = string.Join(", ", excludePortArray);
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
			_logger.LogInformation(">>> Entering DetectionTimeout(param={Param})", param);
            string? newValue;
            if (param.TryGetValue("DetectionTimeout", out newValue))
            {
                if (int.TryParse(newValue, out int value))
                {
                    if (value > 0)
                    {
                        serverVariables.DetectionTimeout = value;
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
			_logger.LogInformation(">>> Entering Stop()");
            try
            {
                serviceLifeTime.StopApplication();
                return StatusCode(StatusCodes.Status200OK);
            }
            catch
            {
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
			_logger.LogInformation(">>> Entering UpdateWebAccess(newWebAccess={NewWebAccess})", newWebAccess);
            if (newWebAccess == null)
                return BadRequest("Invalid configuration data.");

            context.WebAccess = newWebAccess;
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
			_logger.LogInformation(">>> Entering Configure(printerConfigWithId={PrinterConfigWithId})", printerConfigWithId);
            if (context.ConfigurePrinter(printerConfigWithId))
            {
                return StatusCode(StatusCodes.Status200OK);
            }
			_logger.LogDebug("<<< Exiting Configure(printerConfigWithId={PrinterConfigWithId})", printerConfigWithId);
            return StatusCode(StatusCodes.Status423Locked);
        }

        // POST printers/delete
        [HttpPost("printers/delete")]
        public ActionResult Delete(
            [FromBody] PrinterConfigWithId printerConfigWithId
        )
        {
			_logger.LogInformation(">>> Entering Delete(printerConfigWithId={PrinterConfigWithId})", printerConfigWithId);
            if (context.DeletePrinter(printerConfigWithId))
            {
                return StatusCode(StatusCodes.Status200OK);
            }
			_logger.LogDebug("<<< Exiting Delete(printerConfigWithId={PrinterConfigWithId})", printerConfigWithId);
            return StatusCode(StatusCodes.Status423Locked);
        }
    }
}