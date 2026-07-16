using System.ComponentModel.Composition;
using System.IO;
using Serilog;

namespace AFOCS.App.Shared
{
    public interface IConfigService
    {
        Task<bool> SaveAsync<T>(T config) where T: class;

        Task<T?> LoadAsync<T>() where T : class;
    }

    [Export(typeof(IConfigService))]
    [method: ImportingConstructor]
    public class ConfigService(ILogger logger) : IConfigService
    {
        public  string ConfigBasePath = Path.Combine(AppContext.BaseDirectory, "Configs");


        public async Task<bool> SaveAsync<T>(T config) where T : class
        {
            try
            {
                ArgumentException.ThrowIfNullOrEmpty(nameof(config));
                var path = Path.Combine(ConfigBasePath, config!.GetType().Name + ".json") ;
                await JsonHelper.WriteToFileAsync(path, config);
                logger.Debug( $"{nameof(SaveAsync)} {typeof(T)} success!! path:{path}");
                return true;
            }
            catch (Exception e)
            {
                logger.Error(e,$"{nameof(SaveAsync)} {typeof(T)} failure!");
                return false;
            }
            
        }

        public async Task<T?> LoadAsync<T>() where T : class
        {
            try
            {
                var path = Path.Combine(ConfigBasePath, typeof(T).Name + ".json");
                var res =await JsonHelper.ReadFromFileAsync<T>(path);
                logger.Debug($"{nameof(LoadAsync)} {typeof(T)} success!! path:{path}");
                return res;
            }
            catch (Exception e)
            {
                logger.Error(e, $"{nameof(LoadAsync)} {typeof(T)} failure!");
                return null;
            }
        }
    }
}
