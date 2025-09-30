using System;
using System.Threading;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public class ServerSettings
    {
        public ServerSettings(string address, ushort port, IPipeline[] pipelines, CancellationTokenSource cts)
        {
            Address = address;
            Port = port;
            Pipelines = pipelines;
            CancellationTokenSource = cts;
        }

        public readonly string Address;
        public readonly ushort Port;
        public readonly IPipeline[] Pipelines;
        public readonly CancellationTokenSource CancellationTokenSource;
        public Action OnServerStopped;
    }
}