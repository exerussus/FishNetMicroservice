
using Exerussus._1Extensions.MicroserviceFeature;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Api
{
    public struct RunServer : IChannel
    {
        public RunServer(string address, ushort port, (IAuthenticator authenticator, IMatchMaker matchMaker, ISession session)[] pipelines)
        {
            Address = address;
            Port = port;
            Pipelines = pipelines;
        }

        public readonly string Address;
        public readonly ushort Port;
        public readonly (IAuthenticator authenticator, IMatchMaker matchMaker, ISession session)[] Pipelines;
    }

    public struct StartSession : IChannel
    {
        
    }
}