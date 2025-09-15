using System;
using FishNet.Connection;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.MonoBehaviours
{
    public class ServiceCustomAuthenticator: FishNet.Authenticating.Authenticator
    {
        public override event Action<NetworkConnection, bool> OnAuthenticationResult;
        
        public void SetAuthResult(NetworkConnection connection, bool result) => OnAuthenticationResult?.Invoke(connection, result);
    }
}