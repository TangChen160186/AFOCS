using System.Collections.ObjectModel;
using AFOCS.App.Communication;
using AFOCS.App.Devices;
using AFOCS.App.Enums;
using AFOCS.App.Shared;
using Caliburn.Micro;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.ViewModels
{
    internal class SplashScreenViewModel : Screen
    {
        public ObservableCollection<string> LogMessages { get; } = new ObservableCollection<string>();

        private readonly ILogger<SplashScreenViewModel> _logger;
        private readonly ISerialPortClient _serialPortClient;
        private readonly IConfigService _configService;

        public SplashScreenViewModel(ILogger<SplashScreenViewModel> logger,ISerialPortClient serialPortClient,IConfigService configService)
        {
            _logger = logger;
            _serialPortClient = serialPortClient;
            _configService = configService;

        }

        protected override Task OnActivatedAsync(CancellationToken cancellationToken)
        {
            InitializeDevices();
            return  base.OnActivatedAsync(cancellationToken);
        }
        protected override void OnViewLoaded(object view)
        {
            base.OnViewLoaded(view);
        }



        private async void InitializeDevices()
        {
            var gluDispenserLeft = IoC.Get<IGlueDispenser>(nameof(WorkPos.Left));
            var gluDispenserRight = IoC.Get<IGlueDispenser>(nameof(WorkPos.Right));
            var result = await gluDispenserLeft.InitializeAsync();
            LogMessages.Add(result.IsSuccess ? "左工位点胶机初始化成功" : $"左工位点胶机初始化失败\n error:{result.Message}");
            result = await gluDispenserRight.InitializeAsync();
            LogMessages.Add(result.IsSuccess ? "右工位点胶机初始化成功" : $"右工位点胶机初始化失败\n error:{result.Message}");

            var opmLeft = IoC.Get<IOpticalPowerMeter>(nameof(WorkPos.Left));
            var opmRight = IoC.Get<IOpticalPowerMeter>(nameof(WorkPos.Right));
            result = await opmLeft.InitializeAsync();
            LogMessages.Add(result.IsSuccess ? "左工位功率机箱初始化成功" : $"左工位功率机箱初始化失败\n error:{result.Message}");
            result = await opmRight.InitializeAsync();
            LogMessages.Add(result.IsSuccess ? "右工位功率机箱初始化成功" : $"右工位功率机箱初始化失败\n error:{result.Message}");
            Console.WriteLine();
        }


    }
}
