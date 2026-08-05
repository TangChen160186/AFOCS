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
using AFOCS.Framework.Framework;
using Caliburn.Micro;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Microsoft.Win32;
using Action = System.Action;

namespace AFOCS.VisionEditor.ViewModels;

/// <summary>
/// 视觉编辑器 Document ViewModel —— 管理 NCC → 找边1 → 找边2 → 找点 四条固定流程
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
                SyncRoiToEditor();
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

            // 取消订阅旧对象
            if (_selectedConfigObject is INotifyPropertyChanged oldNpc)
                oldNpc.PropertyChanged -= OnConfigPropertyChanged;

            // 订阅新对象
            if (value is INotifyPropertyChanged newNpc)
                newNpc.PropertyChanged += OnConfigPropertyChanged;

            _selectedConfigObject = value;
            NotifyOfPropertyChange();
        }
    }

    private void OnConfigPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // PropertyGrid 修改了 ROI 值 → 同步图像编辑器
        SyncRoiToEditor();
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

    /// <summary>ROI 变更后强制 PropertyGrid 刷新（null → obj 触发重绑）</summary>
    private void RefreshPropertyGrid()
    {
        var current = SelectedConfigObject;
        SelectedConfigObject = null;
        SelectedConfigObject = current;
    }

    // ========== NCC 专用：切换编辑 SearchRoi / TemplateRoi ==========

    private bool _isEditingTemplateRoi;
    public bool IsEditingTemplateRoi
    {
        get => _isEditingTemplateRoi;
        set
        {
            if (Set(ref _isEditingTemplateRoi, value))
                SyncRoiToEditor();
        }
    }

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
                LoadImage();
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

    private ImageSource? _resultImageSource;
    public ImageSource? ResultImageSource
    {
        get => _resultImageSource;
        set => Set(ref _resultImageSource, value);
    }

    // ========== RoiImageEditor 绑定 ==========

    /// <summary>直读当前 ROI 数据，双向绑定</summary>
    public Rect EditorRoiRect
    {
        get
        {
            var roi = GetCurrentRoi();
            return roi != null ? new Rect(roi.X, roi.Y, roi.Width, roi.Height) : Rect.Empty;
        }
        set
        {
            var roi = GetCurrentRoi();
            if (roi == null) return;
            roi.X = value.X;
            roi.Y = value.Y;
            roi.Width = value.Width;
            roi.Height = value.Height;
            RefreshPropertyGrid();
            IsDirty = true;
        }
    }

    /// <summary>直读当前 ROI 角度，双向绑定</summary>
    public double EditorRoiAngle
    {
        get => GetCurrentRoi()?.Angle ?? 0;
        set
        {
            var roi = GetCurrentRoi();
            if (roi == null) return;
            roi.Angle = value;
            RefreshPropertyGrid();
            IsDirty = true;
        }
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
                LoadImage();
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

    private void LoadImage()
    {
        if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
        {
            DisplayImage = null;
            return;
        }

        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(ImagePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            DisplayImage = bitmap;
        }
        catch
        {
            DisplayImage = null;
        }
    }

    // ========== ROI 同步 ==========

    private void SyncRoiToEditor()
    {
        NotifyOfPropertyChange(nameof(EditorRoiRect));
        NotifyOfPropertyChange(nameof(EditorRoiAngle));
    }

    private RoiData? GetCurrentRoi() => SelectedProcessType switch
    {
        VisionProcessType.Ncc => IsEditingTemplateRoi ? Ncc.TemplateRoi : Ncc.SearchRoi,
        VisionProcessType.EdgeFind1 => EdgeFind1.SearchRoi,
        VisionProcessType.EdgeFind2 => EdgeFind2.SearchRoi,
        _ => null
    };

    // ========== 模型 ↔ UI 同步 ==========

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

    // ========== 结果绘图 ==========

    private void DrawResultsOnImage()
    {
        if (string.IsNullOrEmpty(ImagePath) || !File.Exists(ImagePath))
        {
            ResultImageSource = null;
            return;
        }

        try
        {
            using var image = CvInvoke.Imread(ImagePath, ImreadModes.ColorRgb);
            if (image == null || image.IsEmpty)
            {
                ResultImageSource = null;
                return;
            }

            // NCC：画绿色矩形框 + 中心十字
            if (Ncc.IsEnabled && Ncc.ResultScore > 0)
            {
                var tw = Ncc.TemplateRoi.Width;
                var th = Ncc.TemplateRoi.Height;
                var cx = Ncc.ResultX;
                var cy = Ncc.ResultY;
                var angle = Ncc.ResultAngle;

                // 匹配框（绿色）
                var rect = new RotatedRect(
                    new System.Drawing.PointF((float)cx, (float)cy),
                    new System.Drawing.SizeF((float)tw, (float)th),
                    (float)angle);
                var pts = rect.GetVertices();
                for (int i = 0; i < 4; i++)
                    CvInvoke.Line(image, new((int)pts[i].X, (int)pts[i].Y), new((int)pts[(i + 1) % 4].X, (int)pts[(i + 1) % 4].Y), new Bgr(0, 255, 0).MCvScalar, 2);

                // 中心十字
                DrawCross(image, cx, cy, 15, new Bgr(0, 255, 0).MCvScalar);
            }

            // 找边1：画蓝色线段
            if (EdgeFind1.IsEnabled && (EdgeFind1.ResultStartX != 0 || EdgeFind1.ResultStartY != 0))
            {
                var p1 = new System.Drawing.Point((int)EdgeFind1.ResultStartX, (int)EdgeFind1.ResultStartY);
                var p2 = new System.Drawing.Point((int)EdgeFind1.ResultEndX, (int)EdgeFind1.ResultEndY);
                CvInvoke.Line(image, p1, p2, new Bgr(255, 0, 0).MCvScalar, 2);
            }

            // 找边2：画蓝色线段
            if (EdgeFind2.IsEnabled && (EdgeFind2.ResultStartX != 0 || EdgeFind2.ResultStartY != 0))
            {
                var p1 = new System.Drawing.Point((int)EdgeFind2.ResultStartX, (int)EdgeFind2.ResultStartY);
                var p2 = new System.Drawing.Point((int)EdgeFind2.ResultEndX, (int)EdgeFind2.ResultEndY);
                CvInvoke.Line(image, p1, p2, new Bgr(255, 0, 0).MCvScalar, 2);
            }

            // 找点：画红色圆点 + 十字
            if (PointFind.IsEnabled && (PointFind.ResultX != 0 || PointFind.ResultY != 0))
            {
                var pt = new System.Drawing.Point((int)PointFind.ResultX, (int)PointFind.ResultY);
                CvInvoke.Circle(image, pt, 6, new Bgr(0, 0, 255).MCvScalar, -1);
                DrawCross(image, PointFind.ResultX, PointFind.ResultY, 12, new Bgr(0, 0, 255).MCvScalar);
            }

            ResultImageSource = image.ToBitmapSource();
        }
        catch
        {
            ResultImageSource = null;
        }
    }

    private static void DrawCross(Mat image, double cx, double cy, int size, Emgu.CV.Structure.MCvScalar color)
    {
        var x = (int)cx;
        var y = (int)cy;
        CvInvoke.Line(image, new(x - size, y), new(x + size, y), color, 2);
        CvInvoke.Line(image, new(x, y - size), new(x, y + size), color, 2);
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

        try
        {
            await Task.Run(() =>
            {
                var service = new VisionExecutionService();
                service.Execute(ImagePath, Template, (msg, success) =>
                {
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        ExecutionStatus = msg;
                        HasExecutionError = !success;
                    });
                });
            });

            RefreshPropertyGrid();
            DrawResultsOnImage();
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
        }
    }
}
