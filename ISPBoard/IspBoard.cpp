#include "IspBoard.h"
#include <cstring>
#include <cstdio>

// ============================================================================
// 全局状态
// ============================================================================
static struct {
    bool initialized;
    // TODO: 添加设备上下文、VISA session 等
} g_state;

// ============================================================================
// 内部辅助函数
// ============================================================================

static bool WriteStrToBuf(char* buf, int bufSize, int* outLen, const char* str)
{
    if (!buf || !outLen) return false;
    int len = (int)strlen(str);
    if (bufSize > 0 && len < bufSize) {
        memcpy(buf, str, len);
        buf[len] = '\0';
    }
    *outLen = len + 1; // 包含 null 终止符
    return true;
}

static bool WriteMultiStrToBuf(char* buf, int bufSize, int* outLen,
    const char** strs, int strCount)
{
    if (!buf || !outLen) return false;

    int total = 0;
    for (int i = 0; i < strCount; i++) {
        int len = (int)strlen(strs[i]) + 1; // +1 for null terminator
        if (bufSize > 0 && total + len <= bufSize) {
            memcpy(buf + total, strs[i], len);
        }
        total += len;
    }

    if (bufSize > 0 && total < bufSize)
        buf[total] = '\0'; // double null terminate
    if (total + 1 <= bufSize)
        total += 1; // final null terminator

    *outLen = total;
    return true;
}

static void FormatErrorCode(char* buf, int bufSize, int* outLen,
    int errCode, const char* msg)
{
    if (!buf || !outLen) return;
    int len = snprintf(buf, bufSize, "%d|%s", errCode, msg);
    *outLen = (len < bufSize) ? len + 1 : bufSize;
}

// ============================================================================
// ISP_Initialize
// 初始化 ISP Board，加载产品配置文件
//
// 参数:
//   productCfgFile    [in]  产品配置文件内容（JSON 字符串）
//   appNamesBuf       [out] 应用名称缓冲区（多个以 \0 分隔，双 \0 结尾）
//   appNamesBufSize   [in]  缓冲区大小（字节），传0可先查询所需大小
//   appNamesLen       [out] 实际写入长度（含终止符），总是被设置
//   deviceVisaBuf     [out] VISA 地址缓冲区（格式同 appNames）
//   deviceVisaBufSize [in]  缓冲区大小
//   deviceVisaLen     [out] 实际写入长度
//   errBuf            [out] 错误信息缓冲区
//   errBufSize        [in]  错误信息缓冲区大小
//   errLen            [out] 错误信息长度
//
// 返回值: 0=成功, 非0=失败
// ============================================================================
extern "C" int __stdcall ISP_Initialize(
    const char* productCfgFile,
    char*       appNamesBuf,      int appNamesBufSize,   int* appNamesLen,
    char*       deviceVisaBuf,    int deviceVisaBufSize, int* deviceVisaLen,
    char*       errBuf,           int errBufSize,        int* errLen)
{
    // TODO: 解析 ProductCfgFile 中的 JSON/XML 配置
    // TODO: 根据配置初始化硬件接口 (VISA/串口/TCP等)

    // --- 输出 AppNames ---
    {
        const char* apps[] = { "DefaultApp" };
        WriteMultiStrToBuf(appNamesBuf, appNamesBufSize, appNamesLen,
            apps, sizeof(apps) / sizeof(apps[0]));
    }

    // --- 输出 DeviceVisa ---
    {
        const char* visas[] = { "TCPIP0::127.0.0.1::5025::SOCKET" };
        WriteMultiStrToBuf(deviceVisaBuf, deviceVisaBufSize, deviceVisaLen,
            visas, sizeof(visas) / sizeof(visas[0]));
    }

    // --- 错误信息（空=成功） ---
    WriteStrToBuf(errBuf, errBufSize, errLen, "");

    g_state.initialized = true;
    return 0;
}

// ============================================================================
// ISP_EnterEngMode
// 进入/退出工程模式
//
// 参数:
//   devIndex      [in]  设备索引
//   enterEng      [in]  0=退出工程模式, 非0=进入工程模式
//   engStatusBuf  [out] 工程模式状态字节数组
//   engStatusBufSize [in] 缓冲区大小
//   engStatusLen  [out] 实际长度
//   errBuf        [out] 错误信息缓冲区
//   errBufSize    [in]  缓冲区大小
//   errLen        [out] 错误信息长度
//
// 返回值: 0=成功, 非0=失败
// ============================================================================
extern "C" int __stdcall ISP_EnterEngMode(
    uint32_t    devIndex,
    uint8_t     enterEng,
    uint8_t*    engStatusBuf,    int engStatusBufSize, int* engStatusLen,
    char*       errBuf,          int errBufSize,       int* errLen)
{
    if (!g_state.initialized) {
        FormatErrorCode(errBuf, errBufSize, errLen, -1, "Interface not initialized");
        return -1;
    }

    // TODO: 通过 VISA/串口 发送工程模式命令

    // --- 输出 EngStatus ---
    {
        const int statusCount = 4; // TODO: 根据实际状态数量调整
        uint8_t status[4];
        for (int i = 0; i < statusCount; i++)
            status[i] = enterEng ? (uint8_t)1 : (uint8_t)0;

        int copyLen = (statusCount < engStatusBufSize) ? statusCount : engStatusBufSize;
        if (engStatusBuf && copyLen > 0)
            memcpy(engStatusBuf, status, copyLen);
        if (engStatusLen)
            *engStatusLen = statusCount;
    }

    WriteStrToBuf(errBuf, errBufSize, errLen, "");
    return 0;
}

