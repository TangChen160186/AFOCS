using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AFOCS.Framework.Framework;
using AFOCS.VisionEditor.Models;
using AFOCS.VisionEditor.Services;
using Caliburn.Micro;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Microsoft.Win32;

namespace AFOCS.VisionEditor.ViewModels;

/// <summary>
/// 视觉验证对话框VM —— 选择新图片，执行检测，显示结果图+偏差文字
/// </summary>
public class VerifyDialogViewModel : PropertyChangedBase
{
    private readonly VisionTemplate _template;

    // ========== 图片 ==========

    private string? _verifyImagePath;
    public string? VerifyImagePath
    {
        get => _verifyImagePath;
        set => Set(ref _verifyImagePath, value);
    }

    private ImageSource? _displayImage;
    public ImageSource? DisplayImage
    {
        get => _displayImage;
        set => Set(ref _displayImage, value);
    }

    // ========== 状态 ==========

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        set => Set(ref _isBusy, value);
    }

    private bool _hasImage;
    public bool HasImage
    {
        get => _hasImage;
        set => Set(ref _hasImage, value);
    }

    private bool _hasResult;
    public bool HasResult
    {
        get => _hasResult;
        set => Set(ref _hasResult, value);
    }

    // ========== 结果文字 ==========

    public bool ShowNcc => _template.Ncc.IsEnabled;
    public bool ShowEdge1 => _template.EdgeFind1.IsEnabled;
    public bool ShowEdge2 => _template.EdgeFind2.IsEnabled;
    public bool ShowPoint => _template.PointFind.IsEnabled;

    private string _nccText = string.Empty;
    public string NccText { get => _nccText; set => Set(ref _nccText, value); }

    private string _edge1Text = string.Empty;
    public string Edge1Text { get => _edge1Text; set => Set(ref _edge1Text, value); }

    private string _edge2Text = string.Empty;
    public string Edge2Text { get => _edge2Text; set => Set(ref _edge2Text, value); }

    private string _pointText = string.Empty;
    public string PointText { get => _pointText; set => Set(ref _pointText, value); }

    private bool _nccOk;
    public bool NccOk { get => _nccOk; set => Set(ref _nccOk, value); }

    private bool _edge1Ok;
    public bool Edge1Ok { get => _edge1Ok; set => Set(ref _edge1Ok, value); }

    private bool _edge2Ok;
    public bool Edge2Ok { get => _edge2Ok; set => Set(ref _edge2Ok, value); }

    private bool _pointOk;
    public bool PointOk { get => _pointOk; set => Set(ref _pointOk, value); }

    // ========== 命令 ==========

    public ICommand SelectImageCommand { get; }
    public ICommand VerifyCommand { get; }

    public VerifyDialogViewModel(VisionTemplate template)
    {
        _template = template;
        SelectImageCommand = new RelayCommand(_ => SelectImage());
        VerifyCommand = new RelayCommand(_ => VerifyAsync(), _ => !string.IsNullOrEmpty(VerifyImagePath) && !IsBusy);
    }

    // ========== 图片选择 ==========

    private void SelectImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择验证图片",
            Filter = "图片文件|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|所有文件|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            VerifyImagePath = dialog.FileName;
            HasResult = false;
            LoadImage();
        }
    }

    private void LoadImage()
    {
        if (string.IsNullOrEmpty(VerifyImagePath) || !File.Exists(VerifyImagePath))
        {
            DisplayImage = null;
            HasImage = false;
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(VerifyImagePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            DisplayImage = bitmap;
            HasImage = true;
        }
        catch
        {
            DisplayImage = null;
            HasImage = false;
        }
    }

    // ========== 验证执行 ==========

    private async void VerifyAsync()
    {
        if (string.IsNullOrEmpty(VerifyImagePath) || !File.Exists(VerifyImagePath))
            return;

        IsBusy = true;
        HasResult = false;

        try
        {
            VisionInspectionResult? result = null;

            await Task.Run(() =>
            {
                using var grayImage = CvInvoke.Imread(VerifyImagePath, ImreadModes.Grayscale);
                if (grayImage == null || grayImage.IsEmpty)
                    return;
                using var colorImage = CvInvoke.Imread(VerifyImagePath, ImreadModes.ColorRgb);
                var service = new VisionInspectionService();
                result = service.Inspect(grayImage, colorImage, _template);
            });

            if (result != null)
            {
                ApplyResult(result);
                HasResult = true;
            }
        }
        catch
        {
            // 失败时保持原图
        }
        finally
        {
            IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private void ApplyResult(VisionInspectionResult result)
    {
        if (result.DrawMat != null)
        {
            var bmp = result.DrawMat.ToBitmapSource();
            bmp.Freeze();
            DisplayImage = bmp;
        }

        NccOk = result.NccSuccess;
        NccText = result.NccSuccess
            ? $"NCC:  ΔX={result.Dx:+0.00;-0.00}  ΔY={result.Dy:+0.00;-0.00}"
            : "NCC:  未找到匹配";

        Edge1Ok = result.Edge1Success;
        Edge1Text = result.Edge1Success
            ? $"Edge1:  角度偏差={result.Edge1AngleDev:+0.00°;-0.00°}"
            : "Edge1:  未找到有效边缘";

        Edge2Ok = result.Edge2Success;
        Edge2Text = result.Edge2Success
            ? $"Edge2:  角度偏差={result.Edge2AngleDev:+0.00°;-0.00°}"
            : "Edge2:  未找到有效边缘";

        PointOk = result.PointSuccess;
        PointText = result.PointSuccess
            ? $"Point:  ΔX={result.PointDevX:+0.00;-0.00}  ΔY={result.PointDevY:+0.00;-0.00}"
            : "Point:  未找到交点";
    }
}
