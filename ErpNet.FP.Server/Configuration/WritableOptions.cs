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
			_logger.LogDebug("WritableOptions(environment={Environment},options={Options},section={Section},file={File}): this.environment → {Arg4}", environment, options, section, file, this.environment);
			_logger.LogDebug("WritableOptions(environment={Environment},options={Options},section={Section},file={File}): options → {Options1}", environment, options, section, file, options);
			_logger.LogDebug("WritableOptions(environment={Environment},options={Options},section={Section},file={File}): this.options → {Arg4}", environment, options, section, file, this.options);
        _logger.LogDebug("<<< Exiting WritableOptions(environment={Environment},options={Options},section={Section},file={File})", environment, options, section, file);
            this.section = section;
            this.file = file;
        }

        public T Value => options.CurrentValue;
        public T Get(string? name) => options.Get(name);

        public void Update(Action<T> applyChanges)
        {
			_logger.LogInformation(">>> Entering Update(applyChanges={ApplyChanges})", applyChanges);
            var fileProvider = environment.ContentRootFileProvider;
            var fileInfo = fileProvider.GetFileInfo(file);
			_logger.LogDebug("Update(applyChanges={ApplyChanges}): fileInfo → {FileInfo}", applyChanges, fileInfo);
            var physicalPath = fileInfo.PhysicalPath;

            if (string.IsNullOrEmpty(physicalPath))
            {
				_logger.LogError("Throwing IOException from Update(applyChanges={ApplyChanges})", applyChanges);
                throw new IOException("Invalid file path.");
            }

            var jObject = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(physicalPath));
			_logger.LogDebug("Update(applyChanges={ApplyChanges}): jObject → {JObject}", applyChanges, jObject);
            if (jObject != null)
            {
                T? sectionObject = null;
                if (jObject.TryGetValue(section, out JToken? sectionToken) && sectionToken != null)
                {
                    sectionObject = JsonConvert.DeserializeObject<T>(sectionToken.ToString());
                }
				_logger.LogDebug("Update(applyChanges={ApplyChanges}): sectionObject → {SectionObject}", applyChanges, sectionObject);

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
				_logger.LogDebug("ConfigureWritable(services={Services},section={Section},file={File}): environment → {Environment}", services, section, file, environment);
                var options = provider.GetRequiredService<IOptionsMonitor<T>>();
				_logger.LogDebug("ConfigureWritable(services={Services},section={Section},file={File}): options → {Options}", services, section, file, options);
			_logger.LogDebug("<<< Exiting ConfigureWritable(services={Services},section={Section},file={File})", services, section, file);
                return new WritableOptions<T>(environment, options, section.Key, file);
            });
        }
    }
}
