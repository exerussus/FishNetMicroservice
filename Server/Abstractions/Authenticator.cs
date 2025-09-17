
using Cysharp.Threading.Tasks;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models;
using FishNet.Broadcast;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions
{
    public interface IAuthenticator<T> : IAuthenticator where T : struct, IBroadcast
    {
        public UniTask<bool> OnDataCheck(ConnectionContext context, T data);
    }
    
    public interface IAuthenticator
    {
        public float DataCheckTimeout { get; }
        public void OnAuthenticationSuccess(ConnectionContext context);
        public void OnAuthenticatedClientDisconnected(ConnectionContext context);
        
        public virtual void OnInitialize() { }
        public virtual void OnDestroy() { }
    }
}