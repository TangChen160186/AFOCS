using System.IO;
using System.Windows.Media.Imaging;

namespace AFOCS.Framework.Framework.Services
{
	public interface IResourceManager
	{
		Stream GetStream(string relativeUri, string assemblyName);
		BitmapImage GetBitmap(string relativeUri, string assemblyName);
		BitmapImage GetBitmap(string relativeUri);
	}
}