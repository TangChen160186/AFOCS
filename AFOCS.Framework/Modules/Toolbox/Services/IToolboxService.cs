using AFOCS.Framework.Modules.Toolbox.Models;

namespace AFOCS.Framework.Modules.Toolbox.Services
{
    public interface IToolboxService
    {
        IEnumerable<ToolboxItem> GetToolboxItems(Type documentType);
    }
}