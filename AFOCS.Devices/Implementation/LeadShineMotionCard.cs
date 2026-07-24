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

    public async Task<Result> InitializeAsync(CancellationToken token = default)
    {
        try
        {
            // 1. 加载配置
            _config = await configService.LoadAsync<LeadShineMotionCardConfig>() 
                      ?? new LeadShineMotionCardConfig();

            // 2. 板卡初始化
            short cardNum = LTDMC.dmc_board_init();
            if (cardNum == 0)
                return Result.Fail($"没有找到控制卡，或者控制卡异常, card num:{cardNum}");
            if (cardNum < 0 || cardNum > 8)
                return Result.Fail($"控制卡数量异常，card num:{cardNum}");

            // 3. 获取卡号
            ushort usNum = 0;
            ushort[] cardList = new ushort[8];
            uint[] cardTypes = new uint[8];
            var ret = LTDMC.dmc_get_CardInfList(ref usNum, cardTypes, cardList);
            if (ret != 0)
                return Result.Fail($"调用API失败:{nameof(LTDMC.dmc_get_CardInfList)}, error code: {ret}");
            _cardNo = cardList[0];
            logger.Information($"当前板卡号为: {_cardNo}");

            // 4. 设置从站输出保持（建议在ENI下载前设置）
            ret = LTDMC.nmc_set_slave_output_retain(_cardNo, 1);
            if (ret != 0)
                return Result.Fail($"调用API失败:{nameof(LTDMC.nmc_set_slave_output_retain)}, error code: {ret}");

            // 5. 检查总线状态
            ushort nmcErr = 0;
            LTDMC.nmc_get_errcode(_cardNo, EthercatPort, ref nmcErr);

            // 6. 如果ENI缺失或不匹配，下载ENI并热复位
            if (nmcErr == 0x000C || nmcErr == 0x001E)
            {
                // 6.1 设置总线周期（必须在下载ENI之前）
                ret = LTDMC.nmc_set_cycletime(_cardNo, EthercatPort, 1000);
                if (ret != 0)
                    return Result.Fail($"调用API失败:{nameof(LTDMC.nmc_set_cycletime)}, error code: {ret}");

                // 6.2 下载ENI文件
                logger.Information("下载ENI总线配置文件");
                var eniResult = DownloadFile(_cardNo, _config.EniPath,EniFileType);
                if (!eniResult.IsSuccess)
                    return eniResult;

                // 6.3 热复位（让ENI生效）
                logger.Information("执行热复位...");
                var resetResult = await HotResetAsync();
                if (!resetResult.IsSuccess)
                    return resetResult;

                // 6.4 轮询等待总线恢复
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

            // 7. 下载轴参数配置文件（INI）
            if (!string.IsNullOrWhiteSpace(_config.IniPath) && File.Exists(_config.IniPath))
            {
                logger.Information("下载轴参数配置文件");
                var iniResult = DownloadFile(_cardNo, _config.IniPath,ConfigFileType);
                if (!iniResult.IsSuccess)
                    return iniResult;
            }

            // 8. 最终确认总线状态
            LTDMC.nmc_get_errcode(_cardNo, EthercatPort, ref nmcErr);
            if (nmcErr != 0)
                return Result.Fail($"总线错误: 0x{nmcErr:X4}");

            LTDMC.nmc_set_axis_enable(_cardNo, 255);// 使能所有轴


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

    /// <summary>
    /// 热复位（软复位） - 只重启总线协议栈，不关闭板卡驱动
    /// </summary>
    public async Task<Result> HotResetAsync()
    {
        var ret = LTDMC.dmc_soft_reset(_cardNo);
        if (ret != 0)
            return Result.Fail($"调用API失败:{nameof(LTDMC.dmc_soft_reset)}, error code: {ret}");

        return Result.Success();
    }

    /// <summary>
    /// 冷复位（硬件复位） - 彻底重启板卡，需要关闭后等待15秒
    /// </summary>
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

        // 重新获取卡号
        ushort usNum = 0;
        ushort[] cardList = new ushort[8];
        uint[] cardTypes = new uint[8];
        ret = LTDMC.dmc_get_CardInfList(ref usNum, cardTypes, cardList);
        if (ret != 0)
            return Result.Fail($"调用API失败:{nameof(LTDMC.dmc_get_CardInfList)}, error code: {ret}");
        _cardNo = cardList[0];

        return Result.Success();
    }

    /// <summary>
    /// 获取总线错误码和对应的中文描述
    /// </summary>
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

    public LeadShineMotionCardConfig GetConfig() => _config.Clone();

    public async Task SaveConfigAsync(LeadShineMotionCardConfig config)
    {
        _config = config.Clone();
        await configService.SaveAsync(_config);
    }

    private Result DownloadFile(ushort cardNo, string path,ushort fileType)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path))
                return Result.Fail("ENI文件路径为空");
            if (!File.Exists(path))
                return Result.Fail($"ENI文件不存在: {path}");

            byte[] buffer = ReadTextFileToUtf8Bytes(path);
            byte[] dummy = Encoding.UTF8.GetBytes("");
            var ret = LTDMC.dmc_download_memfile(cardNo, buffer, (uint)buffer.Length, dummy, fileType);
            if (ret != 0)
                return Result.Fail($"下载ENI文件失败, error code: {ret}");
            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Fail($"下载ENI文件异常: {ex.Message}");
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

    /// <summary>
    /// 定长运动（点位运动）
    /// </summary>
    /// <param name="axis">轴号（0~最大轴数-1）</param>
    /// <param name="distance">运动距离（单位：由脉冲当量决定，如设当量=1000，则此处单位=mm）</param>
    /// <param name="posiMode">0=相对模式（在当前坐标上偏移），1=绝对模式（走到绝对坐标）</param>
    /// <param name="equiv">脉冲当量（pulse/unit），如 1000 表示 1000 个脉冲走 1 个单位</param>
    /// <param name="minVel">起始速度（unit/s）</param>
    /// <param name="maxVel">最大速度（unit/s）</param>
    /// <param name="tacc">加速时间（秒）</param>
    /// <param name="tdec">减速时间（秒）</param>
    /// <param name="stopVel">停止速度（unit/s）</param>
    /// <param name="sPara">S段时间（秒，设为0表示梯形曲线）</param>
    /// <param name="timeoutMs">运动超时时间（毫秒，0表示无限等待）</param>
    /// <returns>Result 包含执行结果</returns>
    public async Task<Result> MovePmoveAsync(
        ushort axis,
        double distance,
        ushort posiMode = 0,
        double equiv = 1000.0,      // 默认 1000 pulse/mm
        double minVel = 10,
        double maxVel = 3000,
        double tacc = 0.1,
        double tdec = 0.1,
        double stopVel = 10,
        double sPara = 0,
        int timeoutMs = 0)
    {
        try
        {
            // ========== 第1步：检查连接状态 ==========
            if (!IsConnected)
                return Result.Fail("板卡未连接，请先初始化");

            // ========== 第2步：检查轴是否已使能（状态机必须为4） ==========
            // ========== 检查并自动使能 ==========
            ushort stateMachine = 0;
            var ret = LTDMC.nmc_get_axis_state_machine(_cardNo, axis, ref stateMachine);
            if (ret != 0)
                return Result.Fail($"获取轴状态机失败, error code: {ret}");

            if (stateMachine != 4)
            {
                logger.Warning($"轴 {axis} 未使能（状态机={stateMachine}），尝试自动使能...");

                // 调用使能函数
                var enableResult = await EnableAxisAsync(axis);
                if (!enableResult.IsSuccess)
                    return Result.Fail($"自动使能失败: {enableResult.Message}");
            }

            // ========== 第3步：检查是否已有运动 ==========
            if (LTDMC.dmc_check_done(_cardNo, axis) == 0)
                return Result.Fail($"轴 {axis} 正在运动中，请等待完成");

            // ========== 第4步：检查硬限位状态（防止撞机） ==========
            uint axisStatus = LTDMC.dmc_axis_io_status(_cardNo, axis);
            bool isPositiveLimit = (axisStatus & 0x02) != 0; // bit1 = 正限位
            bool isNegativeLimit = (axisStatus & 0x04) != 0; // bit2 = 负限位
            bool isEMG = (axisStatus & 0x08) != 0;           // bit3 = 急停

            if (isEMG)
                return Result.Fail("急停已触发，请复位急停按钮后再试");
            if (isPositiveLimit)
                return Result.Fail($"轴 {axis} 正方向硬限位已触发，无法运动");
            if (isNegativeLimit)
                return Result.Fail($"轴 {axis} 负方向硬限位已触发，无法运动");

            // ========== 第5步：设置脉冲当量（最关键！） ==========
            // 不设当量，distance 会被当成脉冲数，而非物理单位
            ret = LTDMC.dmc_set_equiv(_cardNo, axis, equiv);
            if (ret != 0)
                return Result.Fail($"设置脉冲当量失败, error code: {ret}");
            logger.Information($"轴 {axis} 脉冲当量已设为: {equiv} pulse/unit");

            // ========== 第6步：设置速度曲线 ==========
            ret = LTDMC.dmc_set_profile_unit(_cardNo, axis, minVel, maxVel, tacc, tdec, stopVel);
            if (ret != 0)
                return Result.Fail($"设置速度曲线失败, error code: {ret}");
            logger.Information($"轴 {axis} 速度曲线: 起始={minVel}, 最大={maxVel}, 加减速={tacc}/{tdec}");

            // ========== 第7步：设置S段曲线（可选） ==========
            if (sPara > 0)
            {
                ret = LTDMC.dmc_set_s_profile(_cardNo, axis, 0, sPara);
                if (ret != 0)
                    logger.Warning($"轴 {axis} 设置S段失败, error code: {ret}，将使用梯形曲线");
            }

            // ========== 第8步：读取当前位置（用于日志记录） ==========
            double currentPos = 0;
            LTDMC.dmc_get_position_unit(_cardNo, axis, ref currentPos);
            string modeStr = posiMode == 0 ? "相对" : "绝对";
            logger.Information($"轴 {axis} 定长运动启动，模式={modeStr}，目标={distance}，当前坐标={currentPos}");

            // ========== 第9步：启动定长运动 ==========
            ret = LTDMC.dmc_pmove_unit(_cardNo, axis, distance, posiMode);
            if (ret != 0)
                return Result.Fail($"启动定长运动失败, error code: {ret}");

            // ========== 第10步：等待运动完成（带超时） ==========
            int elapsed = 0;
            int interval = 20; // 轮询间隔（毫秒）
            while (LTDMC.dmc_check_done(_cardNo, axis) == 0)
            {
                if (timeoutMs > 0 && elapsed >= timeoutMs)
                {
                    // 超时，尝试减速停止
                    LTDMC.dmc_stop(_cardNo, axis, 0);
                    return Result.Fail($"轴 {axis} 运动超时 ({timeoutMs}ms)，已强制停止");
                }
                await Task.Delay(interval);
                elapsed += interval;
            }

            // ========== 第11步：读取最终位置（用于确认） ==========
            double finalPos = 0;
            LTDMC.dmc_get_position_unit(_cardNo, axis, ref finalPos);
            logger.Information($"轴 {axis} 定长运动完成，当前坐标={finalPos}");

            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"轴 {axis} 定长运动异常");
            return Result.Fail($"运动异常: {ex.Message}");
        }
    }


    public async Task<Result> EnableAxisAsync(ushort axis, int timeoutMs = 3000)
    {
        if (!IsConnected)
            return Result.Fail("板卡未连接");

        // 1. 发送使能指令
        var ret = LTDMC.nmc_set_axis_enable(_cardNo, axis);
        if (ret != 0)
            return Result.Fail($"使能轴 {axis} 失败, error code: {ret}");

        // 2. 等待状态机变为4（OP_ENABLE）
        ushort stateMachine = 0;
        int elapsed = 0;
        int interval = 20;
        while (elapsed < timeoutMs)
        {
            LTDMC.nmc_get_axis_state_machine(_cardNo, axis, ref stateMachine);
            if (stateMachine == 4)
            {
                logger.Information($"轴 {axis} 使能成功");
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

        logger.Information($"轴 {axis} 已失能");
        return Result.Success();
    }
    /// <summary>
    /// 直线插补运动（多轴同时到达，走直线轨迹）
    /// </summary>
    /// <param name="crd">坐标系号（0~7）</param>
    /// <param name="axisList">参与插补的轴号列表（至少2个轴）</param>
    /// <param name="targetPositions">各轴的目标位置（单位：由脉冲当量决定）</param>
    /// <param name="posiMode">0=相对模式，1=绝对模式</param>
    /// <param name="equivList">各轴的脉冲当量（pulse/unit），长度必须与axisList一致</param>
    /// <param name="minVel">起始矢量速度（unit/s）</param>
    /// <param name="maxVel">最大矢量速度（unit/s）</param>
    /// <param name="tacc">加速时间（秒）</param>
    /// <param name="tdec">减速时间（秒）</param>
    /// <param name="stopVel">停止矢量速度（unit/s）</param>
    /// <param name="sPara">S段时间（秒，0表示梯形）</param>
    /// <param name="timeoutMs">运动超时时间（毫秒，0表示无限等待）</param>
    /// <returns>Result</returns>
    public async Task<Result> MoveLineAsync(
        ushort[] axisList,
        double[] targetPositions,
        ushort posiMode = 0,
        double[]? equivList = null,
        double minVel = 10,
        double maxVel = 3000,
        double tacc = 0.1,
        double tdec = 0.1,
        double stopVel = 10,
        double sPara = 0,
        int timeoutMs = 0)
    {
        // ========== 第1步：参数校验 ==========
        if (!IsConnected)
            return Result.Fail("板卡未连接，请先初始化");
        ushort crd = 0;// 坐标系
        try
        {
      
            if (axisList == null || axisList.Length < 2)
                return Result.Fail("插补至少需要2个轴");

            if (targetPositions == null || targetPositions.Length != axisList.Length)
                return Result.Fail("目标位置数组长度必须与轴列表长度一致");

            int axisCount = axisList.Length;

            // ========== 第2步：检查所有轴是否已使能 ==========
            for (int i = 0; i < axisCount; i++)
            {
                ushort stateMachine = 0;
                var ret = LTDMC.nmc_get_axis_state_machine(_cardNo, axisList[i], ref stateMachine);
                if (ret != 0)
                    return Result.Fail($"获取轴 {axisList[i]} 状态机失败, error code: {ret}");

                if (stateMachine != 4)
                {
                    logger.Warning($"轴 {axisList[i]} 未使能（状态机={stateMachine}），尝试自动使能...");
                    var enableResult = await EnableAxisAsync(axisList[i]);
                    if (!enableResult.IsSuccess)
                        return Result.Fail($"轴 {axisList[i]} 自动使能失败: {enableResult.Message}");
                }
            }

            // ========== 第3步：检查坐标系是否空闲 ==========
            if (LTDMC.dmc_check_done_multicoor(_cardNo, crd) == 0)
                return Result.Fail($"坐标系 {crd} 正在运动中，请等待完成");

            // ========== 第4步：设置各轴脉冲当量 ==========
            for (int i = 0; i < axisCount; i++)
            {
                double equiv = (equivList != null && i < equivList.Length) ? equivList[i] : 1000.0;
                var ret = LTDMC.dmc_set_equiv(_cardNo, axisList[i], equiv);
                if (ret != 0)
                    return Result.Fail($"设置轴 {axisList[i]} 脉冲当量失败, error code: {ret}");
            }
            logger.Information($"插补轴脉冲当量设置完成");

            // ========== 第5步：检查各轴硬限位 ==========
            for (int i = 0; i < axisCount; i++)
            {
                uint axisStatus = LTDMC.dmc_axis_io_status(_cardNo, axisList[i]);
                if ((axisStatus & 0x08) != 0) // EMG
                    return Result.Fail($"轴 {axisList[i]} 急停已触发");
                if ((axisStatus & 0x02) != 0) // 正限位
                    return Result.Fail($"轴 {axisList[i]} 正方向硬限位已触发");
                if ((axisStatus & 0x04) != 0) // 负限位
                    return Result.Fail($"轴 {axisList[i]} 负方向硬限位已触发");
            }

            // ========== 第6步：设置插补矢量速度曲线 ==========
            var ret2 = LTDMC.dmc_set_vector_profile_unit(_cardNo, crd, minVel, maxVel, tacc, tdec, stopVel);
            if (ret2 != 0)
                return Result.Fail($"设置插补速度曲线失败, error code: {ret2}");
            logger.Information($"坐标系 {crd} 矢量速度: 起始={minVel}, 最大={maxVel}");

            // ========== 第7步：设置S段曲线（可选） ==========
            if (sPara > 0)
            {
                ret2 = LTDMC.dmc_set_vector_s_profile(_cardNo, crd, 0, sPara);
                if (ret2 != 0)
                    logger.Warning($"坐标系 {crd} 设置S段失败, error code: {ret2}");
            }

            // ========== 第8步：记录当前位置 ==========
            string posStr = string.Join(", ", targetPositions);
            string modeStr = posiMode == 0 ? "相对" : "绝对";
            logger.Information($"坐标系 {crd} 直线插补启动，模式={modeStr}，目标=[{posStr}]");

            // ========== 第9步：启动直线插补 ==========
            var ret3 = LTDMC.dmc_line_unit(_cardNo, crd, (ushort)axisCount, axisList, targetPositions, posiMode);
            if (ret3 != 0)
                return Result.Fail($"启动直线插补失败, error code: {ret3}");

            // ========== 第10步：等待插补完成（带超时） ==========
            int elapsed = 0;
            int interval = 20;
            while (LTDMC.dmc_check_done_multicoor(_cardNo, crd) == 0)
            {
                if (timeoutMs > 0 && elapsed >= timeoutMs)
                {
                    LTDMC.dmc_stop_multicoor(_cardNo, crd, 0);
                    return Result.Fail($"坐标系 {crd} 插补超时 ({timeoutMs}ms)，已强制停止");
                }
                await Task.Delay(interval);
                elapsed += interval;
            }

            logger.Information($"坐标系 {crd} 直线插补完成");
            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"坐标系 {crd} 直线插补异常");
            return Result.Fail($"插补异常: {ex.Message}");
        }
    }

    /// <summary>
    /// 总线轴回零运动
    /// </summary>
    /// <param name="axis">轴号</param>
    /// <param name="homeMode">回零模式（参考驱动器手册，常用：33=找EZ，17=找负限位，18=找正限位）</param>
    /// <param name="lowVel">回零低速（unit/s），用于精找原点</param>
    /// <param name="highVel">回零高速（unit/s），用于快速接近原点</param>
    /// <param name="tacc">加速时间（秒）</param>
    /// <param name="tdec">减速时间（秒）</param>
    /// <param name="offsetPos">回零偏移量（unit）：回零完成后偏移的距离，通常设为0</param>
    /// <param name="equiv">脉冲当量（pulse/unit）</param>
    /// <param name="timeoutMs">回零超时时间（毫秒，0表示无限等待）</param>
    /// <returns>Result</returns>
    public async Task<Result> MoveHomeAsync(
        ushort axis,
        ushort homeMode = 33,           // 默认找EZ信号（高精度）
        double lowVel = 100,
        double highVel = 1000,
        double tacc = 0.1,
        double tdec = 0.1,
        double offsetPos = 0,
        double equiv = 1000.0,
        int timeoutMs = 30000)          // 回零默认30秒超时
    {
        try
        {
            // ========== 第1步：检查连接状态 ==========
            if (!IsConnected)
                return Result.Fail("板卡未连接，请先初始化");

            // ========== 第2步：检查轴是否已使能 ==========
            ushort stateMachine = 0;
            var ret = LTDMC.nmc_get_axis_state_machine(_cardNo, axis, ref stateMachine);
            if (ret != 0)
                return Result.Fail($"获取轴 {axis} 状态机失败, error code: {ret}");

            if (stateMachine != 4)
            {
                logger.Warning($"轴 {axis} 未使能（状态机={stateMachine}），尝试自动使能...");
                var enableResult = await EnableAxisAsync(axis);
                if (!enableResult.IsSuccess)
                    return Result.Fail($"轴 {axis} 自动使能失败: {enableResult.Message}");
            }

            // ========== 第3步：检查是否已有运动 ==========
            if (LTDMC.dmc_check_done(_cardNo, axis) == 0)
                return Result.Fail($"轴 {axis} 正在运动中，无法回零");

            // ========== 第4步：检查急停和限位状态 ==========
            uint axisStatus = LTDMC.dmc_axis_io_status(_cardNo, axis);
            bool isEMG = (axisStatus & 0x08) != 0;
            bool isPositiveLimit = (axisStatus & 0x02) != 0;
            bool isNegativeLimit = (axisStatus & 0x04) != 0;

            if (isEMG)
                return Result.Fail("急停已触发，请复位急停按钮后再试");

            // 回零前检查限位：如果正负限位同时有效，说明传感器异常
            if (isPositiveLimit && isNegativeLimit)
                return Result.Fail("正负限位同时触发，请检查限位传感器");

            logger.Information($"轴 {axis} 回零前状态: 正限位={isPositiveLimit}, 负限位={isNegativeLimit}");

            // ========== 第5步：设置脉冲当量 ==========
            ret = LTDMC.dmc_set_equiv(_cardNo, axis, equiv);
            if (ret != 0)
                return Result.Fail($"设置脉冲当量失败, error code: {ret}");

            // ========== 第6步：设置回零参数 ==========
            // 注意：回零参数直接写入驱动器，由驱动器执行回零动作
            ret = LTDMC.nmc_set_home_profile(_cardNo, axis, homeMode, lowVel, highVel, tacc, tdec, offsetPos);
            if (ret != 0)
                return Result.Fail($"设置回零参数失败, error code: {ret}");

            logger.Information($"轴 {axis} 回零参数: 模式={homeMode}, 低速={lowVel}, 高速={highVel}, 偏移={offsetPos}");

            // ========== 第7步：启动回零 ==========
            ret = LTDMC.nmc_home_move(_cardNo, axis);
            if (ret != 0)
                return Result.Fail($"启动回零失败, error code: {ret}");

            logger.Information($"轴 {axis} 回零已启动，等待完成...");

            // ========== 第8步：等待回零完成（带超时） ==========
            int elapsed = 0;
            int interval = 50; // 回零过程中轮询间隔可以稍大（50ms）
            bool isCompleted = false;

            while (!isCompleted)
            {
                if (timeoutMs > 0 && elapsed >= timeoutMs)
                {
                    // 超时，尝试停止
                    LTDMC.dmc_stop(_cardNo, axis, 0);
                    return Result.Fail($"轴 {axis} 回零超时 ({timeoutMs}ms)，已强制停止");
                }

                // 检查轴是否停止（dmc_check_done 返回1表示停止）
                if (LTDMC.dmc_check_done(_cardNo, axis) == 1)
                {
                    isCompleted = true;
                    break;
                }

                // 回零过程中定期检查急停（如果用户拍了急停，立即退出）
                if (elapsed % 500 == 0) // 每500ms检查一次
                {
                    uint currentStatus = LTDMC.dmc_axis_io_status(_cardNo, axis);
                    if ((currentStatus & 0x08) != 0)
                    {
                        LTDMC.dmc_stop(_cardNo, axis, 1);
                        return Result.Fail("回零过程中急停被触发");
                    }
                }

                await Task.Delay(interval);
                elapsed += interval;
            }

            // ========== 第9步：读取回零结果 ==========
            ushort homeResult = 0;
            ret = LTDMC.dmc_get_home_result(_cardNo, axis, ref homeResult);

            if (ret != 0)
                return Result.Fail($"读取回零结果失败, error code: {ret}");

            if (homeResult == 1)
            {
                // 回零成功，读取当前位置确认
                double currentPos = 0;
                LTDMC.dmc_get_position_unit(_cardNo, axis, ref currentPos);
                logger.Information($"轴 {axis} 回零成功！当前位置: {currentPos} unit");
                return Result.Success();
            }
            else
            {
                // 回零失败，读取停止原因帮助排查
                int stopReason = 0;
                LTDMC.dmc_get_stop_reason(_cardNo, axis, ref stopReason);
                logger.Warning($"轴 {axis} 回零失败，停止原因码: {stopReason}");
                return Result.Fail($"轴 {axis} 回零失败，停止原因: {GetStopReasonDescription(stopReason)}");
            }
        }
        catch (Exception ex)
        {
            logger.Error(ex, $"轴 {axis} 回零异常");
            return Result.Fail($"回零异常: {ex.Message}");
        }
    }


    /// <summary>
    /// 获取停止原因的描述（方便排查回零失败原因）
    /// </summary>
    private string GetStopReasonDescription(long reason)
    {
        return reason switch
        {
            0 => "正常停止",
            1 => "ALM 立即停止",
            2 => "ALM 减速停止",
            3 => "LTC 外部触发立即停止",
            4 => "EMG 立即停止",
            5 => "正硬限位立即停止",
            6 => "负硬限位立即停止",
            7 => "正硬限位减速停止",
            8 => "负硬限位减速停止",
            9 => "正软限位立即停止",
            10 => "负软限位立即停止",
            11 => "正软限位减速停止",
            12 => "负软限位减速停止",
            13 => "命令立即停止",
            14 => "命令减速停止",
            19 => "DSTP 信号引起的减速停止",
            21 => "原点不在两个限位之间",
            22 => "回零方向与限位方向冲突",
            23 => "正负限位同时有效",
            24 => "没有找到EZ信号",
            25 => "回零位置溢出",
            201 => "正负限位之间全程没找到原点信号",
            202 => "回零方向不匹配",
            203 => "正负限位同时有效",
            204 => "正负限位之间全程没有EZ信号",
            205 => "位置溢出",
            206 => "双原点错误",
            207 => "外部信号触发回零停止",
            208 => "驱动器回零被中断停止",
            _ => $"未知原因 (code: {reason})"
        };
    }
    //模式 描述  适用场景
    //1	找负限位，反找第一个Z相 有负限位+Z相
    //2	找正限位，反找第一个Z相 有正限位+Z相
    //17	找负限位（不回找Z相）	没有Z相或不需要高精度
    //18	找正限位（不回找Z相）	没有Z相或不需要高精度
    //33	正向找Z相 最常用，直接用Z相做原点
    //34	负向找Z相 同上
    //35  当前位置设为原点 手动对位后使用

    /// <summary>
    /// 停止指定轴的运动
    /// </summary>
    /// <param name="axis">轴号</param>
    /// <param name="emergency">true=急停（立即停），false=减速停</param>
    public async Task<Result> StopAxisAsync(ushort axis, bool emergency = false)
    {
        ushort stopMode = emergency ? (ushort)1 : (ushort)0;
        var ret = LTDMC.dmc_stop(_cardNo, axis, stopMode);
        if (ret != 0)
            return Result.Fail($"停止轴 {axis} 失败, error code: {ret}");

        // 等待轴完全停止
        while (LTDMC.dmc_check_done(_cardNo, axis) == 0)
            await Task.Delay(10);

        return Result.Success();
    }

    /// <summary>
    /// 紧急停止所有轴（硬急停）
    /// </summary>
    public async Task<Result> EmergencyStopAllAsync()
    {
        var ret = LTDMC.dmc_emg_stop(_cardNo);
        if (ret != 0)
            return Result.Fail($"紧急停止失败, error code: {ret}");
        return Result.Success();
    }

    /// <summary>
    /// 读取指定轴的当前位置（指令位置）
    /// </summary>
    public async Task<Result<double>> GetPositionAsync(ushort axis)
    {
        double pos = 0;
        var ret = LTDMC.dmc_get_position_unit(_cardNo, axis, ref pos);
        if (ret != 0)
            return Result<double>.Fail($"读取位置失败, error code: {ret}");
        return Result<double>.Success(pos);
    }
    /// <summary>
    /// 读取指定轴的当前速度
    /// </summary>
    public async Task<Result<double>> GetSpeedAsync(ushort axis)
    {
        double speed = 0;
        var ret = LTDMC.dmc_read_current_speed_unit(_cardNo, axis, ref speed);
        if (ret != 0)
            return Result<double>.Fail($"读取速度失败, error code: {ret}");
        return Result<double>.Success(speed);
    }

    /// <summary>
    /// 读取指定输入口电平
    /// </summary>
    public async Task<Result<bool>> ReadInbitAsync(ushort bitNo)
    {
        short level = LTDMC.dmc_read_inbit(_cardNo, bitNo);
        if (level < 0)
            return Result<bool>.Fail($"读取输入口 {bitNo} 失败");
        return Result<bool>.Success(level == 1);
    }

    /// <summary>批量读取输入位</summary>
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

    /// <summary>
    /// 读取指定输出口当前电平
    /// </summary>
    public async Task<Result<bool>> ReadOutbitAsync(ushort bitNo)
    {
        short level = LTDMC.dmc_read_outbit(_cardNo, bitNo);
        if (level < 0)
            return Result<bool>.Fail($"读取输出口 {bitNo} 失败");
        return Result<bool>.Success(level == 1);
    }

    /// <summary>
    /// 设置指定输出口电平
    /// </summary>
    public async Task<Result> WriteOutbitAsync(ushort bitNo, bool on)
    {
        ushort level = on ? (ushort)1 : (ushort)0;
        var ret = LTDMC.dmc_write_outbit(_cardNo, bitNo, level);
        if (ret != 0)
            return Result.Fail($"写入输出口 {bitNo} 失败, error code: {ret}");
        return Result.Success();
    }
    /// <summary>
    /// 设置软件限位
    /// </summary>
    public async Task<Result> SetSoftLimitAsync(
        ushort axis,
        double negativeLimit,
        double positiveLimit,
        bool enable = true)
    {
        var ret = LTDMC.dmc_set_softlimit_unit(
            _cardNo,
            axis,
            enable ? (ushort)1 : (ushort)0,  // enable
            0,                                // source_sel: 0=指令位置
            1,                                // SL_action: 1=减速停止
            negativeLimit,
            positiveLimit);

        if (ret != 0)
            return Result.Fail($"设置软限位失败, error code: {ret}");
        return Result.Success();
    }

    // --- PDO 读写（按 OD 地址直接操作） ---

    /// <summary>写从站 RxPDO（按 index/subindex 指定 OD 地址）</summary>
    public async Task<Result> WriteRxPDOAsync(ushort slaveAddr, ushort index, ushort subIndex, ushort bitLength, int value)
    {
        if (!IsConnected) return Result.Fail("板卡未连接");
        var data = BitConverter.GetBytes(value);
        var ret = LTDMC.nmc_write_rxpdo(_cardNo, EthercatPort, slaveAddr, index, subIndex, bitLength, data);
        if (ret != 0)
            return Result.Fail($"写RxPDO从站{slaveAddr} 0x{index:X4}:{subIndex} 失败, error code: {ret}");
        return Result.Success();
    }

    /// <summary>读从站 TxPDO（按 index/subindex 指定 OD 地址）</summary>
    public async Task<Result<int>> ReadTxPDOAsync(ushort slaveAddr, ushort index, ushort subIndex, ushort bitLength)
    {
        if (!IsConnected) return Result<int>.Fail("板卡未连接");
        var data = new byte[bitLength/8];
        var ret = LTDMC.nmc_read_txpdo(_cardNo, EthercatPort, slaveAddr, index, subIndex, bitLength, data);
        if (ret != 0)
            return Result<int>.Fail($"读TxPDO从站{slaveAddr} 0x{index:X4}:{subIndex} 失败, error code: {ret}");
        return Result<int>.Success(BytesToInt(data));
    }
    public static int BytesToInt(byte[] bytes, bool useLowBytes = true)
    {
        if (bytes == null) throw new ArgumentNullException(nameof(bytes));

        // 创建4字节缓冲区
        byte[] buffer = new byte[4];

        if (bytes.Length >= 4)
        {
            // 取低4字节（索引0-3）或高4字节（末尾4字节）
            int startIndex = useLowBytes ? 0 : bytes.Length - 4;
            Array.Copy(bytes, startIndex, buffer, 0, 4);
        }
        else
        {
            // 不足4字节，复制到低位，高位自动补0
            Array.Copy(bytes, 0, buffer, 0, bytes.Length);
        }

        return BitConverter.ToInt32(buffer, 0);
    }
    public void Dispose()
    {
        // 关闭板卡，释放资源
        if (IsConnected)
        {
            LTDMC.dmc_board_close();
            IsConnected = false;
        }
    }
}