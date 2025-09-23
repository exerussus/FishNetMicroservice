using System.Collections.Generic;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    

    public class Pipeline<TConnection, TRoom, TMatchMaker, TSession> : IPipeline
        where TConnection : ConnectionContext, new()
        where TRoom : Room, new()
        where TMatchMaker : IMatchMaker, new()
        where TSession : ISession, new()
    {
        internal readonly Dictionary<long, TConnection> Connections = new();
        internal readonly Dictionary<long, TRoom> Rooms = new();
        internal readonly TMatchMaker MatchMaker = new();
        internal readonly TSession Session = new();
        
        
    }

    public interface IPipeline
    {
        
    }
}