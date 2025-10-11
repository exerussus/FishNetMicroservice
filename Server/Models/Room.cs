using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Exerussus._1Extensions.SignalSystem;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing.Server;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public abstract class Room<TConnection, TMetaUserData, TRoomMetaData> : IRoom 
        where TMetaUserData : IUserMetaData
        where TConnection : PlayerContext<TMetaUserData>, new()
    {
        internal void SetRoomRefs(long uniqRoomId, ServerManager serverManager, IPipeline pipeline)
        {
            _uniqRoomId = uniqRoomId;
            _serverManager = serverManager;
            _pipeline = pipeline;
        }

        private long _uniqRoomId;
        private TRoomMetaData _roomMetaData;
        private ServerManager _serverManager;
        private IPipeline _pipeline;
        private readonly Dictionary<long, TConnection> _allClients = new();
        private readonly Dictionary<int, TConnection> _allClientsByConnectionId = new();

        internal float SessionStopTime;
        
        public Dictionary<long, TConnection>.ValueCollection ActiveClients => _allClients.Values;
        public bool IsSessionStarted { get; private set; }
        public bool IsSessionCanceled { get; private set; }
        public bool IsSessionClosed { get; private set; }
        public Signal Signal { get; } = new();

        public TRoomMetaData RoomMetaData => _roomMetaData;

        public long UniqRoomId => _uniqRoomId;
        
        internal void SetSessionStarted(bool isStarted)
        {
            IsSessionStarted = isStarted;
        }
        
        internal void SetSessionClosed(bool isSessionClosed)
        {
            IsSessionClosed = isSessionClosed;
        }
        
        internal void SetRoomMetaData(TRoomMetaData metaData)
        {
            _roomMetaData = metaData;
        }
        
        internal void SetSessionCancelled(bool isCancelled)
        {
            IsSessionCanceled = isCancelled;
        }
        
        internal void AddClient(TConnection client)
        {
            _allClients.Add(client.UserId, client);
            _allClientsByConnectionId[client.NetworkConnection.ClientId] = client;
        }
        
        internal void RemoveClient(TConnection client)
        {
            _allClients.Remove(client.UserId);
            _allClientsByConnectionId.Remove(client.NetworkConnection.ClientId);
        }

        public async UniTask StartSession(CancellationToken ct)
        {
            await _pipeline.StartSession(_uniqRoomId, ct);
        }

        public bool TryGetClient(long userId, out TConnection client)
        {
            return _allClients.TryGetValue(userId, out client);
        }

        public bool TryGetClient(NetworkConnection connection, out TConnection client)
        {
            return _allClientsByConnectionId.TryGetValue(connection.ClientId, out client);
        }
        
        public async UniTask StopSession(CancellationToken ct)
        {
            await _pipeline.StopSession(_uniqRoomId, ct);
        }
        
        public async UniTask CloseSession(CancellationToken ct)
        {
            await _pipeline.CloseSession(_uniqRoomId, ct);
        }

        public void Broadcast<T>(T broadcast) where T : struct, IBroadcast
        {
            foreach (var client in ActiveClients)
            {
                _serverManager.Broadcast(client, broadcast);
            }
        }

        public void Broadcast<T>(NetworkConnection connection, T broadcast) where T : struct, IBroadcast
        {
            _serverManager.Broadcast(connection, broadcast);
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
        public Signal Signal { get; } 
        public long UniqRoomId { get; }
        public bool IsSessionStarted { get; }
        public bool IsSessionCanceled { get; }
        public bool IsSessionClosed { get; }
        public UniTask StartSession(CancellationToken ct);
        public UniTask StopSession(CancellationToken ct);
        public void Broadcast<T>(T broadcast) where T : struct, IBroadcast;
        public void Broadcast<T>(NetworkConnection connection, T broadcast) where T : struct, IBroadcast;
        public void BroadcastExcept<T>(NetworkConnection connection, T broadcast) where T : struct, IBroadcast;
    }
}