using AFOCS.Devices.Gripper;
using AFOCS.Infrastructure.Extensions;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Gripper;

public class GripperCouplingItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        foreach (var coupling in Enum.GetValues<GripperType>())
            items.Add(coupling, coupling.GetDescription());
        return items;
    }
}