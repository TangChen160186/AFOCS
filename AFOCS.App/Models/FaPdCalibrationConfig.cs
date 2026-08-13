using AFOCS.Infrastructure;

namespace AFOCS.App.Models;

/// <summary>
/// FA 下表面到 PD 测高的标定配置（全局只标定一次）。
/// 通过 IConfigService 持久化为 JSON。
///
/// 计算逻辑：
///   H_total = (P1 - P0) + H_pd
///   Δd      = (Y1 - Y0) × Precision
///   H_final = H_total - Δd - H0
/// 其中 P0/H0/Y0 为标定值，P1/H_pd/Y1 为运行时值。
/// </summary>
[ConfigPath("标定/FA下表面PD测高")]
public class FaPdCalibrationConfig
{
    /// <summary>是否已完成标定</summary>
    public bool IsCalibrated { get; set; }

    /// <summary>标定工位</summary>
    public WorkPos Station { get; set; } = WorkPos.Left;

    /// <summary>测高方向轴</summary>
    public EAxis Axis { get; set; } = EAxis.CouplingLZ;

    /// <summary>标定示教点的轴位置 P0</summary>
    public double AxisPosition { get; set; }

    /// <summary>标定测高值 H0</summary>
    public double HeightValue { get; set; }

    /// <summary>标定视觉找点像素 X（Y0.x）</summary>
    public double PixelX { get; set; }

    /// <summary>标定视觉找点像素 Y（Y0.y）</summary>
    public double PixelY { get; set; }

    /// <summary>相机精度 (mm/pixel)</summary>
    public double Precision { get; set; }

    /// <summary>标定使用的相机名</summary>
    public string CameraName { get; set; } = string.Empty;

    /// <summary>视觉模板路径</summary>
    public string TemplatePath { get; set; } = string.Empty;
}
