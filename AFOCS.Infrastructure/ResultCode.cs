namespace AFOCS.Infrastructure;

/// <summary>
/// 通用操作状态码
/// </summary>
public enum ResultCode
{
    /// <summary>成功</summary>
    Success = 0,
    /// <summary>通用失败</summary>
    Fail = -1,
    /// <summary>参数错误</summary>
    InvalidParam = -2,
    /// <summary>超时</summary>
    Timeout = -3,
    /// <summary>权限不足</summary>
    NoPermission = -4,
    /// <summary>系统异常</summary>
    SystemError = -5
}