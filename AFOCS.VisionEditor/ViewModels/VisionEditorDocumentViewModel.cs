using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AFOCS.VisionEditor.Models;
using AFOCS.VisionEditor.Services;
using AFOCS.VisionEditor.Views;
using AFOCS.Framework.Framework;
using Caliburn.Micro;
using HalconDotNet;
using Microsoft.Win32;
using Action = System.Action;

namespace AFOCS.VisionEditor.ViewModels;

/// <summary>
/// 视觉编辑器 Document ViewModel —— 管理 NCC → 找边1 → 找边2 → 找点 四条固定流程。
/// Halcon HSmartWindowControlWPF 作为图像显示和 ROI 交互控件，
/// 找边使用 HDrawingObject (LINE)，NCC 使用 HDrawingObject (RECTANGLE1)。
/// </summary>
[Export]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class VisionEditorDocumentViewModel : PersistedDocument
{
    // ========== 视觉模板数据 ==========

    private VisionTemplate _template = new();

    public VisionTemplate Template
    {
        get => _template;
        private set
        {
            _template = value;
            NotifyOfPropertyChange();
            RefreshAllBindings();
        }
    }

    public NccConfig Ncc => Template.Ncc;
    public EdgeFindConfig EdgeFind1 => Template.EdgeFind1;
    public EdgeFindConfig EdgeFind2 => Template.EdgeFind2;
    public PointFindConfig PointFind => Template.PointFind;

    // ========== Halcon 窗口 ==========

    private HSmartWindowControlWPF? _halconControl;
    private HWindow? _halconWindow;
    private HImage? _halconImage;
    private HDrawingObject? _currentHObject;
    private HDrawingObject.HDrawingObjectCallback? _activeCallback; // 防 GC 回收

    public void SetHalconControl(HSmartWindowControlWPF control)
    {
        _halconControl = control;
        _halconWindow = control.HalconWindow;

        // 启用内容移动（图片拖拽平移）
        control.HMoveContent = true;

        // 鼠标松开时同步属性到 PropertyGrid
        control.HMouseUp += (_, _) =>
        {
            if (SelectedProcessType == VisionProcessType.Ncc)
                Ncc.NotifyDragEnd();
            else if (SelectedProcessType == VisionProcessType.EdgeFind1)
                EdgeFind1.NotifyDragEnd();
            else if (SelectedProcessType == VisionProcessType.EdgeFind2)
                EdgeFind2.NotifyDragEnd();
        };

        // 如果已有图片路径，立即显示
        if (!string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath))
            DisplayImageOnHalcon(ImagePath);
    }

    // ========== 流程列表 ==========

    public List<ProcessItem> Processes { get; }

    public class ProcessItem : PropertyChangedBase
    {
        public VisionProcessType Type { get; init; }
        public string DisplayName { get; init; } = string.Empty;

        public bool IsEnabled
        {
            get;
            set
            {
                if (Set(ref field, value))
                    OnEnabledChanged?.Invoke();
            }
        }

        public Action? OnEnabledChanged { get; set; }
    }

    // ========== 当前选中流程 ==========

    private ProcessItem? _selectedProcess;
    public ProcessItem? SelectedProcess
    {
        get => _selectedProcess;
        set
        {
            if (Set(ref _selectedProcess, value))
            {
                NotifyOfPropertyChange(nameof(IsProcessSelected));
                NotifyOfPropertyChange(nameof(SelectedProcessType));
                SyncSelectedConfigObject();
                SyncDrawingObject();
            }
        }
    }

    public bool IsProcessSelected => SelectedProcess != null;
    public VisionProcessType? SelectedProcessType => SelectedProcess?.Type;

    private object? _selectedConfigObject;
    public object? SelectedConfigObject
    {
        get => _selectedConfigObject;
        private set
        {
            if (_selectedConfigObject == value) return;

            if (_selectedConfigObject is INotifyPropertyChanged oldNpc)
                oldNpc.PropertyChanged -= OnConfigPropertyChanged;

            if (value is INotifyPropertyChanged newNpc)
                newNpc.PropertyChanged += OnConfigPropertyChanged;

            _selectedConfigObject = value;
            NotifyOfPropertyChange();
        }
    }

    private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // PropertyGrid 修改了参数 → 更新 Halcon 窗口上的 DrawingObject
        SyncDrawingObject();
    }

    private void SyncSelectedConfigObject()
    {
        SelectedConfigObject = SelectedProcessType switch
        {
            VisionProcessType.Ncc => Ncc,
            VisionProcessType.EdgeFind1 => EdgeFind1,
            VisionProcessType.EdgeFind2 => EdgeFind2,
            VisionProcessType.PointFind => PointFind,
            _ => null
        };
    }

    private void RefreshPropertyGrid()
    {
        var current = SelectedConfigObject;
        SelectedConfigObject = null;
        SelectedConfigObject = current;
    }

    // ========== NCC 专用：切换编辑 SearchRoi / TemplateRoi ==========

    [Browsable(false)]
    public bool IsEditingTemplateRoi { get; set; } // 保留字段，不再使用搜索/模板切换

    // ========== 图片 ==========

    private string? _imagePath;
    public string? ImagePath
    {
        get => _imagePath;
        set
        {
            if (Set(ref _imagePath, value))
            {
                Template.ImagePath = value ?? string.Empty;
                DisplayImageOnHalcon(value);
                IsDirty = true;
            }
        }
    }

    private ImageSource? _displayImage;
    public ImageSource? DisplayImage
    {
        get => _displayImage;
        set => Set(ref _displayImage, value);
    }

    // ========== 执行状态 ==========

    public string ExecutionStatus
    {
        get;
        set => Set(ref field, value);
    } = "就绪";

    public bool HasExecutionError
    {
        get;
        set => Set(ref field, value);
    }

    public bool IsBusy
    {
        get;
        set => Set(ref field, value);
    }

    // ========== 命令 ==========

    public ICommand ExecuteCommand { get; }
    public ICommand SelectImageCommand { get; }
    public ICommand VerifyCommand { get; }

    [ImportingConstructor]
    public VisionEditorDocumentViewModel()
    {
        Processes =
        [
            new ProcessItem
            {
                Type = VisionProcessType.Ncc,
                DisplayName = "NCC 模板匹配",
                OnEnabledChanged = () => { Template.Ncc.IsEnabled = Processes[0].IsEnabled; IsDirty = true; }
            },
            new ProcessItem
            {
                Type = VisionProcessType.EdgeFind1,
                DisplayName = "找边 1",
                OnEnabledChanged = () => { Template.EdgeFind1.IsEnabled = Processes[1].IsEnabled; IsDirty = true; }
            },
            new ProcessItem
            {
                Type = VisionProcessType.EdgeFind2,
                DisplayName = "找边 2",
                OnEnabledChanged = () => { Template.EdgeFind2.IsEnabled = Processes[2].IsEnabled; IsDirty = true; }
            },
            new ProcessItem
            {
                Type = VisionProcessType.PointFind,
                DisplayName = "找点（交点）",
                OnEnabledChanged = () => { Template.PointFind.IsEnabled = Processes[3].IsEnabled; IsDirty = true; }
            },
        ];

        ExecuteCommand = new RelayCommand(_ => ExecuteAsync());
        SelectImageCommand = new RelayCommand(_ => SelectImage());
        VerifyCommand = new RelayCommand(_ => Verify());
    }

    // ========== Halcon 图像显示 ==========

    private void DisplayImageOnHalcon(string? path)
    {
        if (_halconWindow == null) return;
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

        _halconImage?.Dispose();
        _halconImage = new HImage(path);
        _halconImage.GetImageSize(out int width, out int height);

        _halconWindow.SetPart(0, 0, height - 1, width - 1);
        _halconWindow.DispObj(_halconImage);
        _halconWindow.SetLineWidth(1);

        // 自适应缩放
        _halconControl?.SetFullImagePart();

        // 刷新 DrawingObject
        SyncDrawingObject();

        // 同时设置 DisplayImage 用于 WPF 绑定（如果需要）
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            DisplayImage = bitmap;
        }
        catch { }
    }

    // ========== HDrawingObject 管理 ==========

    private void SyncDrawingObject(bool redrawImage = true)
    {
        if (_halconWindow == null || _halconImage == null) return;

        // 移除旧 DrawingObject
        if (_currentHObject != null)
        {
            _currentHObject.Dispose();
            _currentHObject = null;
        }

        // 重绘底图（默认行为；执行结果绘制时跳过以免清除结果）
        if (redrawImage)
            _halconWindow.DispObj(_halconImage);

        // 根据当前选中的流程创建对应的 DrawingObject
        switch (SelectedProcessType)
        {
            case VisionProcessType.Ncc:
                SyncNccDrawingObject();
                break;
            case VisionProcessType.EdgeFind1:
            case VisionProcessType.EdgeFind2:
                SyncEdgeDrawingObject(SelectedProcessType == VisionProcessType.EdgeFind1 ? EdgeFind1 : EdgeFind2);
                break;
        }
    }

    private void SyncNccDrawingObject()
    {
        if (_halconWindow == null) return;

        _currentHObject = HDrawingObject.CreateDrawingObject(
            HDrawingObject.HDrawingObjectType.RECTANGLE2,
            new HTuple[] { Ncc.Row, Ncc.Column, Ncc.Phi, Ncc.Length1, Ncc.Length2 });

        _activeCallback = OnNccDrawingObjectChanged;
        _currentHObject.OnDrag(_activeCallback);
        _currentHObject.OnResize(_activeCallback);
        _halconWindow.AttachDrawingObjectToWindow(_currentHObject);
    }

    private void OnNccDrawingObjectChanged(IntPtr drawid, IntPtr windowHandle, string type)
    {
        if (_currentHObject == null) return;
        var param = _currentHObject.GetDrawingObjectParams(
            new HTuple("row", "column", "phi", "length1", "length2"));
        double[] vals = param.ToDArr();
        param.Dispose();

        // 拖拽中：直设字段，不触发 PropertyChanged，避免 PropertyGrid 刷新打断 Halcon 鼠标捕获
        Ncc.UpdateFromDrag(vals[0], vals[1], vals[2], vals[3], vals[4]);
        IsDirty = true;
    }

    private void SyncEdgeDrawingObject(EdgeFindConfig cfg)
    {
        if (_halconWindow == null) return;

        _currentHObject = HDrawingObject.CreateDrawingObject(
            HDrawingObject.HDrawingObjectType.LINE,
            new HTuple[] { cfg.Row1, cfg.Col1, cfg.Row2, cfg.Col2 });

        _activeCallback = OnEdgeDrawingObjectChanged;
        _currentHObject.OnDrag(_activeCallback);
        _currentHObject.OnResize(_activeCallback);
        _halconWindow.AttachDrawingObjectToWindow(_currentHObject);
    }

    private void OnEdgeDrawingObjectChanged(IntPtr drawid, IntPtr windowHandle, string type)
    {
        if (SelectedProcessType == null || _currentHObject == null) return;

        var cfg = SelectedProcessType == VisionProcessType.EdgeFind1 ? EdgeFind1 : EdgeFind2;

        var param = _currentHObject.GetDrawingObjectParams(new HTuple("row1", "column1", "row2", "column2"));
        double[] vals = param.ToDArr();
        param.Dispose();

        // 拖拽中：直设字段，不触发 PropertyChanged，避免 PropertyGrid 刷新打断 Halcon 鼠标捕获
        cfg.UpdateFromDrag(vals[0], vals[1], vals[2], vals[3]);
        IsDirty = true;
    }

    // ========== 持久化 ==========

    protected override Task DoNew()
    {
        Template = new VisionTemplate();
        SyncProcessStatesFromModel();
        return Task.CompletedTask;
    }

    protected override async Task DoLoad(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        var template = JsonSerializer.Deserialize<VisionTemplate>(json) ?? new VisionTemplate();
        Template = template;
        SyncProcessStatesFromModel();

        if (!string.IsNullOrEmpty(template.ImagePath))
        {
            var imgPath = template.ImagePath;
            if (!Path.IsPathRooted(imgPath))
                imgPath = Path.Combine(Path.GetDirectoryName(filePath)!, imgPath);
            if (File.Exists(imgPath))
            {
                _imagePath = imgPath;
                DisplayImageOnHalcon(imgPath);
            }
        }
    }

    protected override async Task DoSave(string filePath)
    {
        var json = JsonSerializer.Serialize(Template, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(filePath, json);
    }

    // ========== 图片操作 ==========

    private void SelectImage()
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择训练图片",
            Filter = "图片文件|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff|所有文件|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            ImagePath = dialog.FileName;
        }
    }

    // ========== 执行流程 → 结果刷新 ==========

    private void SyncProcessStatesFromModel()
    {
        Processes[0].IsEnabled = Ncc.IsEnabled;
        Processes[1].IsEnabled = EdgeFind1.IsEnabled;
        Processes[2].IsEnabled = EdgeFind2.IsEnabled;
        Processes[3].IsEnabled = PointFind.IsEnabled;
    }

    private void RefreshAllBindings()
    {
        SyncProcessStatesFromModel();
        NotifyOfPropertyChange(nameof(Ncc));
        NotifyOfPropertyChange(nameof(EdgeFind1));
        NotifyOfPropertyChange(nameof(EdgeFind2));
        NotifyOfPropertyChange(nameof(PointFind));
        SyncSelectedConfigObject();
    }

    // ========== 执行 ==========

    public async Task ExecuteAsync()
    {
        if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
        {
            ExecutionStatus = "请先选择训练图片";
            HasExecutionError = true;
            return;
        }

        IsBusy = true;
        HasExecutionError = false;
        ExecutionStatus = "执行中...";

        // 执行前移除 DrawingObject
        DetachDrawingObject();

        try
        {
            bool success = false;

            await Task.Run(() =>
            {
                var service = new VisionExecutionService();
                success = service.Execute(ImagePath, Template, (msg, ok) =>
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        ExecutionStatus = msg;
                        HasExecutionError = !ok;
                    });
                });
            });

            // 执行完成后在 Halcon 窗口显示底图 → 绘制结果 → 挂载编辑工具
            if (_halconWindow != null && _halconImage != null)
            {
                _halconWindow.DispObj(_halconImage);
                _halconWindow.SetLineWidth(2);
                DrawResultsOnHalcon();
                SyncDrawingObject(redrawImage: false);
                _halconControl?.SetFullImagePart();
            }

            if (!success)
            {
                ExecutionStatus = "执行完成（部分流程失败）";
                HasExecutionError = true;
            }

            RefreshPropertyGrid();
            NotifyOfPropertyChange(nameof(Template));
            IsDirty = true;
        }
        catch (Exception ex)
        {
            ExecutionStatus = $"执行失败: {ex.Message}";
            HasExecutionError = true;
        }
        finally
        {
            IsBusy = false;
            // 恢复 DrawingObject（不重绘底图，避免清除执行结果）
            SyncDrawingObject(redrawImage: false);
        }
    }

    private void DetachDrawingObject()
    {
        if (_currentHObject != null && _halconWindow != null)
        {
            _halconWindow.DetachDrawingObjectFromWindow(_currentHObject);
            _currentHObject.Dispose();
            _currentHObject = null;
        }
    }

    /// <summary>在 Halcon 窗口绘制执行结果</summary>
    private void DrawResultsOnHalcon()
    {
        if (_halconWindow == null || _halconImage == null) return;

        _halconWindow.SetLineWidth(2);

        // NCC 结果：绘制匹配到的模板轮廓
        if (Ncc.IsEnabled && Ncc.ResultScore > 0)
        {
            try
            {
                if (File.Exists(Ncc.ModelPath))
                {
                    HOperatorSet.ReadShapeModel(Ncc.ModelPath, out HTuple modelId);
                    HOperatorSet.GetShapeModelContours(out HObject contours, modelId, 1);

                    // 变换到匹配位置
                    HOperatorSet.VectorAngleToRigid(0, 0, 0,
                        Ncc.ResultY, Ncc.ResultX,
                        Ncc.ResultAngle * Math.PI / 180.0,
                        out HTuple homMat);
                    HOperatorSet.AffineTransContourXld(contours, out HObject transContours, homMat);

                    _halconWindow.SetColor("green");
                    _halconWindow.DispObj(transContours);
                }
                else
                {
                    // fallback: 十字
                    _halconWindow.SetColor("green");
                    _halconWindow.DispCross(Ncc.ResultY, Ncc.ResultX, 50, Ncc.ResultAngle);
                }
            }
            catch
            {
                _halconWindow.SetColor("green");
                _halconWindow.DispCross(Ncc.ResultY, Ncc.ResultX, 50, Ncc.ResultAngle);
            }
        }

        // 找边1 结果：红色线段
        if (EdgeFind1.IsEnabled)
        {
            _halconWindow.SetColor("red");
            _halconWindow.DispLine(
                EdgeFind1.ResultStartY, EdgeFind1.ResultStartX,
                EdgeFind1.ResultEndY, EdgeFind1.ResultEndX);
        }

        // 找边2 结果：蓝色线段
        if (EdgeFind2.IsEnabled)
        {
            _halconWindow.SetColor("blue");
            _halconWindow.DispLine(
                EdgeFind2.ResultStartY, EdgeFind2.ResultStartX,
                EdgeFind2.ResultEndY, EdgeFind2.ResultEndX);
        }

        // 找点 结果：品红色圆
        if (PointFind.IsEnabled && (PointFind.ResultX != 0 || PointFind.ResultY != 0))
        {
            _halconWindow.SetColor("magenta");
            _halconWindow.DispCircle(PointFind.ResultY, PointFind.ResultX, 10);
            _halconWindow.DispCross(PointFind.ResultY, PointFind.ResultX, 12, 0);
        }
    }

    // ========== 验证 ==========

    private void Verify()
    {
        var vm = new VerifyDialogViewModel(Template);
        var dialog = new VerifyDialog(vm);
        dialog.ShowDialog();
    }
}
