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
/// 压力停止运动节点：选择轴和压力传感器方向，轴向指定方向连续运动，
/// 同时并行读取压力，一旦压力达到目标值立即停止运动。
/// 工位由入口节点传入（context["WorkPos"]），总线轴使用定长运动，雅克贝斯轴使用相对运动。
/// 最大移动距离作为安全限制，运动到此距离仍未达标则停止。
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

    [DisplayName("采样间隔(ms)")]
    [Description("运动期间读取压力的时间间隔，单位毫秒")]
    [NodePort("IntervalMs", "采样间隔", NodePortType.Int, true)]
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

    // ==================== 总线轴：连续运动 + 并行读取压力，达标即停 ====================

    private async Task MoveBusAxisUntilPressure(IPressureSensor sensor, WorkPos station)
    {
        var busId = Axis.ToBusAxisId(station);

        if (Math.Sign(MaxDistance) == 0) return;

        // 启动连续运动（内部按轴配置设置速度并等待到位，运动期间可被 StopAxisAsync 打断）
        var moveTask = busAxisDevice.MovePmoveAsync(busId, MaxDistance);

        var reached = false;
        try
        {
            while (!moveTask.IsCompleted)
            {
                var pressure = await ReadPressureChannelAsync(sensor);
                if (pressure >= TargetPressure)
                {
                    reached = true;
                    logger.Information("压力达到目标: 当前={Pressure}, 目标={Target}, 停止运动",
                        pressure, TargetPressure);
                    await busAxisDevice.StopAxisAsync(busId);
                    break;
                }
                await Task.Delay(IntervalMs);
            }
        }
        finally
        {
            // 兜底：若运动尚未自然结束，确保轴停止
            if (!moveTask.IsCompleted)
            {
                var stop = await busAxisDevice.StopAxisAsync(busId);
                if (!stop.IsSuccess)
                    logger.Warning("最终停止轴失败: {Error}", stop.Message);
            }
        }

        var moveResult = await moveTask;
        if (reached) return;

        if (!moveResult.IsSuccess)
            throw new InvalidOperationException($"运动失败: {moveResult.Message}");

        logger.Warning("达到最大移动距离但压力未达标: 当前压力={Pressure}, 目标={Target}",
            await ReadPressureChannelAsync(sensor), TargetPressure);
    }

    // ==================== 雅克贝斯轴：连续运动 + 并行读取压力，达标即停 ====================

    private async Task MoveAkribisAxisUntilPressure(IPressureSensor sensor, WorkPos station)
    {
        var (instanceName, akAxis) = Axis.ToAkribis(station);
        var akribisInstances = akribisMotions.ToDictionary(m => m.GetType().Name);

        if (!akribisInstances.TryGetValue(instanceName, out var motion))
            throw new InvalidOperationException($"{Axis.GetDescription()}: 未找到控制器 {instanceName}");

        if (Math.Sign(MaxDistance) == 0) return;

        // 启动连续相对运动（阻塞式等待到位，运动期间可被 StopAxisAsync 打断）
        var moveTask = motion.MoveRelativeAsync(akAxis, (int)MaxDistance);

        var reached = false;
        try
        {
            while (!moveTask.IsCompleted)
            {
                var pressure = await ReadPressureChannelAsync(sensor);
                if (pressure >= TargetPressure)
                {
                    reached = true;
                    logger.Information("压力达到目标: 当前={Pressure}, 目标={Target}, 停止运动",
                        pressure, TargetPressure);
                    await motion.StopAxisAsync(akAxis);
                    break;
                }
                await Task.Delay(IntervalMs);
            }
        }
        finally
        {
            if (!moveTask.IsCompleted)
            {
                var stop = await motion.StopAxisAsync(akAxis);
                if (!stop.IsSuccess)
                    logger.Warning("最终停止轴失败: {Error}", stop.Message);
            }
        }

        var moveResult = await moveTask;
        if (reached) return;

        if (!moveResult.IsSuccess)
            throw new InvalidOperationException($"运动失败: {moveResult.Message}");

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
