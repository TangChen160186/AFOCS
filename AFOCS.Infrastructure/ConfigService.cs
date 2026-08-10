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

        /// <summary>
        /// 根据类型的 ConfigPathAttribute 决定配置文件路径。
        /// 有 [ConfigPath] 时用作相对路径（可含 / 表示子目录），否则用类型名。
        /// 例如 [ConfigPath("压力传感器/左耦合左")] → Configs/压力传感器/左耦合左.json
        /// </summary>
        private static string GetConfigFileName(Type type)
        {
            var attr = type.GetCustomAttributes(typeof(ConfigPathAttribute), false)
                .OfType<ConfigPathAttribute>()
                .FirstOrDefault();

            if (attr != null)
                return attr.RelativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar) + ".json";

            return type.Name + ".json";
        }

        public async Task<bool> SaveAsync<T>(T config) where T : class
        {
            try
            {
                ArgumentException.ThrowIfNullOrEmpty(nameof(config));
                var path = Path.Combine(ConfigBasePath, GetConfigFileName(config!.GetType()));
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
                var path = Path.Combine(ConfigBasePath, GetConfigFileName(typeof(T)));
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
                var path = Path.Combine(ConfigBasePath, GetConfigFileName(type));
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
                var path = Path.Combine(ConfigBasePath, GetConfigFileName(type));
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
