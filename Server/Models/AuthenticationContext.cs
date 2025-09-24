
using FishNet.Broadcast;
using FishNet.Connection;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    internal class AuthenticationContext<TData> where TData : struct, IBroadcast
    {
        public long UserId;
        public NetworkConnection NetworkConnection;
        public bool DataApproved;
        public float KickTime;
        public TData Data;     
    }

    internal class AuthenticationAwaiter
    {
        public float KickTime;
        public bool Kicked;
        public NetworkConnection NetworkConnection;
        public bool IsAuthenticatorDetected;
    }
}