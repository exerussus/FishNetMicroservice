using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing.Server;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public abstract class Room<TConnection, TMetaData> : IRoom 
        where TConnection : PlayerContext<TMetaData>, new()
    {
        internal void SetRoomRefs(long uniqRoomId, ServerManager serverManager, IPipeline pipeline)
        {
            _uniqRoomId = uniqRoomId;
            _serverManager = serverManager;
            _pipeline = pipeline;
        }

        private long _uniqRoomId;
        private ServerManager _serverManager;
        private IPipeline _pipeline;
        private readonly Dictionary<long, TConnection> _allClients = new();

        internal float SessionStopTime;
        
        public Dictionary<long, TConnection>.ValueCollection ActiveClients => _allClients.Values;
        public bool IsSessionStarted { get; private set; }
        public bool IsSessionCancelled { get; private set; }
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
        
        internal void SetSessionCancelled(bool isCancelled)
        {
            IsSessionCancelled = isCancelled;
        }
        
        internal void AddClient(TConnection client)
        {
            _allClients.Add(client.UserId, client);
        }
        
        internal void RemoveClient(TConnection client)
        {
            _allClients.Remove(client.UserId);
        }

        public async UniTask StartSession()
        {
            await _pipeline.StartSession(_uniqRoomId);
        }

        public bool TryGetClient(long userId, out TConnection client)
        {
            return _allClients.TryGetValue(userId, out client);
        }
        
        public async UniTask StopSession()
        {
            await _pipeline.StopSession(_uniqRoomId);
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

    public interface IRoom
    {
        
    }
}