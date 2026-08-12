using System;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AFOCS.Framework.Framework;
using AFOCS.VisionEditor.Models;
using AFOCS.VisionEditor.Services;
using Caliburn.Micro;
using HalconDotNet;
using Microsoft.Win32;

namespace AFOCS.VisionEditor.ViewModels;

/// <summary>
/// 视觉验证对话框VM —— 选择新图片，执行检测，在 Halcon 窗口上显示结果
/// </summary>
public class VerifyDialogViewModel : PropertyChangedBase
{
    private readonly VisionTemplate _template;

    // ---- Halcon ----

    private HSmartWindowControlWPF? _halconControl;
    private HWindow? _halconWindow;
    private HImage? _hImage;

    // ---- 状态 ----

    public bool IsBusy
    {
        get;
        set => Set(ref field, value);
    }

    public string? VerifyImagePath
    {
        get;
        set => Set(ref field, value);
    }

    public bool HasImage
    {
        get;
        set => Set(ref field, value);
    }

    public bool HasResult
    {
        get;
        set => Set(ref field, value);
    }

    // ---- 结果文字 ----

    public bool ShowNcc => _template.Ncc.IsEnabled;
    public bool ShowEdge1 => _template.EdgeFind1.IsEnabled;
    public bool ShowEdge2 => _template.EdgeFind2.IsEnabled;
    public bool ShowPoint => _template.PointFind.IsEnabled;

    public string NccText
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    public string Edge1Text
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    public string Edge2Text
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    public string PointText
    {
        get;
        set => Set(ref field, value);
    } = string.Empty;

    public bool NccOk
    {
        get;
        set => Set(ref field, value);
    }

    public bool Edge1Ok
    {
        get;
        set => Set(ref field, value);
    }

    public bool Edge2Ok
    {
        get;
        set => Set(ref field, value);
    }

    public bool PointOk
    {
        get;
        set => Set(ref field, value);
    }

    // ---- 命令 ----

    public ICommand SelectImageCommand { get; }
    public ICommand VerifyCommand { get; }

    public VerifyDialogViewModel(VisionTemplate template)
    {
        _template = template;
        SelectImageCommand = new RelayCommand(_ => SelectImage());
        VerifyCommand = new RelayCommand(_ => VerifyAsync(), _ => !string.IsNullOrEmpty(VerifyImagePath) && !IsBusy);
    }

    public void SetHalconControl(HSmartWindowControlWPF control)
    {
        _halconControl = control;
        _halconWindow = control.HalconWindow;
    }

    // ==================== 图片选择 ====================

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
            _hImage?.Dispose();
            _hImage = null;
            HasImage = false;
            return;
        }

        try
        {
            _hImage?.Dispose();
            _hImage = new HImage(VerifyImagePath);

            if (_halconWindow != null)
            {
                _halconWindow.DispObj(_hImage);
                _halconControl?.SetFullImagePart();
            }

            HasImage = true;
        }
        catch
        {
            _hImage?.Dispose();
            _hImage = null;
            HasImage = false;
        }
    }

    // ==================== 验证执行 ====================

    private async void VerifyAsync()
    {
        if (string.IsNullOrEmpty(VerifyImagePath) || !File.Exists(VerifyImagePath))
            return;
        if (_hImage == null || _halconWindow == null)
            return;

        IsBusy = true;
        HasResult = false;

        try
        {
            VisionInspectionResult? result = null;

            await Task.Run(() =>
            {
                var service = new VisionInspectionService();
                result = service.Inspect(_hImage, _template);
            });

            if (result != null)
            {
                // 清除旧结果，显示底图
                _halconWindow.DispObj(_hImage);

                // 绘制检测结果
                DrawVerifyResults(result);

                _halconControl?.SetFullImagePart();

                ApplyResult(result);
                HasResult = true;
            }
        }
        catch
        {
            // 失败时至少显示原图
            if (_hImage != null && _halconWindow != null)
            {
                _halconWindow.DispObj(_hImage);
                _halconControl?.SetFullImagePart();
            }
        }
        finally
        {
            IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    // ==================== 绘制结果 ====================

    private void DrawVerifyResults(VisionInspectionResult result)
    {
        if (_halconWindow == null) return;

        if (result.NccSuccess && _template.Ncc.IsEnabled)
        {
            DrawNccResult(result);
        }

        if (result.Edge1Success && _template.EdgeFind1.IsEnabled)
        {
            DrawEdgeResult(result.Edge1ResultStartX, result.Edge1ResultStartY,
                           result.Edge1ResultEndX, result.Edge1ResultEndY,
                           "green");
        }

        if (result.Edge2Success && _template.EdgeFind2.IsEnabled)
        {
            DrawEdgeResult(result.Edge2ResultStartX, result.Edge2ResultStartY,
                           result.Edge2ResultEndX, result.Edge2ResultEndY,
                           "yellow");
        }

        if (result.PointSuccess && _template.PointFind.IsEnabled)
        {
            DrawPointResult(result.PointResultX, result.PointResultY);
        }
    }

    private void DrawNccResult(VisionInspectionResult result)
    {
        if (_halconWindow == null) return;

        try
        {
            HOperatorSet.ReadShapeModel(_template.Ncc.ModelPath, out HTuple modelId);

            HOperatorSet.GetShapeModelContours(out HObject ho_ModelContours, modelId, 1);

            HOperatorSet.VectorAngleToRigid(
                0, 0, 0,
                result.NccResultRow, result.NccResultColumn, result.NccResultAngle * Math.PI / 180.0,
                out HTuple hv_HomMat2D);

            HOperatorSet.AffineTransContourXld(ho_ModelContours,
                out HObject ho_TransContours, hv_HomMat2D);

            _halconWindow.SetColor("red");
            _halconWindow.SetLineWidth(2);
            _halconWindow.DispObj(ho_TransContours);

            ho_ModelContours.Dispose();
            ho_TransContours.Dispose();
            modelId.Dispose();
        }
        catch { }
    }

    private void DrawEdgeResult(
        double startX, double startY, double endX, double endY, string color)
    {
        if (_halconWindow == null) return;

        _halconWindow.SetColor(color);
        _halconWindow.SetLineWidth(2);
        _halconWindow.DispLine(startY, startX, endY, endX);
    }

    private void DrawPointResult(double x, double y)
    {
        if (_halconWindow == null) return;

        _halconWindow.SetColor("red");
        _halconWindow.SetLineWidth(1);
        int crossSize = 10;
        _halconWindow.DispLine(y - crossSize, x, y + crossSize, x);
        _halconWindow.DispLine(y, x - crossSize, y, x + crossSize);
    }

    // ==================== 结果文字 ====================

    private void ApplyResult(VisionInspectionResult result)
    {
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
