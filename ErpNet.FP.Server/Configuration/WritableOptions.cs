namespace ErpNet.FP.Server.Configuration
{
    using System;
    using System.IO;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;
	using Microsoft.Extensions.Logging;

    public interface IWritableOptions<out T> : IOptionsSnapshot<T> where T : class, new()
    {
		private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        void Update(Action<T> applyChanges);
    }

    public class WritableOptions<T> : IWritableOptions<T> where T : class, new()
    {
    	private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        private readonly IWebHostEnvironment environment;
        private readonly IOptionsMonitor<T> options;
        private readonly string section;
        private readonly string file;

        public WritableOptions(
            IWebHostEnvironment environment,
            IOptionsMonitor<T> options,
            string section,
            string file)
        {
            this.environment = environment;
            this.options = options;
            this.section = section;
            this.file = file;
			_logger.LogDebug("<<< Exiting WritableOptions(environment={Environment},options={Options},section={Section},file={File})", environment, options, section, file);
        }

        public T Value => options.CurrentValue;
        public T Get(string? name) => options.Get(name);

        public void Update(Action<T> applyChanges)
        {
			_logger.LogInformation(">>> Entering Update(applyChanges={ApplyChanges})", applyChanges);
            var fileProvider = environment.ContentRootFileProvider;
            var fileInfo = fileProvider.GetFileInfo(file);
            var physicalPath = fileInfo.PhysicalPath;

            if (string.IsNullOrEmpty(physicalPath))
            {
                throw new IOException("Invalid file path.");
            }

            var jObject = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(physicalPath));
            if (jObject != null)
            {
                T? sectionObject = null;
                if (jObject.TryGetValue(section, out JToken? sectionToken) && sectionToken != null)
                {
                    sectionObject = JsonConvert.DeserializeObject<T>(sectionToken.ToString());
                }

                if (object.ReferenceEquals(sectionObject, null))
                {
                    sectionObject = Value ?? new T();
                }

                applyChanges(sectionObject);

                jObject[section] = JObject.Parse(JsonConvert.SerializeObject(sectionObject));
                File.WriteAllText(physicalPath, JsonConvert.SerializeObject(jObject, Formatting.Indented));
            }
			_logger.LogDebug("<<< Exiting Update(applyChanges={ApplyChanges})", applyChanges);
        }
    }

    public static class ServiceCollectionExtensions
    {
    	private static readonly Microsoft.Extensions.Logging.ILogger _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
        public static void ConfigureWritable<T>(
            this IServiceCollection services,
            IConfigurationSection section,
            string file = "appsettings.json") where T : class, new()
        {
			_logger.LogInformation(">>> Entering ConfigureWritable(services={Services},section={Section},file={File})", services, section, file);
            services.Configure<T>(section);
            services.AddTransient<IWritableOptions<T>>(provider =>
            {
                var environment = provider.GetRequiredService<IWebHostEnvironment>();
                var options = provider.GetRequiredService<IOptionsMonitor<T>>();
			_logger.LogDebug("<<< Exiting ConfigureWritable(services={Services},section={Section},file={File})", services, section, file);
                return new WritableOptions<T>(environment, options, section.Key, file);
            });
        }
    }
}
