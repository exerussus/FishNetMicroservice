using System.Collections.Generic;
using FishNet.Managing.Server;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public class Room
    {
        internal Room(int uniqRoomId, ServerManager serverManager)
        {
            _serverManager = serverManager;
        }

        private readonly int _uniqRoomId;
        private readonly ServerManager _serverManager;
        private readonly Dictionary<int, ConnectionContext> _clients = new();
        
        public int UniqRoomId => _uniqRoomId;
        
    }
}