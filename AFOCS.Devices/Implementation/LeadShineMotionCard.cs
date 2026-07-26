using System.ComponentModel.Composition;
using System.Text;
using AFOCS.Infrastructure;
using Serilog;

namespace AFOCS.Devices.Implementation;

public class LeadShineMotionCardConfig : ICloneable
{
    public string EniPath { get; set; } = "";
    public string IniPath { get; set; } = "";
    public int TimeoutMs { get; set; } = 30000;

    public LeadShineMotionCardConfig Clone() => new()
    {
        EniPath = EniPath,
        IniPath = IniPath,
        TimeoutMs = TimeoutMs,
    };

    object ICloneable.Clone() => Clone();
}

[Export]
[method: ImportingConstructor]
[Export(typeof(IMotionControlCard))]
public class LeadShineMotionCard(IConfigService configService, ILogger logger) : IMotionControlCard
{
    public event EventHandler<MotionCardConnectionChangedEventArgs>? ConnectionChanged;

    public bool IsConnected
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            ConnectionChanged?.Invoke(this, new MotionCardConnectionChangedEventArgs(value));
        }
    }

    private ushort _cardNo = 0;
    private LeadShineMotionCardConfig _config = new();
    private const ushort EniFileType = 200;
    private const ushort ConfigFileType = 201;
    private const ushort EthercatPort = 2;

    // ========== 板卡初始化 ==========

    public async Task<Result> InitializeAsync(CancellationToken token = default)
    {
        try
        {
            _config = await configService.LoadAsync<LeadShineMotionCardConfig>()
                      ?? new LeadShineMotionCardConfig();

            short cardNum = LTDMC.dmc_board_init();
            if (cardNum == 0)
                return Result.Fail($"没有找到控制卡，或者控制卡异常, card num:{cardNum}");
            if (cardNum < 0 || cardNum > 8)
                return Result.Fail($"控制卡数量异常，card num:{cardNum}");

            ushort usNum = 0;
            ushort[] cardList = new ushort[8];
            uint[] cardTypes = new uint[8];
            var ret = LTDMC.dmc_get_CardInfList(ref usNum, cardTypes, cardList);
            if (ret != 0)
                return Result.Fail($"调用API失败:{nameof(LTDMC.dmc_get_CardInfList)}, error code: {ret}");
            _cardNo = cardList[0];
            logger.Information("当前板卡号为: {CardNo}", _cardNo);

            ret = LTDMC.nmc_set_slave_output_retain(_cardNo, 1);
            if (ret != 0)
                return Result.Fail($"调用API失败:{nameof(LTDMC.nmc_set_slave_output_retain)}, error code: {ret}");

            ushort nmcErr = 0;
            LTDMC.nmc_get_errcode(_cardNo, EthercatPort, ref nmcErr);

            if (nmcErr == 0x000C || nmcErr == 0x001E)
            {
                ret = LTDMC.nmc_set_cycletime(_cardNo, EthercatPort, 1000);
                if (ret != 0)
                    return Result.Fail($"调用API失败:{nameof(LTDMC.nmc_set_cycletime)}, error code: {ret}");

                logger.Information("下载ENI总线配置文件");
                var eniResult = DownloadFile(_cardNo, _config.EniPath, EniFileType);
                if (!eniResult.IsSuccess)
                    return eniResult;

                logger.Information("执行热复位...");
                var resetResult = await HotResetAsync();
                if (!resetResult.IsSuccess)
                    return resetResult;

                logger.Information("等待总线恢复...");
                int timeout = 0;
                while (timeout < 5000)
                {
                    if (token.IsCancellationRequested)
                        return Result.Fail("初始化被取消");
                    LTDMC.nmc_get_errcode(_cardNo, EthercatPort, ref nmcErr);
                    if (nmcErr == 0)
                        break;
                    await Task.Delay(50, token);
                    timeout += 50;
                }
                if (nmcErr != 0)
                    return Result.Fail($"总线热复位后仍未恢复，错误码: 0x{nmcErr:X4}");
                logger.Information("总线恢复成功");
            }
            else if (nmcErr != 0)
            {
                return Result.Fail($"总线错误: 0x{nmcErr:X4}");
            }

            if (!string.IsNullOrWhiteSpace(_config.IniPath) && File.Exists(_config.IniPath))
            {
                logger.Information("下载轴参数配置文件");
                var iniResult = DownloadFile(_cardNo, _config.IniPath, ConfigFileType);
                if (!iniResult.IsSuccess)
                    return iniResult;
            }

            LTDMC.nmc_get_errcode(_cardNo, EthercatPort, ref nmcErr);
            if (nmcErr != 0)
                return Result.Fail($"总线错误: 0x{nmcErr:X4}");

            LTDMC.nmc_set_axis_enable(_cardNo, 255);

            logger.Information("雷赛板卡初始化成功");
            IsConnected = true;
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, "板卡初始化异常");
            return Result.Fail($"初始化异常: {ex.Message}");
        }
    }

    public async Task<Result> StopAsync(CancellationToken token = default)
    {
        if (IsConnected)
        {
            await Task.Run(() => LTDMC.dmc_board_close());
            IsConnected = false;
        }
        return Result.Success();
    }

    public async Task<Result> ReConnectAsync(CancellationToken token = default)
    {
        await StopAsync(token);
        return await InitializeAsync(token);
    }

    // ========== 复位 ==========

    public async Task<Result> HotResetAsync()
    {
        var ret = LTDMC.dmc_soft_reset(_cardNo);
        if (ret != 0)
            return Result.Fail($"调用API失败:{nameof(LTDMC.dmc_soft_reset)}, error code: {ret}");
        return Result.Success();
    }

    public async Task<Result> ColdResetAsync()
    {
        var ret = LTDMC.dmc_cool_reset(_cardNo);
        if (ret != 0)
            return Result.Fail($"调用API失败:{nameof(LTDMC.dmc_cool_reset)}, error code: {ret}");

        ret = LTDMC.dmc_board_close();
        if (ret != 0)
            return Result.Fail($"调用API失败:{nameof(LTDMC.dmc_board_close)}, error code: {ret}");

        await Task.Delay(TimeSpan.FromSeconds(15));

        ret = LTDMC.dmc_board_init();
        if (ret <= 0)
            return Result.Fail($"冷复位后重新初始化板卡失败, ret: {ret}");

        ushort usNum = 0;
        ushort[] cardList = new ushort[8];
        uint[] cardTypes = new uint[8];
        ret = LTDMC.dmc_get_CardInfList(ref usNum, cardTypes, cardList);
        if (ret != 0)
            return Result.Fail($"调用API失败:{nameof(LTDMC.dmc_get_CardInfList)}, error code: {ret}");
        _cardNo = cardList[0];

        return Result.Success();
    }

    // ========== 总线状态 ==========

    public Task<Result<(ushort ErrorCode, string Description)>> GetBusStatusAsync()
    {
        ushort nmcErr = 0;
        var ret = LTDMC.nmc_get_errcode(_cardNo, EthercatPort, ref nmcErr);
        if (ret != 0)
            return Task.FromResult(Result<(ushort, string)>.Fail($"读取总线状态失败, error code: {ret}"));
        return Task.FromResult(Result<(ushort, string)>.Success((nmcErr, GetBusErrorDescription(nmcErr))));
    }

    private static string GetBusErrorDescription(ushort errCode)
    {
        return errCode switch
        {
            0x0000 => "总线正常，EtherCAT 运行中",
            0x0001 => "无 EtherCAT 从站",
            0x0002 => "从站数量不匹配",
            0x0003 => "从站信息不匹配",
            0x0004 => "从站初始化失败",
            0x0005 => "从站未进入 OP 状态",
            0x0006 => "SM 看门狗超时",
            0x0007 => "DC 时钟同步失败",
            0x0008 => "EEPROM 加载失败",
            0x0009 => "SDO 下载失败",
            0x000A => "SDO 上传失败",
            0x000B => "PDO 映射失败",
            0x000C => "ENI 文件缺失（需下载 ENI）",
            0x000D => "从站 AL 状态错误",
            0x000E => "看门狗错误",
            0x000F => "从站 DC 配置失败",
            0x0010 => "EEPROM 重新加载失败",
            0x0011 => "从站 SM 配置失败",
            0x0012 => "从站 PDO 看门狗错误",
            0x0013 => "从站通信错误",
            0x0014 => "从站返回错误",
            0x0015 => "从站 AL 状态超时",
            0x0016 => "主站状态异常",
            0x0017 => "从站响应超时",
            0x0018 => "链路丢失",
            0x0019 => "无效的帧",
            0x001A => "CRC 校验错误",
            0x001B => "物理层错误",
            0x001C => "从站端口未打开",
            0x001D => "无效的从站配置",
            0x001E => "INI 配置不匹配（需重新下载）",
            0x001F => "帧丢失",
            0x0020 => "从站数量不足",
            0x0021 => "从站丢失",
            0x0022 => "主站初始化未完成",
            _ => $"未知错误 (0x{errCode:X4})"
        };
    }

    // ========== 板卡配置 ==========

    public LeadShineMotionCardConfig GetConfig() => _config.Clone();

    public async Task SaveConfigAsync(LeadShineMotionCardConfig config)
    {
        _config = config.Clone();
        await configService.SaveAsync(_config);
    }

    // ========== IO 读写 ==========

    public async Task<Result<bool>> ReadInbitAsync(ushort bitNo)
    {
        short level = LTDMC.dmc_read_inbit(_cardNo, bitNo);
        if (level < 0)
            return Result<bool>.Fail($"读取输入口 {bitNo} 失败");
        return Result<bool>.Success(level == 1);
    }

    public async Task<Result<bool[]>> ReadInbitsAsync(ushort bitCount)
    {
        var bits = new bool[bitCount];
        for (ushort i = 0; i < bitCount; i++)
        {
            var result = await ReadInbitAsync(i);
            if (!result.IsSuccess)
                return Result<bool[]>.Fail(result.Message);
            bits[i] = result.Data;
        }
        return Result<bool[]>.Success(bits);
    }

    public async Task<Result<bool>> ReadOutbitAsync(ushort bitNo)
    {
        short level = LTDMC.dmc_read_outbit(_cardNo, bitNo);
        if (level < 0)
            return Result<bool>.Fail($"读取输出口 {bitNo} 失败");
        return Result<bool>.Success(level == 1);
    }

    public async Task<Result> WriteOutbitAsync(ushort bitNo, bool on)
    {
        ushort level = on ? (ushort)1 : (ushort)0;
        var ret = LTDMC.dmc_write_outbit(_cardNo, bitNo, level);
        if (ret != 0)
            return Result.Fail($"写入输出口 {bitNo} 失败, error code: {ret}");
        return Result.Success();
    }

    // ========== PDO 读写 ==========

    public async Task<Result> WriteRxPDOAsync(ushort slaveAddr, ushort index, ushort subIndex, ushort bitLength, int value)
    {
        if (!IsConnected) return Result.Fail("板卡未连接");
        var data = BitConverter.GetBytes(value);
        var ret = LTDMC.nmc_write_rxpdo(_cardNo, EthercatPort, slaveAddr, index, subIndex, bitLength, data);
        if (ret != 0)
            return Result.Fail($"写RxPDO从站{slaveAddr} 0x{index:X4}:{subIndex} 失败, error code: {ret}");
        return Result.Success();
    }

    public async Task<Result<int>> ReadTxPDOAsync(ushort slaveAddr, ushort index, ushort subIndex, ushort bitLength)
    {
        if (!IsConnected) return Result<int>.Fail("板卡未连接");
        var data = new byte[bitLength / 8];
        var ret = LTDMC.nmc_read_txpdo(_cardNo, EthercatPort, slaveAddr, index, subIndex, bitLength, data);
        if (ret != 0)
            return Result<int>.Fail($"读TxPDO从站{slaveAddr} 0x{index:X4}:{subIndex} 失败, error code: {ret}");
        return Result<int>.Success(BytesToInt(data));
    }

    // ========== 底层轴操作（薄封装，供 BusAxisDevice 调用） ==========

    public async Task<Result<double>> GetPositionAsync(ushort axis)
    {
        double pos = 0;
        var ret = LTDMC.dmc_get_position_unit(_cardNo, axis, ref pos);
        if (ret != 0)
            return Result<double>.Fail($"读取位置失败, error code: {ret}");
        return Result<double>.Success(pos);
    }

    public async Task<Result<double>> GetSpeedAsync(ushort axis)
    {
        double speed = 0;
        var ret = LTDMC.dmc_read_current_speed_unit(_cardNo, axis, ref speed);
        if (ret != 0)
            return Result<double>.Fail($"读取速度失败, error code: {ret}");
        return Result<double>.Success(speed);
    }

    public async Task<Result> SetEquivAsync(ushort axis, double equiv)
    {
        var ret = LTDMC.dmc_set_equiv(_cardNo, axis, equiv);
        if (ret != 0)
            return Result.Fail($"设置脉冲当量失败, error code: {ret}");
        return Result.Success();
    }

    public async Task<Result> SetProfileUnitAsync(ushort axis, double minVel, double maxVel, double tacc, double tdec, double stopVel)
    {
        var ret = LTDMC.dmc_set_profile_unit(_cardNo, axis, minVel, maxVel, tacc, tdec, stopVel);
        if (ret != 0)
            return Result.Fail($"设置速度曲线失败, error code: {ret}");
        return Result.Success();
    }

    public async Task<Result> SetSProfileAsync(ushort axis, ushort mode, double sPara)
    {
        var ret = LTDMC.dmc_set_s_profile(_cardNo, axis, mode, sPara);
        if (ret != 0)
            return Result.Fail($"设置S段曲线失败, error code: {ret}");
        return Result.Success();
    }

    public async Task<Result> PmoveUnitAsync(ushort axis, double distance, ushort posiMode)
    {
        var ret = LTDMC.dmc_pmove_unit(_cardNo, axis, distance, posiMode);
        if (ret != 0)
            return Result.Fail($"启动定长运动失败, error code: {ret}");
        return Result.Success();
    }

    public async Task<Result<int>> CheckDoneAsync(ushort axis)
    {
        return Result<int>.Success(LTDMC.dmc_check_done(_cardNo, axis));
    }

    public async Task<Result<uint>> GetAxisIoStatusAsync(ushort axis)
    {
        return Result<uint>.Success(LTDMC.dmc_axis_io_status(_cardNo, axis));
    }

    public async Task<Result<ushort>> GetAxisStateMachineAsync(ushort axis)
    {
        ushort stateMachine = 0;
        var ret = LTDMC.nmc_get_axis_state_machine(_cardNo, axis, ref stateMachine);
        if (ret != 0)
            return Result<ushort>.Fail($"获取轴状态机失败, error code: {ret}");
        return Result<ushort>.Success(stateMachine);
    }

    public async Task<Result> SetHomeProfileAsync(ushort axis, ushort homeMode, double lowVel, double highVel, double tacc, double tdec, double offsetPos)
    {
        var ret = LTDMC.nmc_set_home_profile(_cardNo, axis, homeMode, lowVel, highVel, tacc, tdec, offsetPos);
        if (ret != 0)
            return Result.Fail($"设置回零参数失败, error code: {ret}");
        return Result.Success();
    }

    public async Task<Result> HomeMoveAsync(ushort axis)
    {
        var ret = LTDMC.nmc_home_move(_cardNo, axis);
        if (ret != 0)
            return Result.Fail($"启动回零失败, error code: {ret}");
        return Result.Success();
    }

    public async Task<Result<ushort>> GetHomeResultAsync(ushort axis)
    {
        ushort homeResult = 0;
        var ret = LTDMC.dmc_get_home_result(_cardNo, axis, ref homeResult);
        if (ret != 0)
            return Result<ushort>.Fail($"读取回零结果失败, error code: {ret}");
        return Result<ushort>.Success(homeResult);
    }

    public async Task<Result<int>> GetStopReasonAsync(ushort axis)
    {
        int stopReason = 0;
        LTDMC.dmc_get_stop_reason(_cardNo, axis, ref stopReason);
        return Result<int>.Success(stopReason);
    }

    public async Task<Result> EnableAxisAsync(ushort axis, int timeoutMs = 3000)
    {
        if (!IsConnected)
            return Result.Fail("板卡未连接");

        var ret = LTDMC.nmc_set_axis_enable(_cardNo, axis);
        if (ret != 0)
            return Result.Fail($"使能轴 {axis} 失败, error code: {ret}");

        ushort stateMachine = 0;
        int elapsed = 0;
        int interval = 20;
        while (elapsed < timeoutMs)
        {
            LTDMC.nmc_get_axis_state_machine(_cardNo, axis, ref stateMachine);
            if (stateMachine == 4)
            {
                logger.Information("轴 {Axis} 使能成功", axis);
                return Result.Success();
            }
            await Task.Delay(interval);
            elapsed += interval;
        }

        return Result.Fail($"轴 {axis} 使能超时 ({timeoutMs}ms)，当前状态机: {stateMachine}");
    }

    public Result DisableAxis(ushort axis)
    {
        if (!IsConnected)
            return Result.Fail("板卡未连接");

        var ret = LTDMC.nmc_set_axis_disable(_cardNo, axis);
        if (ret != 0)
            return Result.Fail($"失能轴 {axis} 失败, error code: {ret}");

        logger.Information("轴 {Axis} 已失能", axis);
        return Result.Success();
    }

    public async Task<Result> EmergencyStopAllAsync()
    {
        var ret = LTDMC.dmc_emg_stop(_cardNo);
        if (ret != 0)
            return Result.Fail($"紧急停止失败, error code: {ret}");
        return Result.Success();
    }

    public async Task<Result> SetSoftLimitAsync(ushort axis, double negativeLimit, double positiveLimit, bool enable = true)
    {
        var ret = LTDMC.dmc_set_softlimit_unit(
            _cardNo, axis,
            enable ? (ushort)1 : (ushort)0,
            0, 1,
            negativeLimit, positiveLimit);

        if (ret != 0)
            return Result.Fail($"设置软限位失败, error code: {ret}");
        return Result.Success();
    }

    public async Task<Result> StopAxisAsync(ushort axis, bool emergency = false)
    {
        ushort stopMode = emergency ? (ushort)1 : (ushort)0;
        var ret = LTDMC.dmc_stop(_cardNo, axis, stopMode);
        if (ret != 0)
            return Result.Fail($"停止轴 {axis} 失败, error code: {ret}");

        while (LTDMC.dmc_check_done(_cardNo, axis) == 0)
            await Task.Delay(10);

        return Result.Success();
    }

    // ========== 插补 ==========

    public async Task<Result> SetVectorProfileUnitAsync(ushort crd, double minVel, double maxVel, double tacc, double tdec, double stopVel)
    {
        var ret = LTDMC.dmc_set_vector_profile_unit(_cardNo, crd, minVel, maxVel, tacc, tdec, stopVel);
        if (ret != 0)
            return Result.Fail($"设置插补速度曲线失败, error code: {ret}");
        return Result.Success();
    }

    public async Task<Result> SetVectorSProfileAsync(ushort crd, ushort mode, double sPara)
    {
        var ret = LTDMC.dmc_set_vector_s_profile(_cardNo, crd, mode, sPara);
        if (ret != 0)
            return Result.Fail($"设置插补S段曲线失败, error code: {ret}");
        return Result.Success();
    }

    public async Task<Result> LineUnitAsync(ushort crd, ushort axisCount, ushort[] axisList, double[] targetPositions, ushort posiMode)
    {
        var ret = LTDMC.dmc_line_unit(_cardNo, crd, axisCount, axisList, targetPositions, posiMode);
        if (ret != 0)
            return Result.Fail($"启动直线插补失败, error code: {ret}");
        return Result.Success();
    }

    public async Task<Result<int>> CheckDoneMultiCoorAsync(ushort crd)
    {
        return Result<int>.Success(LTDMC.dmc_check_done_multicoor(_cardNo, crd));
    }

    public async Task<Result> StopMultiCoorAsync(ushort crd, ushort mode)
    {
        var ret = LTDMC.dmc_stop_multicoor(_cardNo, crd, mode);
        if (ret != 0)
            return Result.Fail($"停止坐标系 {crd} 失败, error code: {ret}");
        return Result.Success();
    }

    // ========== 内部 ==========

    private Result DownloadFile(ushort cardNo, string path, ushort fileType)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return Result.Fail("文件路径为空");
            if (!File.Exists(path))
                return Result.Fail($"文件不存在: {path}");

            byte[] buffer = ReadTextFileToUtf8Bytes(path);
            byte[] dummy = Encoding.UTF8.GetBytes("");
            var ret = LTDMC.dmc_download_memfile(cardNo, buffer, (uint)buffer.Length, dummy, fileType);
            if (ret != 0)
                return Result.Fail($"下载文件失败, error code: {ret}");
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Fail($"下载文件异常: {ex.Message}");
        }
    }

    private byte[] ReadTextFileToUtf8Bytes(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentNullException(nameof(filePath), "文件路径不能为空");
        if (!File.Exists(filePath))
            throw new FileNotFoundException("目标文件不存在", filePath);

        using FileStream fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using StreamReader sr = new StreamReader(fs, Encoding.UTF8);
        string content = sr.ReadToEnd();
        return Encoding.UTF8.GetBytes(content);
    }

    static int BytesToInt(byte[] bytes, bool useLowBytes = true)
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));
        byte[] buffer = new byte[4];
        if (bytes.Length >= 4)
        {
            int startIndex = useLowBytes ? 0 : bytes.Length - 4;
            Array.Copy(bytes, startIndex, buffer, 0, 4);
        }
        else
        {
            Array.Copy(bytes, 0, buffer, 0, bytes.Length);
        }
        return BitConverter.ToInt32(buffer, 0);
    }

    public void Dispose()
    {
        if (IsConnected)
        {
            LTDMC.dmc_board_close();
            IsConnected = false;
        }
    }
}
