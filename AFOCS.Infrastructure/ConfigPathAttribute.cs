namespace AFOCS.Infrastructure;

/// <summary>
/// 指定配置文件相对于 ConfigBasePath 的相对路径（可含 / 表示子目录），不含 .json 后缀。
/// 例如 [ConfigPath("压力传感器/左耦合左")] → Configs/压力传感器/左耦合左.json
/// 未标注时默认用类型名作为文件名。
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class ConfigPathAttribute(string relativePath) : Attribute
{
    public string RelativePath { get; } = relativePath;
}
