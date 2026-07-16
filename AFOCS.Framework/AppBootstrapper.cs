using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.ReflectionModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using AFOCS.Framework.Framework.Services;
using Caliburn.Micro;

namespace AFOCS.Framework
{
    public class AppBootstrapper : BootstrapperBase
    {
        private List<Assembly> _priorityAssemblies;

        protected CompositionContainer Container { get; set; }

        internal IList<Assembly> PriorityAssemblies
            => _priorityAssemblies;


        public AppBootstrapper()
        {
            PreInitialize();
            Initialize();
        }

        protected virtual void PreInitialize()
        {
            var code = Properties.Settings.Default.LanguageCode;

            if (!string.IsNullOrWhiteSpace(code))
            {
                var culture = CultureInfo.GetCultureInfo(code);
                Thread.CurrentThread.CurrentUICulture = culture;
                Thread.CurrentThread.CurrentCulture = culture;
            }
        }

        protected override void Configure()
        {
            PopulateAssemblySource();

            // Prioritise the executable assembly. This allows the client project to override exports, including IShell.
            // The client project can override SelectAssemblies to choose which assemblies are prioritised.
            _priorityAssemblies = SelectAssemblies().ToList();
            var priorityCatalog = new AggregateCatalog(_priorityAssemblies.Select(x => new AssemblyCatalog(x)));
            var priorityProvider = new CatalogExportProvider(priorityCatalog);

            // Now get all other assemblies (excluding the priority assemblies).
            var mainCatalog = new AggregateCatalog(
                AssemblySource.Instance
                    .Where(assembly => !_priorityAssemblies.Contains(assembly))
                    .Select(x => new AssemblyCatalog(x)));
            var mainProvider = new CatalogExportProvider(mainCatalog);

            Container = new CompositionContainer(priorityProvider, mainProvider);
            priorityProvider.SourceProvider = Container;
            mainProvider.SourceProvider = Container;

            var batch = new CompositionBatch();

            BindServices(batch);
            batch.AddExportedValue(mainCatalog);

            Container.Compose(batch);
        }

        protected virtual void PopulateAssemblySource()
        {
            PopulateAssemblySourceUsingDirectoryCatalog(AppContext.BaseDirectory);
        }

        protected void PopulateAssemblySourceUsingDirectoryCatalog(string path)
        {
            var directoryCatalog = new DirectoryCatalog(path);
            AssemblySource.Instance.AddRange(
                directoryCatalog.Parts
                    .Select(part => ReflectionModelServices.GetPartType(part).Value.Assembly)
                    .Where(assembly => !AssemblySource.Instance.Contains(assembly)));
        }

        protected virtual void BindServices(CompositionBatch batch)
        {
            batch.AddExportedValue<IWindowManager>(new WindowManager());
            batch.AddExportedValue<IEventAggregator>(new EventAggregator());
            batch.AddExportedValue(Container);
            batch.AddExportedValue(this);
        }

        protected override object GetInstance(Type serviceType, string key)
        {
            string contract = string.IsNullOrEmpty(key) ? AttributedModelServices.GetContractName(serviceType) : key;
            var exports = Container.GetExports<object>(contract);

            if (exports.Any())
                return exports.First().Value;

            throw new Exception($"Could not locate any instances of contract {contract}.");
        }

        protected override IEnumerable<object> GetAllInstances(Type serviceType)
            => Container.GetExportedValues<object>(AttributedModelServices.GetContractName(serviceType));

        protected override void BuildUp(object instance)
            => Container.SatisfyImportsOnce(instance);

        protected override void OnStartup(object sender, StartupEventArgs e)
        {
            base.OnStartup(sender, e);
            DisplayRootViewForAsync<IMainWindow>();
        }

        protected override Assembly[] SelectAssemblies()
            => [Assembly.GetEntryAssembly()!];
    };
}
