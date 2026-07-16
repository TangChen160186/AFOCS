namespace AFOCS.Infrastructure.Extensions
{
    public static class EnumExtensions
    {
        /// <summary>
        /// 将枚举值转换为字符串，如果枚举值无效则返回默认字符串
        /// </summary>
        public static string GetName<T>(this T enumValue, string defaultValue = "") where T : Enum
        {
            return Enum.IsDefined(typeof(T), enumValue)
                ? enumValue.ToString()
                : defaultValue;
        }
    }
}
