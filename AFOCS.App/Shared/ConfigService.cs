using System.IO;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Shared
{
    public interface IConfigService
    {
        Task<bool> SaveAsync<T>(T config) where T: class;

        Task<T?> LoadAsync<T>() where T : class;

    }
    public class ConfigService : IConfigService
    {
        private readonly ILogger<ConfigService> _logger;
        public  string ConfigBasePath;
        public ConfigService(ILogger<ConfigService> logger)
        {
            ConfigBasePath = Path.Combine(AppContext.BaseDirectory, "Configs");
            _logger = logger;
        }


        public async Task<bool> SaveAsync<T>(T config) where T : class
        {
            try
            {
                ArgumentException.ThrowIfNullOrEmpty(nameof(config));
                var path = Path.Combine(ConfigBasePath, config!.GetType().Name + ".json") ;
                await JsonHelper.WriteToFileAsync(path, config);
                _logger.LogDebug( $"{nameof(SaveAsync)} {typeof(T)} success!! path:{path}");
                return true;
            }
            catch (Exception e)
            {
                _logger.LogError(e,$"{nameof(SaveAsync)} {typeof(T)} failure!");
                return false;
            }
            
        }

        public async Task<T?> LoadAsync<T>() where T : class
        {
            try
            {
                var path = Path.Combine(ConfigBasePath, typeof(T).Name + ".json");
                var res =await JsonHelper.ReadFromFileAsync<T>(path);
                _logger.LogDebug($"{nameof(LoadAsync)} {typeof(T)} success!! path:{path}");
                return res;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"{nameof(LoadAsync)} {typeof(T)} failure!");
                return null;
            }
        }
    }
}
