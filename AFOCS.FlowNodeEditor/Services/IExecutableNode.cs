namespace AFOCS.FlowNodeEditor.Services;

public interface IExecutableNode
{
    Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context);
}