using FishNet.Connection;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Global.Abstractions
{
    public interface IClientBroadcast
    {
        public NetworkConnection Connection { get; set; }
    }
}