using AFOCS.FlowNodeEditor.ViewModels;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using Caliburn.Micro;
using System.ComponentModel.Composition;

namespace AFOCS.FlowNodeEditor;

/// <summary>
/// 节点编辑器 Document 的 EditorProvider，
/// 注册到 Framework 后会在"新建文件"菜单中出现"流程图"选项
/// </summary>
[Export(typeof(IEditorProvider))]
public class NodeEditorProvider : IEditorProvider
{
    public IEnumerable<EditorFileType> FileTypes =>
    [
        new EditorFileType("流程图", ".nflow")
    ];

    public bool CanCreateNew => true;

    public bool Handles(string path)
        => System.IO.Path.GetExtension(path).Equals(".nflow", StringComparison.OrdinalIgnoreCase);

    public IDocument Create()
        => IoC.Get<NodeEditorDocumentViewModel>();

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