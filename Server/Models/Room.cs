using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing.Server;
using UnityEngine;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public class Room
    {
        internal Room(long uniqRoomId, ServerManager serverManager, FishNetServerMicroservice microservice)
        {
            _uniqRoomId = uniqRoomId;
            _serverManager = serverManager;
            _microservice = microservice;
        }

        private readonly long _uniqRoomId;
        private readonly ServerManager _serverManager;
        private readonly FishNetServerMicroservice _microservice;
        private readonly Dictionary<long, ConnectionContext> _allClients = new();
        public readonly List<ConnectionContext> ActiveClients = new();
        public bool IsSessionStarted { get; private set; }
        public bool IsSessionDone { get; private set; }
        
        public long UniqRoomId => _uniqRoomId;

        internal void AddClient(ConnectionContext client)
        {
            _allClients.Add(client.UserId, client);
        }

        internal void SetClientActive(ConnectionContext client, bool isActive)
        {
            if (!_allClients.ContainsKey(client.UserId))
            {
                Debug.LogError($"Room {UniqRoomId} doesn't have client {client.UserId}!");
                return;
            }
            
            ConnectionContext.Handle.SetActive(client, isActive);
        }

        public void StartSession()
        {
            IsSessionStarted = true;
        }
        
        public void StopSession()
        {
            IsSessionDone = true;
        }

        public void Broadcast<T>(T broadcast) where T : struct, IBroadcast
        {
            foreach (var client in _allClients.Values)
            {
                _serverManager.Broadcast(client, broadcast);
            }
        }

        public void BroadcastExcept<T>(NetworkConnection connection, T broadcast) where T : struct, IBroadcast
        {
            foreach (var client in _allClients.Values)
            {
                if (connection.ClientId == client.NetworkConnection.ClientId) continue;
                _serverManager.Broadcast(client, broadcast);
            }
        }
    }
}