using System.Runtime.InteropServices;
using System.Text;

namespace AFOCS.Devices.Implementation
{
    /// <summary>
    /// ISPBoard.dll P/Invoke 封装。
    /// 
    /// 传统 C 风格 API：调用者分配缓冲区，DLL 写入数据，out 参数返回实际长度。
    /// 字符串缓冲区传 0 可先查询所需大小。
    /// </summary>
    internal static class ISPBoardNative
    {
        private const string DllName = "ISPBoard.dll";

        // ====================================================================
        // P/Invoke 声明
        // ====================================================================

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int ISP_Initialize(
            [MarshalAs(UnmanagedType.LPStr)] string productCfgFile,
            IntPtr appNamesBuf,      int appNamesBufSize,   out int appNamesLen,
            IntPtr deviceVisaBuf,    int deviceVisaBufSize, out int deviceVisaLen,
            IntPtr errBuf,           int errBufSize,        out int errLen);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int ISP_EnterEngMode(
            uint devIndex,
            byte enterEng,
            IntPtr engStatusBuf,    int engStatusBufSize, out int engStatusLen,
            IntPtr errBuf,          int errBufSize,       out int errLen);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int ISP_DutReadWrite(
            uint devIndex,
            byte dutSlot,
            byte dutChannel,
            [MarshalAs(UnmanagedType.LPStr)] string appName,
            ushort operation,
            IntPtr dataIn,          int dataInLen,
            IntPtr dataOutBuf,      int dataOutBufSize,   out int dataOutLen,
            IntPtr errBuf,          int errBufSize,       out int errLen);

        [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
        public static extern int ISP_HeaterScan(
            uint devIndex,
            byte dutSlot,
            byte dutChannel,
            [MarshalAs(UnmanagedType.LPStr)] string appName,
            IntPtr dataIn,          int dataInLen,
            IntPtr mpdOutBuf,       int mpdOutBufSize,    out int mpdOutLen,
            IntPtr mpdInBuf,        int mpdInBufSize,     out int mpdInLen,
            IntPtr errBuf,          int errBufSize,       out int errLen);

        // ====================================================================
        // 辅助：错误检查
        // ====================================================================

        /// <summary>检查错误码和错误信息，返回 null=成功, 非null=错误信息</summary>
        public static string? CheckError(int retCode, IntPtr errBuf, int errBufSize, int errLen)
        {
            if (retCode == 0 && errLen <= 1)
                return null;

            if (errBuf == IntPtr.Zero || errLen <= 1)
                return $"错误码: {retCode}";

            byte[] bytes = new byte[errLen - 1]; // 排除 null 终止符
            Marshal.Copy(errBuf, bytes, 0, bytes.Length);
            return Encoding.ASCII.GetString(bytes);
        }

        // ====================================================================
        // 辅助：两个阶段读取（先查大小，再读数据）
        // ====================================================================

        /// <summary>两个阶段读取字符串列表（\0 分隔，双\0结尾）</summary>
        public static string[] ReadMultiStr(IntPtr buf, int len)
        {
            if (buf == IntPtr.Zero || len <= 1)
                return [];

            var result = new List<string>();
            int start = 0;
            for (int i = 0; i < len; i++)
            {
                byte b = Marshal.ReadByte(buf, i);
                if (b == 0)
                {
                    int segLen = i - start;
                    if (segLen > 0)
                    {
                        byte[] seg = new byte[segLen];
                        Marshal.Copy(buf + start, seg, 0, segLen);
                        result.Add(Encoding.ASCII.GetString(seg));
                    }
                    start = i + 1;
                    // 双 null = 结束
                    if (i + 1 < len && Marshal.ReadByte(buf, i + 1) == 0)
                        break;
                }
            }
            return [.. result];
        }

        /// <summary>两个阶段读取 ushort 数组</summary>
        public static ushort[] ReadUInt16Array(IntPtr buf, int len)
        {
            if (buf == IntPtr.Zero || len <= 0) return [];

            var result = new ushort[len];
            for (int i = 0; i < len; i++)
                result[i] = (ushort)Marshal.ReadInt16(buf, i * 2);
            return result;
        }

        /// <summary>两个阶段读取 byte 数组</summary>
        public static byte[] ReadByteArray(IntPtr buf, int len)
        {
            if (buf == IntPtr.Zero || len <= 0) return [];

            byte[] result = new byte[len];
            Marshal.Copy(buf, result, 0, len);
            return result;
        }

        // ====================================================================
        // 辅助：分配 / 写入 / 释放
        // ====================================================================

        /// <summary>分配缓冲区并写入 ushort 数组，返回 (IntPtr, elementCount)</summary>
        public static (IntPtr ptr, int count) AllocUInt16Buf(ushort[]? data)
        {
            int count = data?.Length ?? 0;
            if (count == 0) return (IntPtr.Zero, 0);

            IntPtr ptr = Marshal.AllocHGlobal(count * 2);
            for (int i = 0; i < count; i++)
                Marshal.WriteInt16(ptr, i * 2, (short)data![i]);
            return (ptr, count);
        }

        /// <summary>分配 byte 缓冲区</summary>
        public static IntPtr AllocBuf(int size)
            => size > 0 ? Marshal.AllocHGlobal(size) : IntPtr.Zero;

        /// <summary>释放缓冲区</summary>
        public static void FreeBuf(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
                Marshal.FreeHGlobal(ptr);
        }
    }
}
