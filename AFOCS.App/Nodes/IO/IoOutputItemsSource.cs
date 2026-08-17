using AFOCS.Devices.IO;
using AFOCS.Infrastructure.Extensions;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.IO;

public class IoOutputItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        foreach (var signal in Enum.GetValues<AllOutputs>())
            items.Add(signal, signal.GetDescription());
        return items;
    }
}