using AFOCS.App.Communication;
using AFOCS.App.Shared;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AFOCS.App.Devices.Implementation
{
    public class OpticalPowerMeterLeft : OpticalPowerMeter
    {
        public OpticalPowerMeterLeft(ITcpClient tcpClient, IConfigService configService, ILogger<OpticalPowerMeter> logger) : base(tcpClient, configService, logger)
        {
        }
    }
}
