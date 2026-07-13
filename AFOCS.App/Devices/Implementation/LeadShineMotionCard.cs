using System.IO;
using AFOCS.App.Core;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;

namespace AFOCS.App.Devices.Implementation
{
    public class LeadShineMotionCardConfig
    {
        public string EniPath { get; set; } = "";

        public string IniPath { get; set; } = "";

    }
    public class LeadShineMotionCard: IMotionControlCard
    {
        private readonly IConfigService _configService;
        private readonly ILogger<LeadShineMotionCard> _logger;
        public bool IsConnected { get; }

        public LeadShineMotionCard(IConfigService configService,ILogger<LeadShineMotionCard> logger)
        {
            _configService = configService;
            _logger = logger;
        }
        public async Task<Result> InitializeAsync(CancellationToken token = default)
        {
            try
            {
                var config = await _configService.LoadAsync<LeadShineMotionCardConfig>();
                if (config == null)
                {
                    config = new LeadShineMotionCardConfig();
                    await _configService.SaveAsync(config);
                }
                if(string.IsNullOrWhiteSpace(config.IniPath) && File.Exists(config.IniPath) || File.Exists(config.))
               
                return Result.Success();
            }
            finally
            {
                
            }
        }

        public Task<Result> StopAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result> ReConnectAsync(CancellationToken token = default)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            // TODO release managed resources here
        }

    }
}
