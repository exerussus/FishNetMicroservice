using System;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public class ServerSettings
    {
        public ServerSettings(string address, ushort port, IPipeline[] pipelines)
        {
            Address = address;
            Port = port;
            Pipelines = pipelines;
        }

        public readonly string Address;
        public readonly ushort Port;
        public readonly IPipeline[] Pipelines;
        public Action OnServerStopped;
    }
}