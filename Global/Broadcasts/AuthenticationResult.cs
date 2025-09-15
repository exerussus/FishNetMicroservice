using FishNet.Broadcast;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Global.Broadcasts
{
    internal struct AuthenticationResult : IBroadcast
    {
        public AuthenticationResult(bool success)
        {
            Success = success;
        }

        public readonly bool Success;
    }
}