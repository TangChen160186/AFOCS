#include "IspBoard.h"
#include <cstring>
#include <windows.h>
#include <objbase.h> // CoTaskMemAlloc

// ============================================================================
// 内存管理说明:
// 本 DLL 使用 CoTaskMemAlloc 分配所有输出内存，C# 端可通过
// Marshal.FreeCoTaskMem / Marshal.PtrToStringAnsi 等方式释放和读取。
// LStr 结构: [int32 cnt][uChar data[cnt]]
// ============================================================================

// 分配一个空的 LStrHandle (空字符串，表示无错误)
static void AllocEmptyLStr(LStrHandle *h)
{
    *h = (LStrHandle)CoTaskMemAlloc(sizeof(LStr));
    if (*h) {
        memset(*h, 0, sizeof(LStr));
    }
}

// 分配一个带错误消息的 LStrHandle
static void AllocErrorLStr(LStrHandle *h, const char *msg)
{
    int32 len = (int32)strlen(msg);
    *h = (LStrHandle)CoTaskMemAlloc(sizeof(LStr) + len);
    if (*h) {
        (**h)->cnt = len;
        memcpy((**h)->str, msg, len);
    }
}

// 读取 LStrHandle 输入字符串为 C 字符串
static const char* ReadLStr(LStrHandle h)
{
    if (!h || !*h) return "";
    static char buf[4096];
    int32 len = (**h).cnt;
    if (len > 4095) len = 4095;
    memcpy(buf, (**h).str, len);
    buf[len] = '\0';
    return buf;
}

// 创建一个 LStrHandle (用于数组元素)
static LStrHandle CreateLStr(const char *str)
{
    int32 len = (int32)strlen(str);
    LStrHandle h = (LStrHandle)CoTaskMemAlloc(sizeof(LStr) + len);
    if (h) {
        (*h)->cnt = len;
        memcpy((*h)->str, str, len);
    }
    return h;
}

// ============================================================================
// 全局状态
// ============================================================================
static struct {
    bool initialized;
    // TODO: 添加设备上下文、VISA session 等
} g_state;

// ============================================================================
// InterfaceInitialEx_C
// 通过产品配置文件初始化设备接口
//
// C# 调用示例:
//   [DllImport("ISPBoard.dll", CallingConvention = CallingConvention.StdCall)]
//   static extern void InterfaceInitialEx_C(
//       IntPtr productCfgFile,    // LStrHandle (输入字符串)
//       ref IntPtr appNames,      // TD1Hdl* (输出: 字符串数组)
//       ref IntPtr deviceVisa,    // TD6Hdl* (输出: VISA地址数组)
//       ref IntPtr errInfo);      // LStrHandle* (输出: 错误信息, 空=成功)
// ============================================================================
extern "C" void __stdcall InterfaceInitialEx_C(
    LStrHandle *ProductCfgFile,
    TD1Hdl     *AppNames,
    TD6Hdl     *DeviceVisa,
    LStrHandle *ErrInfo)
{
    // 读取配置文件内容
    const char* cfgContent = "";
    if (ProductCfgFile && *ProductCfgFile) {
        cfgContent = ReadLStr(*ProductCfgFile);
    }
    // TODO: 解析 ProductCfgFile 中的 JSON/XML 配置
    // TODO: 根据配置初始化硬件接口 (VISA/串口/TCP等)

    // --- 分配 AppNames (TD1: 字符串数组) ---
    {
        const int32 nameCount = 1; // TODO: 从配置文件中读取
        size_t structSize = sizeof(int32_t) + nameCount * sizeof(LStrHandle);

        *AppNames = (TD1Hdl)CoTaskMemAlloc(sizeof(TD1) + (nameCount - 1) * sizeof(LStrHandle));
        if (!*AppNames) {
            AllocErrorLStr(ErrInfo, "Failed to allocate AppNames");
            return;
        }
        memset(*AppNames, 0, sizeof(TD1) + (nameCount - 1) * sizeof(LStrHandle));
        (**AppNames)->dimSize = nameCount;

        // TODO: 从配置加载实际 App 名称
        (**AppNames)->String[0] = CreateLStr("DefaultApp");
    }

    // --- 分配 DeviceVisa (TD6: VISA地址字符串数组) ---
    {
        const int32 devCount = 1; // TODO: 从配置文件中读取
        *DeviceVisa = (TD6Hdl)CoTaskMemAlloc(sizeof(TD6) + (devCount - 1) * sizeof(LStrHandle));
        if (!*DeviceVisa) {
            AllocErrorLStr(ErrInfo, "Failed to allocate DeviceVisa");
            return;
        }
        memset(*DeviceVisa, 0, sizeof(TD6) + (devCount - 1) * sizeof(LStrHandle));
        (**DeviceVisa)->dimSize = devCount;

        // TODO: 从配置加载实际 VISA 地址
        (**DeviceVisa)->DeviceVisa[0] = CreateLStr("TCPIP0::127.0.0.1::5025::SOCKET");
    }

    g_state.initialized = true;
    AllocEmptyLStr(ErrInfo);
}

