using System.ComponentModel.Composition;
using AFOCS.Framework.Framework;
using AFOCS.Framework.Framework.Services;
using AFOCS.VisionEditor.Services;
using AFOCS.VisionEditor.ViewModels;
using Caliburn.Micro;

namespace AFOCS.VisionEditor
{
    /// <summary>
    /// 视觉模板编辑器的 EditorProvider，
    /// 注册后会在"新建文件"菜单中出现"视觉模板"选项。
    /// </summary>
    [Export(typeof(IEditorProvider))]
    public class VisionEditorProvider : IEditorProvider
    {
        public IEnumerable<EditorFileType> FileTypes =>
        [
            new EditorFileType("视觉模板", ".nvision")
        ];

        public bool CanCreateNew => true;

        public bool Handles(string path)
            => System.IO.Path.GetExtension(path).Equals(".nvision", StringComparison.OrdinalIgnoreCase);

        public IDocument Create()
            => new VisionEditorDocumentViewModel(IoC.Get<IVisionNodeRegistry>());

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
}