// ============================================================================
// ISP_DutReadWrite
// DUT 寄存器读写
//
// 参数:
//   devIndex      [in]  设备索引
//   dutSlot       [in]  DUT 插槽号
//   dutChannel    [in]  DUT 通道号
//   appName       [in]  应用名称（null-terminated）
//   operation     [in]  操作类型 (0=读, 1=写, 其他=自定义)
//   dataIn        [in]  写入数据（写操作时有效，可为 NULL）
//   dataInLen     [in]  dataIn 的元素个数
//   dataOutBuf    [out] 读取数据缓冲区
//   dataOutBufSize [in] 缓冲区能容纳的元素个数
//   dataOutLen    [out] 实际输出元素个数
//   errBuf        [out] 错误信息缓冲区
//   errBufSize    [in]  缓冲区大小
//   errLen        [out] 错误信息长度
//
// 返回值: 0=成功, 非0=失败
// ============================================================================
extern "C" int __stdcall ISP_DutReadWrite(
    uint32_t     devIndex,
    uint8_t      dutSlot,
    uint8_t      dutChannel,
    const char*  appName,
    uint16_t     operation,
    const uint16_t* dataIn,     int dataInLen,
    uint16_t*    dataOutBuf,    int dataOutBufSize, int* dataOutLen,
    char*        errBuf,        int errBufSize,     int* errLen)
{
    if (!g_state.initialized) {
        FormatErrorCode(errBuf, errBufSize, errLen, -1, "Interface not initialized");
        return -1;
    }

    // TODO: 通过 VISA/串口 执行寄存器读写

    // --- 输出 DataOut ---
    {
        const int outCount = dataInLen > 0 ? dataInLen : 16;
        int copyLen = (outCount < dataOutBufSize) ? outCount : dataOutBufSize;
        if (dataOutBuf && copyLen > 0)
            memset(dataOutBuf, 0, copyLen * sizeof(uint16_t)); // TODO: 填充实际数据
        if (dataOutLen)
            *dataOutLen = outCount;
    }

    WriteStrToBuf(errBuf, errBufSize, errLen, "");
    return 0;
}

// ============================================================================
// ISP_HeaterScan
// 加热器扫描
//
// 参数:
//   devIndex      [in]  设备索引
//   dutSlot       [in]  DUT 插槽号
//   dutChannel    [in]  DUT 通道号
//   appName       [in]  应用名称
//   dataIn        [in]  加热器控制参数（uint16 数组）
//   dataInLen     [in]  dataIn 的元素个数
//   mpdOutBuf     [out] MPD 输出 ADC 缓冲区
//   mpdOutBufSize [in]  缓冲区大小
//   mpdOutLen     [out] 实际元素个数
//   mpdInBuf      [out] MPD 输入 ADC 缓冲区
//   mpdInBufSize  [in]  缓冲区大小
//   mpdInLen      [out] 实际元素个数
//   errBuf        [out] 错误信息缓冲区
//   errBufSize    [in]  缓冲区大小
//   errLen        [out] 错误信息长度
//
// 返回值: 0=成功, 非0=失败
// ============================================================================
extern "C" int __stdcall ISP_HeaterScan(
    uint32_t     devIndex,
    uint8_t      dutSlot,
    uint8_t      dutChannel,
    const char*  appName,
    const uint16_t* dataIn,     int dataInLen,
    uint16_t*    mpdOutBuf,     int mpdOutBufSize, int* mpdOutLen,
    uint16_t*    mpdInBuf,      int mpdInBufSize,  int* mpdInLen,
    char*        errBuf,        int errBufSize,    int* errLen)
{
    if (!g_state.initialized) {
        FormatErrorCode(errBuf, errBufSize, errLen, -1, "Interface not initialized");
        return -1;
    }

    // TODO: 通过 VISA/串口 执行加热器扫描

    // --- 输出 MpdOutADC ---
    {
        const int outCount = dataInLen > 0 ? dataInLen : 8;
        int copyLen = (outCount < mpdOutBufSize) ? outCount : mpdOutBufSize;
        if (mpdOutBuf && copyLen > 0)
            memset(mpdOutBuf, 0, copyLen * sizeof(uint16_t)); // TODO: 填充实际数据
        if (mpdOutLen)
            *mpdOutLen = outCount;
    }

    // --- 输出 MpdInADC ---
    {
        const int outCount = dataInLen > 0 ? dataInLen : 8;
        int copyLen = (outCount < mpdInBufSize) ? outCount : mpdInBufSize;
        if (mpdInBuf && copyLen > 0)
            memset(mpdInBuf, 0, copyLen * sizeof(uint16_t)); // TODO: 填充实际数据
        if (mpdInLen)
            *mpdInLen = outCount;
    }

    WriteStrToBuf(errBuf, errBufSize, errLen, "");
    return 0;
}
