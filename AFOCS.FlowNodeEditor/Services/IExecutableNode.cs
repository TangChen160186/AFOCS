namespace AFOCS.FlowNodeEditor.Services
{
    /// <summary>
    /// 可执行节点接口。节点定义实现此接口即可在流程中执行。
    /// </summary>
    public interface IExecutableNode
    {
        /// <summary>
        /// 执行节点逻辑。
        /// </summary>
        /// <param name="inputs">输入端口名称 -> 输入值</param>
        /// <param name="properties">属性名称 -> 属性值</param>
        /// <param name="context">流程图共享上下文（入口节点创建，沿 Execution 链传递）</param>
        /// <returns>输出端口名称 -> 输出值</returns>
        Task<Dictionary<string, object?>> ExecuteAsync(
            Dictionary<string, object?> inputs,
            Dictionary<string, object?> properties,
            Dictionary<string, object?> context);
    }
}
