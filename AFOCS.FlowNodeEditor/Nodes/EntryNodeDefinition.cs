using AFOCS.FlowNodeEditor.Models;
using AFOCS.FlowNodeEditor.Services;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using System.ComponentModel;
using System.ComponentModel.Composition;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.FlowNodeEditor.Nodes;


[NodeDefinition("Builtin.Entry", "入口", "流程", HasExecutionInput = false, HasExecutionOutput = true)]
[Export(typeof(INodeDefinition))]
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class EntryNodeDefinition : NodeDefinitionBase, IExecutableNode
{
    [DisplayName("优先级")]
    public int Priority
    {
        get;
        set => Set(ref field, value);
    }
    [DisplayName("工位")]
    [ItemsSource(typeof(WorkPosItemsSource))]
    public WorkPos Workpos
    {
        get;
        set => Set(ref field, value);
    }

    public Task<Dictionary<string, object?>> ExecuteAsync(Dictionary<string, object?> context)
    {
        var result = new Dictionary<string, object?>
        {
            ["WorkPos"] = Workpos,
        };
        return Task.FromResult(result);
    }


    public class WorkPosItemsSource : IItemsSource
    {
        public ItemCollection GetValues()
        {
            var items = new ItemCollection();
            foreach (var pos in Enum.GetValues<WorkPos>())
                items.Add(pos, pos.GetDescription());
            return items;
        }
    }

}