using System.Collections.Generic;
using FishNet.Managing.Server;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public class Room
    {
        internal Room(long uniqRoomId, ServerManager serverManager)
        {
            _serverManager = serverManager;
        }

        private readonly long _uniqRoomId;
        private readonly ServerManager _serverManager;
        private readonly Dictionary<int, ConnectionContext> _clients = new();
        
        public long UniqRoomId => _uniqRoomId;
        
    }
}