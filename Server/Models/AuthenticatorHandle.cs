using System;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public class AuthenticatorHandle
    {
        public AuthenticatorHandle(IAuthenticator authenticator)
        {
            Authenticator = authenticator;
        }

        public readonly IAuthenticator Authenticator;
        public Action Dispose;
    }
}