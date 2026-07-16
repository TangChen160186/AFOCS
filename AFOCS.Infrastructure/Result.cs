namespace AFOCS.Infrastructure
{
    public class Result
    {
        public ResultCode Code { get; set; }
        public bool IsSuccess => Code == ResultCode.Success;
        public string Message { get; set; } = string.Empty;
        public Exception? Exception { get; set; }

        public static Result Success(string msg = "操作成功")
        {
            return new Result
            {
                Code = ResultCode.Success,
                Message = msg
            };
        }

        public static Result Fail(ResultCode code, string msg, Exception? ex = null)
        {
            return new Result
            {
                Code = code,
                Message = msg,
                Exception = ex
            };
        }

        public static Result Fail(string msg, Exception? ex = null)
        {
            return new Result
            {
                Code = ResultCode.Fail,
                Message = msg,
                Exception = ex
            };
        }
    }

    public class Result<T>
    {
        public ResultCode Code { get; set; }
        public bool IsSuccess => Code == ResultCode.Success;
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public Exception? Exception { get; set; }

        public static Result<T> Success(T? data, string msg = "操作成功")
        {
            return new Result<T>
            {
                Code = ResultCode.Success,
                Data = data,
                Message = msg
            };
        }
        public static Result<T> Fail(ResultCode code, string msg, Exception? ex = null)
        {
            return new Result<T>
            {
                Code = code,
                Message = msg,
                Exception = ex
            };
        }

        public static Result<T> Fail(string msg, Exception? ex = null)
        {
            return new Result<T>
            {
                Code = ResultCode.Fail,
                Message = msg,
                Exception = ex
            };
        }
    }
}
