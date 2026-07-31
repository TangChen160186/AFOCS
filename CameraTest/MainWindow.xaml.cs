using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System.Windows;
using System.Windows.Media.Imaging;

namespace CameraTest
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MyImageControl_OnInitialized(object? sender, EventArgs e)
        {
            // 1. 读取源图像和模板图像
            Mat src = CvInvoke.Imread("images/inputimg.jpeg", ImreadModes.Grayscale); // 大图
            Mat template = CvInvoke.Imread("images/model.jpeg", ImreadModes.Grayscale); // 小图

            // 2. 准备结果矩阵
            // 结果矩阵的尺寸 = (源图宽 - 模板宽 + 1) x (源图高 - 模板高 + 1)
            Mat result = new Mat(src.Rows - template.Rows + 1,
                src.Cols - template.Cols + 1,
                DepthType.Cv32F, 1); // 必须是32位浮点型，单通道[reference:3]

            // 3. 执行模板匹配
            // 这里使用归一化相关系数匹配法 (CcoeffNormed)
            CvInvoke.MatchTemplate(src, template, result, TemplateMatchingType.CcoeffNormed);

            // 4. 寻找最佳匹配位置
            double minVal = 0, maxVal = 0;
            System.Drawing.Point minLoc = new System.Drawing.Point(), maxLoc = new System.Drawing.Point();
            CvInvoke.MinMaxLoc(result, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

            // 5. 在源图像上绘制结果矩形
            // 对于 CcoeffNormed 方法，值越大表示匹配度越高，所以使用 maxLoc[reference:4]
            Rectangle matchRect = new Rectangle(maxLoc, template.Size);
            CvInvoke.Rectangle(src, matchRect, new MCvScalar(0, 0, 255), 3); // 红色矩形框
            // 通过 Emgu.CV.Wpf 提供的扩展方法，直接转换为 WPF 的 BitmapSource
            var wpfImage = src.ToBitmapSource();

            // 然后就可以将 wpfImage 赋给 WPF 中 Image 控件的 Source 属性
            MyImageControl.Source = wpfImage;
        }
    }
}