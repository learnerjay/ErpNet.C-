namespace ErpNet.FP.Server.Middlewares
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Serilog;

    public class ActionLoggingMiddleware
    {
        private readonly RequestDelegate requestDelegate;

        public ActionLoggingMiddleware(RequestDelegate requestDelegate)
        {
			Log.Information(">>> Entering ActionLoggingMiddleware(requestDelegate={RequestDelegate})", requestDelegate);
            this.requestDelegate = requestDelegate;
			Log.Debug("<<< Exiting ActionLoggingMiddleware(requestDelegate={RequestDelegate})", requestDelegate);
        }

        public async Task Invoke(HttpContext context)
        {
			Log.Information(">>> Entering Invoke(context={Context})", context);
            await LogRequest(context.Request);
            var bodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();
            context.Response.Body = responseBody;
            await requestDelegate(context);
            await LogResponse(context.Response);
            await responseBody.CopyToAsync(bodyStream);
			Log.Debug("<<< Exiting Invoke(context={Context})", context);
        }

        private async Task LogRequest(HttpRequest request)
        {
			Log.Information(">>> Entering LogRequest(request={Request})", request);
            try
            {
                request.EnableBuffering();
                var buffer = new byte[Convert.ToInt32(request.ContentLength)];
                await request.Body.ReadAsync(buffer, 0, buffer.Length);
                var bodyAsText = Encoding.UTF8.GetString(buffer);
                request.Body.Position = 0;
                Log.Information($"-- HTTP Request -- {request.Method}: {request.Scheme}://{request.Host}{request.Path}{request.QueryString}, Body({bodyAsText.Length}):{(bodyAsText.Length == 0 ? string.Empty : Environment.NewLine)}{bodyAsText}");
            }
            catch
            {
                Log.Information($"-- HTTP Request -- {request.Method}: {request.Scheme}://{request.Host}{request.Path}{request.QueryString}");
            }
			Log.Debug("<<< Exiting LogRequest(request={Request})", request);
        }

        private async Task LogResponse(HttpResponse response)
        {
			Log.Information(">>> Entering LogResponse(response={Response})", response);
            try
            {
                response.Body.Seek(0, SeekOrigin.Begin);
                var streamReader = new StreamReader(response.Body);
                string bodyAsText = await streamReader.ReadToEndAsync();
                response.Body.Seek(0, SeekOrigin.Begin);
                Log.Information($"-- HTTP Response -- Code: {response.StatusCode}, Body({bodyAsText.Length}):{(bodyAsText.Length == 0 ? string.Empty : Environment.NewLine)}{bodyAsText}");
            }
            catch
            {
                Log.Information($"-- HTTP Response -- Code: {response.StatusCode}");
            }
			Log.Debug("<<< Exiting LogResponse(response={Response})", response);
        }
    }
}
