using AFOCS.Devices;
using AFOCS.Devices.Implementation;
using AFOCS.Infrastructure;
using System.ComponentModel;

namespace AFOCS.App.Models;

// ==================== 统一轴枚举 ====================

public enum EAxis : byte
{
    // ========== 相机轴（总线）==========
    [Description("上相机 X 轴")] CamUpX = 0,
    [Description("上相机 Y 轴")] CamUpY = 1,
    [Description("上相机 Z 轴")] CamUpZ = 2,
    [Description("侧相机 Y 轴")] CamSideY = 3,

    // ========== 耦合旋转轴（总线）==========
    [Description("左耦合 θX 轴")] CouplingLThetaX = 4,
    [Description("左耦合 θY 轴")] CouplingLThetaY = 5,
    [Description("左耦合 θZ 轴")] CouplingLThetaZ = 6,
    [Description("右耦合 θX 轴")] CouplingRThetaX = 7,
    [Description("右耦合 θY 轴")] CouplingRThetaY = 8,
    [Description("右耦合 θZ 轴")] CouplingRThetaZ = 9,

    // ========== 耦合直线轴（雅克贝斯）==========
    [Description("左耦合 X 轴")] CouplingLX = 10,
    [Description("左耦合 Y 轴")] CouplingLY = 11,
    [Description("左耦合 Z 轴")] CouplingLZ = 12,
    [Description("右耦合 X 轴")] CouplingRX = 13,
    [Description("右耦合 Y 轴")] CouplingRY = 14,
    [Description("右耦合 Z 轴")] CouplingRZ = 15,
}

public static class EAxisExtensions
{
    /// <summary>是否为 EtherCAT 总线轴（值 0–9）</summary>
    public static bool IsBusAxis(this EAxis axis) => (byte)axis <= 9;

    /// <summary>是否为雅克贝斯直连轴（值 10–15）</summary>
    public static bool IsAkribisAxis(this EAxis axis) => (byte)axis >= 10;

    /// <summary>EtherCAT 轴 → BusAxisId（考虑工位偏移）</summary>
    public static BusAxisId ToBusAxisId(this EAxis axis, WorkPos station)
    {
        var baseVal = (int)axis;
        if (station == WorkPos.Right) baseVal += 10;
        return (BusAxisId)baseVal;
    }

    /// <summary>雅克贝斯轴 → (实例 TypeName, AkribisAxisId)</summary>
    public static (string instanceName, AkribisAxisId axisId) ToAkribis(this EAxis axis, WorkPos station)
    {
        var isLeft = station == WorkPos.Left;
        var val = (int)axis;
        // CouplingL: 10–12, CouplingR: 13–15
        var isL = val <= (int)EAxis.CouplingLZ;
        var offset = isL ? val - (int)EAxis.CouplingLX : val - (int)EAxis.CouplingRX;
        var akAxis = offset switch
        {
            0 => AkribisAxisId.X,
            1 => AkribisAxisId.Y,
            2 => AkribisAxisId.Z,
            _ => AkribisAxisId.X,
        };
        var instance = isLeft
            ? (isL ? nameof(AkribisLeftCouplingL) : nameof(AkribisLeftCouplingR))
            : (isL ? nameof(AkribisRightCouplingL) : nameof(AkribisRightCouplingR));
        return (instance, akAxis);
    }
}