using AFOCS.Infrastructure.Extensions;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Flow;

public class ComparisonOperatorItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        foreach (var op in Enum.GetValues<ComparisonOperator>())
            items.Add(op, op.GetDescription());
        return items;
    }
}