using System.ComponentModel;
using System.ComponentModel.Composition;
using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using Serilog;

namespace AFOCS.App.Nodes.Flow;

/// <summary>
/// 条件判断节点：读取输入端口 Bool 值，选择执行"真"或"假"分支。
/// 执行结果以 FlowExecutor.BranchResultKey（_branch）返回，由执行引擎决定跟随哪个执行输出端口。
/// 条件来源（如 IO 状态）由其它数据节点提供，本节点不包含任何设备逻辑。
/// </summary>
[Export]
[Export(typeof(INodeDefinition))]
[PartCreationPolicy(CreationPolicy.NonShared)]
[NodeDefinition("App.If", "条件判断", "流程", HasExecutionOutput = false)]
[method: ImportingConstructor]
public class IfNodeDefinition(ILogger logger) : NodeDefinitionBase, IExecutableNode
{
    [Browsable(false)]
    [NodePort("Condition", "条件", NodePortType.Bool, true)]
    public bool Condition
    {
        get;
        set => Set(ref field, value);
    }

    [Browsable(false)]
    [NodePort(FlowExecutor.TrueBranchPortName, "真", NodePortType.Execution, false)]
    public bool TruePort
    {
        get;
        set => Set(ref field, value);
    }

    [Browsable(false)]
    [NodePort(FlowExecutor.FalseBranchPortName, "假", NodePortType.Execution, false)]
    public bool FalsePort
    {
        get;
        set => Set(ref field, value);
    }

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        logger.Information("条件判断: 条件={Condition}，走{分支}分支",
            Condition, Condition ? "真" : "假");

        return Task.FromResult(new Dictionary<string, object?>
        {
            [FlowExecutor.BranchResultKey] = Condition,
        });
    }
}
