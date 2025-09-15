using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using UnityEngine;
using Channel = FishNet.Transporting.Channel;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions
{
    public abstract class Authenticator<T> : Authenticator where T : struct, IBroadcast
    {
        public override void Initialize()
        {
            ServerManager.RegisterBroadcast<T>(OnAuthData, false);
            OnInitialize();
        }

        public override void Destroy()
        {
            ServerManager.UnregisterBroadcast<T>(OnAuthData);
            OnDestroy();
        }

        private void OnAuthData(NetworkConnection connection, T data, Channel channel)
        {
            if (!InProcess.TryGetValue(connection.ClientId, out var context))
            {
                connection.Kick(KickReason.Unset, LoggingType.Warning, "Authentication not found.");
                return;
            }

            ConnectionContext.Handle.SetAuthenticator(context, this);
            ConnectionContext.Handle.SetKickTime(context, DataCheckTimeout + Time.time);
            
            CheckAsync(context, data).Forget();
        }

        private async UniTask CheckAsync(ConnectionContext context, T data)
        {
            var result = await OnDataCheck(context, data);
            ConnectionContext.Handle.SetDataApproved(context, result);
        }
        
        protected virtual void OnInitialize() { }
        protected virtual void OnDestroy() { }
        protected abstract UniTask<bool> OnDataCheck(ConnectionContext context, T data);
    }
    
    public abstract class Authenticator
    {
        private ServerManager _serverManager;
        protected Dictionary<int, ConnectionContext> InProcess;
        protected abstract float DataCheckTimeout { get; }

        public ServerManager ServerManager => _serverManager;

        public abstract void OnAuthenticationSuccess(ConnectionContext context);
        public abstract void OnAuthenticatedClientDisconnected(ConnectionContext context);
        public abstract void Initialize();
        public abstract void Destroy();
        
        public class Handle
        {
            public Handle(Authenticator authenticator)
            {
                Authenticator = authenticator;
            }

            public readonly Authenticator Authenticator;

            public void SetProcess(Dictionary<int, ConnectionContext> inProcess, ServerManager serverManager)
            {
                Authenticator._serverManager = serverManager;
                Authenticator.InProcess = inProcess;
            }
        }
    }
}