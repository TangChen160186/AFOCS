using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.App.Models;
using AFOCS.Devices.AkribrisMotion;
using AFOCS.Devices.BusAxisDevice;
using AFOCS.Devices.PressureSensor;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Motion;

/// <summary>
/// 压力停止运动节点：选择轴和压力传感器方向，轴按指定步长逐步运动，
/// 每走完一步读取一次压力，达到目标值后停止，未达标则继续走下一步。
/// 工位由入口节点传入（context["WorkPos"]），总线轴使用定长运动，雅克贝斯轴使用相对运动。
/// 最大移动距离作为安全限制，防止无限运动。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.MoveUntilPressure", "压力停止运动", "运动")]
[CategoryOrder("基础", 0),
 CategoryOrder("配置", 1), 
 CategoryOrder("输入", 2), 
 CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class MoveUntilPressureNodeDefinition(
    ILogger logger,
    IBusAxisDevice busAxisDevice,
    [ImportMany] IEnumerable<IAkribisMotion> akribisMotions,
    [ImportMany] IEnumerable<IPressureSensor> pressureSensors)
    : NodeDefinitionBase, IExecutableNode
{
    [DisplayName("轴")]
    [ItemsSource(typeof(AxisItemsSource))]
    [Category("配置")]
    public EAxis Axis
    {
        get;
        set => Set(ref field, value);
    } = EAxis.CouplingLThetaZ;

    [DisplayName("压力传感器")]
    [Description("选择要监测的压力传感器类型")]
    [ItemsSource(typeof(PressureSensorTypeItemsSource))]
    [Category("配置")]
    public PressureSensorType SensorType
    {
        get;
        set => Set(ref field, value);
    } = PressureSensorType.LeftCoupling;

    [DisplayName("传感器方向")]
    [Description("选择压力传感器的通道方向（X/Y/Z）")]
    [ItemsSource(typeof(PressureChannelItemsSource))]
    [Category("配置")]
    public PressureChannel Channel
    {
        get;
        set => Set(ref field, value);
    } = PressureChannel.Z;

    [DisplayName("目标压力值")]
    [Description("当压力传感器达到此值（≥）时停止运动")]
    [NodePort("TargetPressure", "目标压力值", NodePortType.Int, true)]
    [Category("输入")]
    public int TargetPressure
    {
        get;
        set => Set(ref field, value);
    } = 400;

    [DisplayName("最大移动距离")]
    [Description("安全限制：轴移动的最大距离（脉冲值），正值正向、负值反向")]
    [NodePort("MaxDistance", "最大移动距离", NodePortType.Double, true)]
    [Category("输入")]
    public double MaxDistance
    {
        get;
        set => Set(ref field, value);
    } = 10000.0;

    [DisplayName("步长")]
    [Description("每次运动的脉冲数（正数），方向由最大移动距离的符号决定")]
    [NodePort("StepDistance", "步长", NodePortType.Double, true)]
    [Category("输入")]
    public double StepDistance
    {
        get;
        set => Set(ref field, value);
    } = 100.0;

    [DisplayName("步间延迟(ms)")]
    [Description("每步运动完成后、读取压力前等待的时间，用于压力传感器稳定")]
    [NodePort("IntervalMs", "步间延迟", NodePortType.Int, true)]
    [Category("输入")]
    public int IntervalMs
    {
        get;
        set => Set(ref field, value);
    } = 20;

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        if (!context.TryGetValue("WorkPos", out var workPosObj) || workPosObj is not WorkPos station)
            throw new InvalidOperationException("流程上下文缺少工位（WorkPos），请确认已连接入口节点并设置工位");

        var sensor = FindSensor(pressureSensors, SensorType, station);
        if (sensor == null)
            throw new InvalidOperationException(
                $"未找到匹配的压力传感器: {SensorType.GetDescription()}, 工位={station}");


        if (Axis.IsBusAxis())
        {
            await MoveBusAxisUntilPressure(sensor, station);
        }
        else if (Axis.IsAkribisAxis())
        {
            await MoveAkribisAxisUntilPressure(sensor, station);
        }
        else
        {
            throw new InvalidOperationException($"{Axis.GetDescription()}: 未知轴类型");
        }

        return new Dictionary<string, object?>();
    }

    // ==================== 总线轴：步进式定长运动 + 逐步读取压力 ====================

    private async Task MoveBusAxisUntilPressure(IPressureSensor sensor, WorkPos station)
    {
        var busId = Axis.ToBusAxisId(station);

        var direction = Math.Sign(MaxDistance);
        if (direction == 0) return;

        var stepAbs = Math.Abs(StepDistance);
        if (stepAbs <= 0) throw new InvalidOperationException("步长必须大于 0");

        var totalAbs = Math.Abs(MaxDistance);
        var moved = 0.0;
        try
        {
            while (moved < totalAbs)
            {
                // 每步距离 = min(步长, 剩余距离)，方向与最大距离一致
                var step = Math.Min(stepAbs, totalAbs - moved) * direction;

                var moveResult = await busAxisDevice.MovePmoveAsync(busId, step);
                if (!moveResult.IsSuccess)
                    throw new InvalidOperationException($"运动失败: {moveResult.Message}");

                moved += Math.Abs(step);

                // 等待压力稳定后读取
                if (IntervalMs > 0)
                    await Task.Delay(IntervalMs);

                var pressure = await ReadPressureChannelAsync(sensor);
                if (pressure >= TargetPressure)
                {
                    logger.Information("压力达到目标: 当前={Pressure}, 目标={Target}, 停止运动",
                        pressure, TargetPressure);
                    return;
                }

                logger.Debug("压力未达标，继续下一步: 当前={Pressure}, 目标={Target}, 已走={Moved}/{Total}",
                    pressure, TargetPressure, moved, totalAbs);
            }

            logger.Warning("达到最大移动距离但压力未达标: 当前压力={Pressure}, 目标={Target}",
                await ReadPressureChannelAsync(sensor), TargetPressure);
        }
        finally
        {
            var finalStop = await busAxisDevice.StopAxisAsync(busId);
            if (!finalStop.IsSuccess)
                logger.Warning("最终停止轴失败: {Error}", finalStop.Message);
        }
    }

    // ==================== 雅克贝斯轴：步进式相对运动 + 逐步读取压力 ====================

    private async Task MoveAkribisAxisUntilPressure(IPressureSensor sensor, WorkPos station)
    {
        var (instanceName, akAxis) = Axis.ToAkribis(station);
        var akribisInstances = akribisMotions.ToDictionary(m => m.GetType().Name);

        if (!akribisInstances.TryGetValue(instanceName, out var motion))
            throw new InvalidOperationException($"{Axis.GetDescription()}: 未找到控制器 {instanceName}");

        var direction = Math.Sign(MaxDistance);
        if (direction == 0) return;

        var stepAbs = Math.Abs(StepDistance);
        if (stepAbs <= 0) throw new InvalidOperationException("步长必须大于 0");

        var totalAbs = Math.Abs(MaxDistance);
        var moved = 0.0;
        while (moved < totalAbs)
        {
            // 每步距离 = min(步长, 剩余距离)，方向与最大距离一致
            var step = Math.Min(stepAbs, totalAbs - moved) * direction;

            var moveResult = await motion.MoveRelativeAsync(akAxis, (int)step);
            if (!moveResult.IsSuccess)
                throw new InvalidOperationException($"运动失败: {moveResult.Message}");

            moved += Math.Abs(step);

            // 等待压力稳定后读取
            if (IntervalMs > 0)
                await Task.Delay(IntervalMs);

            var pressure = await ReadPressureChannelAsync(sensor);
            if (pressure >= TargetPressure)
            {
                logger.Information("压力达到目标: 当前={Pressure}, 目标={Target}, 停止运动",
                    pressure, TargetPressure);
                return;
            }

            logger.Debug("压力未达标，继续下一步: 当前={Pressure}, 目标={Target}, 已走={Moved}/{Total}",
                pressure, TargetPressure, moved, totalAbs);
        }

        logger.Warning("达到最大移动距离但压力未达标: 当前压力={Pressure}, 目标={Target}",
            await ReadPressureChannelAsync(sensor), TargetPressure);
    }

    // ==================== 辅助方法 ====================

    private async Task<int> ReadPressureChannelAsync(IPressureSensor sensor)
    {
        Result<int> result = Channel switch
        {
            PressureChannel.X => await sensor.ReadXAsync(),
            PressureChannel.Y => await sensor.ReadYAsync(),
            PressureChannel.Z => await sensor.ReadZAsync(),
            _ => Result<int>.Fail("未知压力通道")
        };

        if (!result.IsSuccess)
            logger.Warning("读取压力失败: {Error}", result.Message);

        return result.IsSuccess ? result.Data : 0;
    }

    /// <summary>从所有压力传感器中根据 SensorType + WorkPos 匹配具体实例</summary>
    private static IPressureSensor? FindSensor(
        IEnumerable<IPressureSensor> sensors, PressureSensorType sensorType, WorkPos workPos)
    {

        return sensors.FirstOrDefault(e=>e.WorkPos == workPos && e.SensorType == sensorType);
    }
}

// ==================== ItemsSource ====================

public class PressureSensorTypeItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        foreach (var type in Enum.GetValues<PressureSensorType>())
            items.Add(type, type.GetDescription());
        return items;
    }
}

public class PressureChannelItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        foreach (var channel in Enum.GetValues<PressureChannel>())
            items.Add(channel, channel.ToString());
        return items;
    }
}
