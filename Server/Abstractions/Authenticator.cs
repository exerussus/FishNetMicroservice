
using Cysharp.Threading.Tasks;
using FishNet.Broadcast;
using FishNet.Connection;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions
{
    public interface IAuthenticator<TAuthData, TMetaData> : IAuthenticator where TAuthData : struct, IBroadcast
    {
        public UniTask<(bool isApproved, long userId, TMetaData metaData)> OnDataCheck(NetworkConnection networkConnection, TAuthData data);
        public void OnAuthenticationSuccess(long userId, NetworkConnection networkConnection, TAuthData data);
    }
    
    public interface IAuthenticator
    {
        public float DataCheckTimeout { get; }
        public virtual void OnInitialize() { }
        public virtual void OnDestroy() { }
    }
}