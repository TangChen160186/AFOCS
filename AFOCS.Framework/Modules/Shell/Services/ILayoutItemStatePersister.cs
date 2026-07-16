using AFOCS.Framework.Framework.Services;
using AFOCS.Framework.Modules.Shell.Views;

namespace AFOCS.Framework.Modules.Shell.Services
{
    public interface ILayoutItemStatePersister
    {
        bool SaveState(IShell shell, IShellView shellView, string fileName);
        bool LoadState(IShell shell, IShellView shellView, string fileName);
    }
}