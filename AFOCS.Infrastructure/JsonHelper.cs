using System.Text.Json;
using System.Text.Json.Serialization;

namespace AFOCS.Infrastructure
{
    public static class JsonHelper
    {
        // 全局统一序列化配置
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,                          // 格式化换行输出
            PropertyNameCaseInsensitive = true,             // 反序列化忽略大小写
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // 不输出null字段
            Converters = { new JsonStringEnumConverter() }  // 枚举输出字符串而非数字
        };

        #region 基础序列化
        /// <summary>对象转JSON字符串</summary>
        public static string Serialize<T>(T obj)
        {
            if (obj == null) return string.Empty;
            return JsonSerializer.Serialize(obj, JsonOptions);
        }

        /// <summary>JSON字符串转对象</summary>
        public static T Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentNullException(nameof(json));
            return JsonSerializer.Deserialize<T>(json, JsonOptions)!;
        }
        #endregion

        #region JSON 文件写入
        /// <summary>将对象序列化为JSON并写入文件</summary>
        /// <param name="filePath">文件完整路径</param>
        /// <param name="obj">待写入对象</param>
        public static void WriteToFile<T>(string filePath, T obj)
        {
            string json = Serialize(obj);
            // 自动创建目录
            string dir = Path.GetDirectoryName(filePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(filePath, json);
        }

        /// <summary>异步写入JSON文件</summary>
        public static async Task WriteToFileAsync<T>(string filePath, T obj)
        {
            string json = Serialize(obj);
            string dir = Path.GetDirectoryName(filePath)!;
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(filePath, json);
        }
        #endregion

        #region JSON 文件读取
        /// <summary>读取JSON文件并反序列化为对象</summary>
        public static T ReadFromFile<T>(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("文件不存在", filePath);
            string json = File.ReadAllText(filePath);
            return Deserialize<T>(json);
        }

        /// <summary>异步读取JSON文件</summary>
        public static async Task<T> ReadFromFileAsync<T>(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("文件不存在", filePath);
            string json = await File.ReadAllTextAsync(filePath);
            return Deserialize<T>(json);
        }
        #endregion
    }
}
