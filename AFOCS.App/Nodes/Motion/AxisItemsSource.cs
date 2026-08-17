using AFOCS.App.Models;
using AFOCS.Infrastructure.Extensions;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Motion;

public class AxisItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        foreach (var axis in Enum.GetValues<EAxis>())
            items.Add(axis, axis.GetDescription());
        return items;
    }
}