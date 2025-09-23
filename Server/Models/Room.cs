using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing.Server;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public class Room
    {
        internal Room(long uniqRoomId, ISession session, ServerManager serverManager, FishNetServerMicroservice microservice)
        {
            _uniqRoomId = uniqRoomId;
            _serverManager = serverManager;
            _microservice = microservice;
            Session = session;
        }

        private readonly long _uniqRoomId;
        private readonly ServerManager _serverManager;
        private readonly FishNetServerMicroservice _microservice;
        private readonly Dictionary<long, ConnectionContext> _allClients = new();
        internal readonly ISession Session;
        public Dictionary<long, ConnectionContext>.ValueCollection ActiveClients => _allClients.Values;
        public bool IsSessionStarted { get; private set; }
        public bool IsSessionDone { get; private set; }
        
        public long UniqRoomId => _uniqRoomId;
        
        internal void SetSessionStarted(bool isStarted)
        {
            IsSessionStarted = isStarted;
        }
        
        internal void SetSessionDone(bool isDone)
        {
            IsSessionDone = isDone;
        }
        
        internal void AddClient(ConnectionContext client)
        {
            _allClients.Add(client.UserId, client);
        }
        
        internal void RemoveClient(ConnectionContext client)
        {
            _allClients.Remove(client.UserId);
            if (_allClients.Count == 0) StopSession().Forget();
        }

        public async UniTask StartSession()
        {
            await _microservice.StartSession(_uniqRoomId);
        }
        
        public async UniTask StopSession()
        {
            await _microservice.StopSession(_uniqRoomId);
        }

        public void Broadcast<T>(T broadcast) where T : struct, IBroadcast
        {
            foreach (var client in ActiveClients)
            {
                _serverManager.Broadcast(client, broadcast);
            }
        }

        public void BroadcastExcept<T>(NetworkConnection connection, T broadcast) where T : struct, IBroadcast
        {
            foreach (var client in ActiveClients)
            {
                if (connection.ClientId == client.NetworkConnection.ClientId) continue;
                _serverManager.Broadcast(client, broadcast);
            }
        }
    }
}