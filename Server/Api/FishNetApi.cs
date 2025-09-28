
using Exerussus._1Extensions.MicroserviceFeature;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Api
{
    public struct RunServer : IChannel
    {
        public RunServer(ServerSettings settings)
        {
            Settings = settings;
        }

        public readonly ServerSettings Settings;
    }

    public readonly struct OnServerStateChanged : IChannel
    {
        public OnServerStateChanged(bool isActive)
        {
            IsActive = isActive;
        }

        public readonly bool IsActive;
    }
}