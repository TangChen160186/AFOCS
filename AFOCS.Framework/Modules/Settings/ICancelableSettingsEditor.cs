namespace Gemini.Modules.Settings
{
    /// <summary>
    /// 可取消的设置编辑器。当用户在设置对话框中点击取消或关闭时，
    /// SettingsViewModel 会调用此方法，让编辑器恢复变更前的状态。
    /// </summary>
    public interface ICancelableSettingsEditor
    {
        void CancelChanges();
    }
}
