using System.ComponentModel.Composition.Hosting;

namespace AFOCS.FlowNodeEditor
{
    /// <summary>
    /// 简易全局服务定位器，用于 EditorProvider 等非 DI 构造场景
    /// </summary>
    public static class AppBootstrapper
    {
        private static CompositionContainer? _container;

        public static void Initialize(CompositionContainer container)
        {
            _container = container;
        }

        public static T GetInstance<T>() where T : class
        {
            if (_container == null)
                throw new InvalidOperationException("Container not initialized");
            return _container.GetExportedValue<T>() ?? throw new InvalidOperationException($"Service of type {typeof(T).FullName} not registered.");
        }

        public static IEnumerable<T> GetAllInstances<T>() where T : class
        {
            if (_container == null)
                throw new InvalidOperationException("Container not initialized");
            return _container.GetExportedValues<T>();
        }
    }
}
