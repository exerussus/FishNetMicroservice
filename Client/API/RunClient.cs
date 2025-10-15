using Exerussus._1Extensions.MicroserviceFeature;
using Exerussus.MicroservicesModules.FishNetMicroservice.Client.Abstractions;
using Exerussus.MicroservicesModules.FishNetMicroservice.Client.Models;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Client.API
{
    public struct RunClient : ICommand<(bool isSuccess, RunResult resultDetails)>
    {
        public RunClient(string address, ushort port, IConnector connector)
        {
            Address = address;
            Port = port;
            Connector = connector;
        }

        public readonly string Address;
        public readonly ushort Port;
        public readonly IConnector Connector;
    }

    public struct StopClient : IChannel
    {
        
    }
}