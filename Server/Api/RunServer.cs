
using Exerussus._1Extensions.MicroserviceFeature;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Api
{
    public struct RunServer : IChannel
    {
        public RunServer(string address, ushort port, IAuthenticator[] authenticators)
        {
            Address = address;
            Port = port;
            Authenticators = authenticators;
        }

        public readonly string Address;
        public readonly ushort Port;
        public readonly IAuthenticator[] Authenticators;
    }

    public struct StartSession : IChannel
    {
        
    }
}