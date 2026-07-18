namespace AFOCS.FlowNodeEditor.Services
{
    /// <summary>
    /// 可执行节点接口。输入来自属性级 [NodeInput]（框架自动赋值），
    /// 输出写到属性级 [NodeOutput]（框架自动读取），ExecuteAsync 只需处理业务逻辑。
    /// </summary>
    public interface IExecutableNode
    {
        /// <summary>
        /// 执行节点逻辑。输入值已由框架自动写入实例属性，输出值直接写实例属性即可。
        /// </summary>
        /// <param name="context">流程图共享上下文</param>
        Task ExecuteAsync(Dictionary<string, object?> context);
    }
}
