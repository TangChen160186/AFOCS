using System.ComponentModel.Composition;
using System.IO;
using AFOCS.VisionEditor.ViewModels;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using Caliburn.Micro;

namespace AFOCS.VisionEditor;

/// <summary>
/// 视觉编辑器 Document 的 EditorProvider，
/// 注册到 Framework 后会在"新建文件"菜单中出现"视觉模板"选项
/// </summary>
[Export(typeof(IEditorProvider))]
public class VisionEditorProvider : IEditorProvider
{
    public IEnumerable<EditorFileType> FileTypes =>
    [
        new EditorFileType("视觉模板", ".vtemplate")
    ];

    public bool CanCreateNew => true;

    public bool Handles(string path)
        => Path.GetExtension(path).Equals(".vtemplate", StringComparison.OrdinalIgnoreCase);

    public IDocument Create()
        => IoC.Get<VisionEditorDocumentViewModel>();

    public async Task New(IDocument document, string name)
    {
        var persisted = (PersistedDocument)document;
        await persisted.New(name);
    }

    public async Task Open(IDocument document, string path)
    {
        var persisted = (PersistedDocument)document;
        await persisted.Load(path);
    }
}
