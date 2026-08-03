using System.ComponentModel;
using System.Windows;
using AFOCS.FlowNodeEditor.Models;
using Caliburn.Micro;

namespace AFOCS.FlowNodeEditor.ViewModels;

public class NodeViewModel : PropertyChangedBase
{
    public Guid InstanceId { get; }
    public INodeDefinition Definition { get; }

    public string Title
    {
        get;
        set => Set(ref field, value);
    }

    public Point Location
    {
        get;
        set => Set(ref field, value);
    }

    public bool IsSelected
    {
        get;
        set => Set(ref field, value);
    }

    /// <summary>节点正在执行</summary>
    public bool IsExecuting
    {
        get;
        set => Set(ref field, value);
    }

    /// <summary>节点执行完成</summary>
    public bool IsCompleted
    {
        get;
        set => Set(ref field, value);
    }

    /// <summary>节点执行出错</summary>
    public bool HasError
    {
        get;
        set => Set(ref field, value);
    }

    /// <summary>重置执行状态</summary>
    public void ResetExecutionState()
    {
        IsExecuting = false;
        IsCompleted = false;
        HasError = false;
    }

    public List<ConnectorViewModel> Inputs { get; } = [];
    public List<ConnectorViewModel> Outputs { get; } = [];

    public IEnumerable<ConnectorViewModel> ExecutionInputs => 
        Inputs.Where(c => c.PortType == NodePortType.Execution);
    public IEnumerable<ConnectorViewModel> DataInputs => 
        Inputs.Where(c => c.PortType != NodePortType.Execution);
    public IEnumerable<ConnectorViewModel> ExecutionOutputs => 
        Outputs.Where(c => c.PortType == NodePortType.Execution);
    public IEnumerable<ConnectorViewModel> DataOutputs => 
        Outputs.Where(c => c.PortType != NodePortType.Execution);

    public string Description => GetDescription();

    private string GetDescription()
    {
        return Definition.GetType()
            .GetProperty("Description")?.GetValue(Definition) as string ?? string.Empty;
    }

    public bool IsEnabled => GetIsEnabled();

    private bool GetIsEnabled()
    {
        return Definition.GetType()
            .GetProperty("Enabled")?.GetValue(Definition) as bool? ?? true;
    }

    public NodeViewModel(INodeDefinition definition, Guid? instanceId = null)
    {
        Definition = definition;
        InstanceId = instanceId ?? Guid.NewGuid();
        Title = NodeDefinitionHelper.GetDisplayName(definition);

        foreach (var port in NodeDefinitionHelper.GetInputPorts(definition))
            Inputs.Add(new ConnectorViewModel(this, port, true));

        foreach (var port in NodeDefinitionHelper.GetOutputPorts(definition))
            Outputs.Add(new ConnectorViewModel(this, port, false));

        if (definition is INotifyPropertyChanged notifyDef)
        {
            notifyDef.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == "Description")
                {
                    NotifyOfPropertyChange(nameof(Description));
                }
                else if (e.PropertyName == "Enabled")
                {
                    NotifyOfPropertyChange(nameof(IsEnabled));
                }
            };
        }
    }
}