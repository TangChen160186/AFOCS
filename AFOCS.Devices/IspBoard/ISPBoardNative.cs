using System.Runtime.InteropServices;

namespace AFOCS.Devices.IspBoard;

/// <summary>
/// ISPBoard.dll P/Invoke 封装。
/// 
/// 内存约定：C++ 端用 CoTaskMemAlloc 分配的输出（字符串、数组），C# 端用 Marshal.FreeCoTaskMem 释放。
/// 调用者分配的数值缓冲区（dataOut, mpdOutAdc 等）由调用者管理。
/// </summary>
internal static class IspBoardNative
{
    private const string DllName = "ISPBoard.dll";

    // ====================================================================
    // P/Invoke 声明
    // ====================================================================

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void IspInterfaceInitialEx_c(
        [MarshalAs(UnmanagedType.LPStr)] string productCfgFile,
        out IntPtr appNames,    out uint appNameCount,
        out IntPtr deviceVisa,  out uint deviceVisaCount,
        out IntPtr errorInfo,   out ushort errorSize);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void IspDutReadWriteEx(
        uint devIndex,
        byte dutSlot, byte dutChannel,
        [MarshalAs(UnmanagedType.LPStr)] string appName,
        byte operation,
        IntPtr dataIn,          ushort dataInCount,
        IntPtr dataOut,         ref ushort dataOutCount,
        out IntPtr errorInfo,   out ushort errorSize);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void IspFormularCalc(
        [MarshalAs(UnmanagedType.LPStr)] string appName,
        IntPtr dataIn,          ushort dataInCount,
        out double result,
        out IntPtr errorInfo,   out ushort errorSize);

    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void IspDutHeaterScanEx(
        uint devIndex,
        byte dutSlot, byte dutChannel,
        [MarshalAs(UnmanagedType.LPStr)] string appName,
        IntPtr dataIn,          ushort dataInCount,
        IntPtr mpdOutAdc,       ref ushort mpdOutAdcCount,
        IntPtr mpdInAdc,        ref ushort mpdInAdcCount,
        out IntPtr errorInfo,   out ushort errorSize);

    // ====================================================================
    // 辅助方法
    // ====================================================================

    /// <summary>读取 CoTaskMemAlloc 分配的字符串，并释放内存。null/空返回 null 表示成功。</summary>
    public static string? ReadError(IntPtr ptr, ushort size)
    {
        if (ptr == IntPtr.Zero || size <= 1)
            return null;

        string s = Marshal.PtrToStringAnsi(ptr, size - 1)!;
        Marshal.FreeCoTaskMem(ptr);
        return s;
    }

    /// <summary>读取 CoTaskMemAlloc 分配的字符串数组（char**）, 并释放所有内存。</summary>
    public static string[] ReadStrArray(IntPtr ptr, uint count)
    {
        if (ptr == IntPtr.Zero || count == 0)
            return [];

        var result = new string[count];
        for (int i = 0; i < count; i++)
        {
            IntPtr strPtr = Marshal.ReadIntPtr(ptr, i * IntPtr.Size);
            result[i] = strPtr != IntPtr.Zero
                ? Marshal.PtrToStringAnsi(strPtr) ?? ""
                : "";
            Marshal.FreeCoTaskMem(strPtr);
        }
        Marshal.FreeCoTaskMem(ptr);
        return result;
    }

    /// <summary>分配缓冲区并写入 ushort 数组，返回 (ptr, count)</summary>
    public static (IntPtr ptr, ushort count) AllocUInt16Buf(ushort[]? data)
    {
        if (data == null || data.Length == 0)
            return (IntPtr.Zero, 0);

        int byteLen = data.Length * 2;
        IntPtr ptr = Marshal.AllocHGlobal(byteLen);
        for (int i = 0; i < data.Length; i++)
            Marshal.WriteInt16(ptr, i * 2, (short)data[i]);
        return (ptr, (ushort)data.Length);
    }

    /// <summary>从缓冲区读取 ushort 数组</summary>
    public static ushort[] ReadUInt16Array(IntPtr ptr, int count)
    {
        if (ptr == IntPtr.Zero || count <= 0) return [];
        var result = new ushort[count];
        for (int i = 0; i < count; i++)
            result[i] = (ushort)Marshal.ReadInt16(ptr, i * 2);
        return result;
    }

    /// <summary>分配 double 数组并写入数据</summary>
    public static (IntPtr ptr, ushort count) AllocDoubleBuf(double[]? data)
    {
        if (data == null || data.Length == 0)
            return (IntPtr.Zero, 0);

        int byteLen = data.Length * sizeof(double);
        IntPtr ptr = Marshal.AllocHGlobal(byteLen);
        Marshal.Copy(data, 0, ptr, data.Length);
        return (ptr, (ushort)data.Length);
    }

    /// <summary>分配 HGlobal 缓冲区</summary>
    public static IntPtr AllocHGlobal(int byteSize)
        => byteSize > 0 ? Marshal.AllocHGlobal(byteSize) : IntPtr.Zero;

    /// <summary>释放 HGlobal 缓冲区</summary>
    public static void FreeHGlobal(IntPtr ptr)
    {
        if (ptr != IntPtr.Zero)
            Marshal.FreeHGlobal(ptr);
    }
}