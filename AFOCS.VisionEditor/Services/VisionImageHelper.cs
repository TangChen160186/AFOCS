using System.IO;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace AFOCS.VisionEditor.Services
{
    /// <summary>
    /// 图像加载辅助工具。
    /// 视觉节点优先使用连接的图像输入端口，未连接时回退到图像路径属性。
    /// </summary>
    public static class VisionImageHelper
    {
        /// <summary>
        /// 加载灰度单通道图像。
        /// </summary>
        /// <param name="input">图像输入端口的值（可为 null）</param>
        /// <param name="imagePath">图像文件路径（回退来源）</param>
        /// <returns>灰度单通道 Mat，调用方负责 Dispose</returns>
        public static Mat LoadGray(Mat? input, string imagePath)
        {
            if (input != null && !input.IsEmpty)
            {
                if (input.NumberOfChannels == 1)
                    return input.Clone();
                var converted = new Mat();
                CvInvoke.CvtColor(input, converted, ColorConversion.Bgr2Gray);
                return converted;
            }

            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                throw new InvalidOperationException("图像无效：请连接图像输入或填写有效的图像路径");

            var loaded = CvInvoke.Imread(imagePath, ImreadModes.AnyColor);
            if (loaded == null || loaded.IsEmpty)
                throw new InvalidOperationException($"图像加载失败: {imagePath}");

            if (loaded.NumberOfChannels == 1)
                return loaded;

            var gray = new Mat();
            CvInvoke.CvtColor(loaded, gray, ColorConversion.Bgr2Gray);
            loaded.Dispose();
            return gray;
        }
    }
}
