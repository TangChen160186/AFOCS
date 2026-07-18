using System.Windows.Media.Imaging;
using AFOCS.Framework.Framework.Results;
using Caliburn.Micro;
using Microsoft.Win32;

namespace AFOCS.Framework.Inspector.Inspectors
{
    public class BitmapSourceEditorViewModel : EditorBase<BitmapSource>, ILabelledInspector
    {
        public IEnumerable<IResult> Choose()
        {
            var fileDialog = new OpenFileDialog();
            yield return Show.CommonDialog(fileDialog);
            
            using (var stream = fileDialog.OpenFile())
            {
                var result = new BitmapImage();
                result.BeginInit();
                result.CacheOption = BitmapCacheOption.OnLoad;
                result.StreamSource = stream;
                result.EndInit();
                result.Freeze();

                Value = result;
            }
        }
    }
}