// ============================================================================
// DutEnterEngEx
// 进入/退出工程模式
//
// C# 调用示例:
//   [DllImport("ISPBoard.dll", CallingConvention = CallingConvention.StdCall)]
//   static extern void DutEnterEngEx(
//       uint devIndex,            // 设备索引
//       byte enterEng,            // 0=退出, 非0=进入
//       ref IntPtr engStatus,     // TD9Hdl* (输出: uint8数组, dimSize+data)
//       ref IntPtr errInfo);      // LStrHandle* (输出: 错误信息)
// ============================================================================
extern "C" void __stdcall DutEnterEngEx(
    uint32_t   DevIndex,
    uint8_t    EnterEng,
    TD9Hdl    *EngStatus,
    LStrHandle *ErrInfo)
{
    if (!g_state.initialized) {
        AllocErrorLStr(ErrInfo, "Interface not initialized");
        return;
    }

    // TODO: 通过 VISA/串口 发送工程模式命令
    // EnterEng: 0 = 退出工程模式, 非0 = 进入工程模式
    // DevIndex: 设备索引

    // --- 分配 EngStatus (TD9: uint8数组, 结构=[dimSize][data...]) ---
    {
        const int32 statusCount = 4; // TODO: 根据实际状态数量调整
        *EngStatus = (TD9Hdl)CoTaskMemAlloc(sizeof(TD9) + (statusCount - 1) * sizeof(uint8_t));
        if (!*EngStatus) {
            AllocErrorLStr(ErrInfo, "Failed to allocate EngStatus");
            return;
        }
        memset(*EngStatus, 0, sizeof(TD9) + (statusCount - 1) * sizeof(uint8_t));
        (**EngStatus)->dimSize = statusCount;

        // TODO: 填充实际工程模式状态
        for (int32 i = 0; i < statusCount; i++) {
            (**EngStatus)->_[i] = EnterEng ? (uint8_t)1 : (uint8_t)0;
        }
    }

    AllocEmptyLStr(ErrInfo);
}

