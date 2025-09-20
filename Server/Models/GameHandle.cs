using System;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public class GameHandle
    {
        public GameHandle(IAuthenticator authenticator, IMatchMaker matchMaker, ISession session)
        {
            Authenticator = authenticator;
            MatchMaker = matchMaker;
            Session = session;
        }

        public readonly IAuthenticator Authenticator;
        public readonly IMatchMaker MatchMaker;
        public readonly ISession Session;
        public Action Dispose;
    }
}