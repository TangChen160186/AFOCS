using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.App.Models;
using AFOCS.Devices.AkribrisMotion;
using AFOCS.Devices.BusAxisDevice;
using AFOCS.Devices.MotionControlCard;
using AFOCS.Devices.PressureSensor;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using Serilog;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Motion;

/// <summary>
/// 压力停止运动节点：选择轴和压力传感器方向，轴持续移动直到压力传感器达到目标值后自动停止。
/// 工位由入口节点传入（context["WorkPos"]），总线轴使用定长运动（后台非阻塞），雅克贝斯轴使用相对运动。
/// 最大移动距离作为安全限制，防止无限运动。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.MoveUntilPressure", "压力停止运动", "运动")]
[CategoryOrder("基础", 0), CategoryOrder("配置", 1), CategoryOrder("输入", 2), CategoryOrder("输出", 3)]
[method: ImportingConstructor]
public class MoveUntilPressureNodeDefinition(
    IBusAxisDevice busAxisDevice,
    IMotionControlCard motionCard,
    ILogger logger,
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
    } = EAxis.CouplingLThetaX;

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

    [DisplayName("检测间隔(ms)")]
    [Description("压力传感器轮询间隔，默认 50ms")]
    [Category("配置")]
    public int PollIntervalMs
    {
        get;
        set => Set(ref field, value);
    } = 50;

    public async Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        if (!context.TryGetValue("WorkPos", out var workPosObj) || workPosObj is not WorkPos station)
            throw new InvalidOperationException("流程上下文缺少工位（WorkPos），请确认已连接入口节点并设置工位");

        var sensor = FindSensor(pressureSensors, SensorType, station);
        if (sensor == null)
            throw new InvalidOperationException(
                $"未找到匹配的压力传感器: {SensorType.GetDescription()}, 工位={station}");

        logger.Information("开始压力停止运动: 轴={Axis}, 传感器={SensorType}.{Channel}, 目标={Target}, 最大距离={Dist}",
            Axis.GetDescription(), SensorType.GetDescription(), Channel, TargetPressure, MaxDistance);

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

        logger.Information("压力停止运动完成: 轴={Axis}, 传感器={Type}.{Ch}",
            Axis.GetDescription(), SensorType.GetDescription(), Channel);
        return new Dictionary<string, object?>();
    }

    // ==================== 总线轴：非阻塞 PMove + 轮询压力 ====================

    private async Task MoveBusAxisUntilPressure(IPressureSensor sensor, WorkPos station)
    {
        var busId = Axis.ToBusAxisId(station);
        var axisIndex = (ushort)busId;

        // 确保轴已使能
        var enableResult = await busAxisDevice.EnableAxisAsync(busId);
        if (!enableResult.IsSuccess)
            throw new InvalidOperationException($"轴使能失败: {enableResult.Message}");

        try
        {
            // 启动非阻塞定长运动（dmc_pmove_unit 启动后立即返回，运动在控制器后台执行）
            var moveResult = await motionCard.PmoveUnitAsync(axisIndex, MaxDistance, posiMode: 0);
            if (!moveResult.IsSuccess)
                throw new InvalidOperationException($"启动运动失败: {moveResult.Message}");

            logger.Information("总线轴运动已启动: axis={BusId}, distance={Dist}", busId, MaxDistance);

            // 轮询压力传感器，达到目标值则停止
            var stopped = false;
            while (!stopped)
            {
                await Task.Delay(PollIntervalMs);

                var pressure = ReadPressureChannel(sensor);
                if (pressure >= TargetPressure)
                {
                    logger.Information("压力达到目标: 当前={Pressure}, 目标={TargetPressure}, 停止运动",
                        pressure, TargetPressure);
                    await busAxisDevice.StopAxisAsync(busId);
                    stopped = true;
                    continue;
                }

                // 安全检查：轴是否已停止（可能触发限位或行程走完）
                var doneResult = await motionCard.CheckDoneAsync(axisIndex);
                if (doneResult.IsSuccess && doneResult.Data == 1)
                {
                    logger.Warning("轴已停止但压力未达标: 当前压力={Pressure}, 目标={TargetPressure}", pressure, TargetPressure);
                    break;
                }
            }
        }
        finally
        {
            // 确保轴停止
            var finalStop = await busAxisDevice.StopAxisAsync(busId);
            if (!finalStop.IsSuccess)
                logger.Warning("最终停止轴失败: {Error}", finalStop.Message);
        }
    }

    // ==================== 雅克贝斯轴：后台相对运动 + 轮询压力 ====================

    private async Task MoveAkribisAxisUntilPressure(IPressureSensor sensor, WorkPos station)
    {
        var (instanceName, akAxis) = Axis.ToAkribis(station);
        var akribisInstances = akribisMotions.ToDictionary(m => m.GetType().Name);

        if (!akribisInstances.TryGetValue(instanceName, out var motion))
            throw new InvalidOperationException($"{Axis.GetDescription()}: 未找到控制器 {instanceName}");

        // 后台启动相对运动（MoveRelativeAsync 会阻塞直到运动完成或 StopAxisAsync 中断）
        var moveTask = Task.Run(async () =>
        {
            try
            {
                // 使用大距离让其持续运动，直到被 StopAxisAsync 中断
                await motion.MoveRelativeAsync(akAxis, (int)MaxDistance);
            }
            catch (Exception ex)
            {
                logger.Debug("雅克贝斯运动任务结束（可能被停止中断）: {Error}", ex.Message);
            }
        });

        try
        {
            var stopped = false;
            while (!stopped && !moveTask.IsCompleted)
            {
                await Task.Delay(PollIntervalMs);

                var pressure = ReadPressureChannel(sensor);
                if (pressure >= TargetPressure)
                {
                    logger.Information("压力达到目标: 当前={Pressure}, 目标={TargetPressure}, 紧急停止运动",
                        pressure, TargetPressure);
                    await motion.EmergencyStopAsync(akAxis);
                    stopped = true;
                }
            }

            if (!stopped && moveTask.IsCompleted)
                logger.Warning("雅克贝斯轴运动已完成但压力未达标");
        }
        finally
        {
            // 确保停止
            await motion.EmergencyStopAsync(akAxis);
            try { await moveTask; } catch { /* 预期的停止中断异常 */ }
        }
    }

    // ==================== 辅助方法 ====================

    private int ReadPressureChannel(IPressureSensor sensor)
    {
        return Channel switch
        {
            PressureChannel.X => sensor.GetX(),
            PressureChannel.Y => sensor.GetY(),
            PressureChannel.Z => sensor.GetZ(),
            _ => 0
        };
    }

    /// <summary>从所有压力传感器中根据 SensorType + WorkPos 匹配具体实例</summary>
    private static IPressureSensor? FindSensor(
        IEnumerable<IPressureSensor> sensors, PressureSensorType sensorType, WorkPos workPos)
    {
        foreach (var s in sensors)
        {
            if (s.SensorType != sensorType) continue;

            var workPosProp = s.GetType().GetProperty("WorkPos");
            if (workPosProp != null && workPosProp.GetValue(s) is WorkPos wp && wp == workPos)
                return s;
        }
        return null;
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
