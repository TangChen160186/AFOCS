using System.Windows.Media.Effects;

namespace AFOCS.Framework.Framework.ShaderEffects
{
    internal static class ShaderEffectUtility
    {
        public static PixelShader GetPixelShader(string name)
        {
            return new PixelShader
            {
                UriSource = new Uri(@"pack://application:,,,/AFOCS.Framework;component/Framework/ShaderEffects/" + name + ".ps")
            };
        }
    }
}