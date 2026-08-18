using AFOCS.Infrastructure;

namespace AFOCS.App.Models;

/// <summary>
/// 左右工位生产信息配置：逻辑流程、安全位置流程、回零流程地址及胶水 SN。
/// 通过 IConfigService 持久化到 Configs/生产信息.json。
/// </summary>
[ConfigPath("生产信息")]
public class ProductionInfoConfig
{
    public StationProductionInfo Left { get; set; } = new();

    public StationProductionInfo Right { get; set; } = new();
}

/// <summary>单个工位的生产信息</summary>
public class StationProductionInfo
{
    /// <summary>逻辑流程地址</summary>
    public string LogicFlowPath { get; set; } = string.Empty;

    /// <summary>安全位置流程地址</summary>
    public string SafePositionFlowPath { get; set; } = string.Empty;

    /// <summary>回零流程地址</summary>
    public string HomeFlowPath { get; set; } = string.Empty;

    /// <summary>胶水 SN</summary>
    public string GlueSn { get; set; } = string.Empty;
}