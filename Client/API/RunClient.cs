using Exerussus._1Extensions.MicroserviceFeature;
using Exerussus.MicroservicesModules.FishNetMicroservice.Client.Abstractions;
using Exerussus.MicroservicesModules.FishNetMicroservice.Client.Models;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Client.API
{
    public struct RunClient : IChannel
    {
        public RunClient(string address, ushort port, IConnector connector)
        {
            Address = address;
            Port = port;
            Connector = connector;
            Response = new RunClientResponse();
        }

        public readonly string Address;
        public readonly ushort Port;
        public readonly IConnector Connector;
        public readonly RunClientResponse Response;
    }

    public struct StopClient : IChannel
    {
        
    }
}