// ============================================================================
// DutHeaterScanEx
// 加热器扫描
//
// C# 调用示例:
//   [DllImport("ISPBoard.dll", CallingConvention = CallingConvention.StdCall)]
//   static extern void DutHeaterScanEx(
//       uint devIndex,
//       byte dutSlot, byte dutChannel,
//       IntPtr appName,          // LStrHandle (输入)
//       IntPtr dataIn,           // TD2Hdl (输入: uint16数组)
//       ref IntPtr mpdOutADC,    // TD5Hdl* (输出: uint16数组)
//       ref IntPtr mpdInADC,     // TD2Hdl* (输出: uint16数组)
//       ref IntPtr errInfo);     // LStrHandle* (输出)
// ============================================================================
extern "C" void __stdcall DutHeaterScanEx(
    uint32_t   DevIndex,
    uint8_t    DutSlot,
    uint8_t    DutChannel,
    LStrHandle *AppName,
    TD2Hdl    *DataIn,
    TD5Hdl    *MpdOutADC,
    TD2Hdl    *MpdInADC,
    LStrHandle *ErrInfo)
{
    if (!g_state.initialized) {
        AllocErrorLStr(ErrInfo, "Interface not initialized");
        return;
    }

    const char* appName = AppName && *AppName ? ReadLStr(*AppName) : "";
    int32 inCount = (DataIn && *DataIn) ? (**DataIn)->dimSize : 0;

    // TODO: 通过 VISA/串口 执行加热器扫描
    // DutSlot: DUT插槽号, DutChannel: DUT通道号
    // DataIn: 加热器控制参数 (uint16数组), 返回 MpdOutADC, MpdInADC

    // --- 分配 MpdOutADC (TD5: uint16数组) ---
    {
        const int32 adcCount = inCount > 0 ? inCount : 8;
        *MpdOutADC = (TD5Hdl)CoTaskMemAlloc(sizeof(TD5) + (adcCount - 1) * sizeof(uint16_t));
        if (!*MpdOutADC) {
            AllocErrorLStr(ErrInfo, "Failed to allocate MpdOutADC");
            return;
        }
        memset(*MpdOutADC, 0, sizeof(TD5) + (adcCount - 1) * sizeof(uint16_t));
        (**MpdOutADC)->dimSize = adcCount;
        // TODO: 填充实际 ADC 数据
    }

    // --- 分配 MpdInADC (TD2: uint16数组) ---
    {
        const int32 adcCount = inCount > 0 ? inCount : 8;
        *MpdInADC = (TD2Hdl)CoTaskMemAlloc(sizeof(TD2) + (adcCount - 1) * sizeof(uint16_t));
        if (!*MpdInADC) {
            AllocErrorLStr(ErrInfo, "Failed to allocate MpdInADC");
            return;
        }
        memset(*MpdInADC, 0, sizeof(TD2) + (adcCount - 1) * sizeof(uint16_t));
        (**MpdInADC)->dimSize = adcCount;
        // TODO: 填充实际 ADC 数据
    }

    AllocEmptyLStr(ErrInfo);
}

// ============================================================================
// DutReadWriteEx
// DUT寄存器读写
//
// C# 调用示例:
//   [DllImport("ISPBoard.dll", CallingConvention = CallingConvention.StdCall)]
//   static extern void DutReadWriteEx(
//       uint devIndex,
//       byte dutSlot, byte dutChannel,
//       IntPtr appName,          // LStrHandle (输入)
//       ushort operation,        // 操作类型
//       IntPtr dataIn,           // TD2Hdl (输入: uint16数组)
//       ref IntPtr dataOut,      // TD2Hdl* (输出: uint16数组)
//       ref IntPtr errInfo);     // LStrHandle* (输出)
// ============================================================================
extern "C" void __stdcall DutReadWriteEx(
    uint32_t   DevIndex,
    uint8_t    DutSlot,
    uint8_t    DutChannel,
    LStrHandle *AppName,
    uint16_t   Operation,
    TD2Hdl    *DataIn,
    TD2Hdl    *DataOut,
    LStrHandle *ErrInfo)
{
    if (!g_state.initialized) {
        AllocErrorLStr(ErrInfo, "Interface not initialized");
        return;
    }

    const char* appName = AppName && *AppName ? ReadLStr(*AppName) : "";
    int32 inCount = (DataIn && *DataIn) ? (**DataIn)->dimSize : 0;

    // TODO: 通过 VISA/串口 执行寄存器读写
    // Operation: 0=读, 1=写, 其他=自定义操作
    // DataIn: 写入数据 (写操作时有效)

    // --- 分配 DataOut (TD2: uint16数组) ---
    {
        const int32 outCount = inCount > 0 ? inCount : 16;
        *DataOut = (TD2Hdl)CoTaskMemAlloc(sizeof(TD2) + (outCount - 1) * sizeof(uint16_t));
        if (!*DataOut) {
            AllocErrorLStr(ErrInfo, "Failed to allocate DataOut");
            return;
        }
        memset(*DataOut, 0, sizeof(TD2) + (outCount - 1) * sizeof(uint16_t));
        (**DataOut)->dimSize = outCount;
        // TODO: 填充实际读取数据
    }

    AllocEmptyLStr(ErrInfo);
}

// ============================================================================
// 释放内存辅助函数 (C# 端使用 Marshal.FreeCoTaskMem 手动释放)
//
// 注意: 所有通过本 DLL 分配的输出参数均使用 CoTaskMemAlloc，
// C# 端使用 Marshal.FreeCoTaskMem(ptr) 释放顶层 handle。
// 嵌套 LStrHandle (如 TD1 中的字符串) 需先逐个释放。
// ============================================================================
