using System.ComponentModel.Composition;
using System.Windows;
using AFOCS.Framework.Framework;

namespace AFOCS.Framework.Modules.ToolBars
{
    [Export(typeof(IModule))]
    public class Module : ModuleBase
    {
        public override IEnumerable<ResourceDictionary> GlobalResourceDictionaries
        {
            get
            {
                yield return new ResourceDictionary
                {
                    Source = new Uri("/AFOCS.Framework;component/Modules/ToolBars/Resources/Styles.xaml", UriKind.Relative)
                };
            }
        }
    }
}