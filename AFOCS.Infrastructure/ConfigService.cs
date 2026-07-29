using System.Collections.Concurrent;
using System.ComponentModel.Composition;
using Serilog;

namespace AFOCS.Infrastructure
{
    public interface IConfigService
    {
        Task<bool> SaveAsync<T>(T config) where T : class;

        Task<T?> LoadAsync<T>() where T : class;

        Task<bool> SaveAsync(Type type, object config);

        Task<object?> LoadAsync(Type type);

        /// <summary>清除指定类型的内存缓存，下次 Load 会重新从文件读取</summary>
        void ClearCache<T>() where T : class;

        /// <summary>清除所有内存缓存</summary>
        void ClearAllCache();
    }

    [Export(typeof(IConfigService))]
    [method: ImportingConstructor]
    public class ConfigService(ILogger logger) : IConfigService
    {
        public string ConfigBasePath = Path.Combine(AppContext.BaseDirectory, "Configs");

        private readonly ConcurrentDictionary<Type, object> _cache = new();

        public async Task<bool> SaveAsync<T>(T config) where T : class
        {
            try
            {
                ArgumentException.ThrowIfNullOrEmpty(nameof(config));
                var path = Path.Combine(ConfigBasePath, config!.GetType().Name + ".json");
                await JsonHelper.WriteToFileAsync(path, config);
                _cache[typeof(T)] = config;
                logger.Debug($"{nameof(SaveAsync)} {typeof(T)} success!! path:{path}");
                return true;
            }
            catch (Exception e)
            {
                logger.Error(e, $"{nameof(SaveAsync)} {typeof(T)} failure!");
                return false;
            }
        }

        public async Task<T?> LoadAsync<T>() where T : class
        {
            if (_cache.TryGetValue(typeof(T), out var cached))
            {
                logger.Debug($"{nameof(LoadAsync)} {typeof(T)} hit cache");
                return (T)cached;
            }

            try
            {
                var path = Path.Combine(ConfigBasePath, typeof(T).Name + ".json");
                var res = await JsonHelper.ReadFromFileAsync<T>(path);
                if (res != null)
                    _cache[typeof(T)] = res;
                logger.Debug($"{nameof(LoadAsync)} {typeof(T)} success!! path:{path}");
                return res;
            }
            catch (Exception e)
            {
                logger.Error(e, $"{nameof(LoadAsync)} {typeof(T)} failure!");
                return null;
            }
        }

        public async Task<bool> SaveAsync(Type type, object config)
        {
            try
            {
                ArgumentNullException.ThrowIfNull(config);
                var path = Path.Combine(ConfigBasePath, type.Name + ".json");
                await JsonHelper.WriteToFileAsync(path, config);
                _cache[type] = config;
                logger.Debug($"{nameof(SaveAsync)} {type} success!! path:{path}");
                return true;
            }
            catch (Exception e)
            {
                logger.Error(e, $"{nameof(SaveAsync)} {type} failure!");
                return false;
            }
        }

        public async Task<object?> LoadAsync(Type type)
        {
            if (_cache.TryGetValue(type, out var cached))
            {
                logger.Debug($"{nameof(LoadAsync)} {type} hit cache");
                return cached;
            }

            try
            {
                var path = Path.Combine(ConfigBasePath, type.Name + ".json");
                var json = await File.ReadAllTextAsync(path);
                var res = JsonHelper.Deserialize(json, type);
                if (res != null)
                    _cache[type] = res;
                logger.Debug($"{nameof(LoadAsync)} {type} success!! path:{path}");
                return res;
            }
            catch (Exception e)
            {
                logger.Error(e, $"{nameof(LoadAsync)} {type} failure!");
                return null;
            }
        }

        public void ClearCache<T>() where T : class
        {
            _cache.TryRemove(typeof(T), out _);
            logger.Debug($"{nameof(ClearCache)} {typeof(T)}");
        }

        public void ClearAllCache()
        {
            _cache.Clear();
            logger.Debug($"{nameof(ClearAllCache)} all caches cleared");
        }
    }
}
