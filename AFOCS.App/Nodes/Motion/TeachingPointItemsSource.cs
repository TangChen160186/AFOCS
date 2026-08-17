using AFOCS.App.Models;
using AFOCS.Infrastructure;
using AFOCS.Infrastructure.Extensions;
using Caliburn.Micro;
using Xceed.Wpf.Toolkit.PropertyGrid.Attributes;

namespace AFOCS.App.Nodes.Motion;

public class TeachingPointItemsSource : IItemsSource
{
    public ItemCollection GetValues()
    {
        var items = new ItemCollection();
        try
        {
            var config = LoadConfig();

            if (config?.Points != null)
            {
                foreach (var point in config.Points)
                {
                    items.Add(point.Id, $"{point.Name}（{point.Station.GetDescription()}）");
                }
            }
        }
        catch
        {

        }
        return items;
    }

    private static TeachingPointsConfig? LoadConfig()
    {
        var configService = IoC.Get<IConfigService>();
        return Task.Run(configService.LoadAsync<TeachingPointsConfig>).GetAwaiter().GetResult();
    }
}