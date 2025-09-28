using System;
using System.Collections.Generic;
using Exerussus._1Extensions.SignalSystem;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing.Server;
using FishNet.Transporting;
using UnityEngine;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public class ObserverRelay
    {
        private Action _disposeAction;
        private Signal _signal;
        private HashSet<Type> _types;
        private ServerManager _serverManager;
        private IPipeline _pipeline;
        private bool _logsEnabled;
        
        public ObserverRelay(Signal signal, ServerManager serverManager, IPipeline pipeline, bool logsEnabled = false)
        {
            _disposeAction = () => { }; 
            _signal = signal;
            _types = new HashSet<Type>();
            _pipeline = pipeline;
            _serverManager = serverManager;
            _logsEnabled = logsEnabled;
        }

        public ObserverRelay AddSignal<T>() where T : struct, IBroadcast
        {
            if (!_types.Add(typeof(T))) return this;
            
            if (_logsEnabled) Debug.Log($"BroadcastObserver | Subscribed to {typeof(T).Name}.");
            _serverManager.RegisterBroadcast<T>(OnBroadcast);
            _disposeAction += () => _serverManager.UnregisterBroadcast<T>(OnBroadcast);
            return this;
        }
        
        private void OnBroadcast<T>(NetworkConnection connection, T data, Channel channel) where T : struct, IBroadcast
        {
            if (!_pipeline.TryGetRoom(connection, out var room))
            {
                if (_logsEnabled) Debug.LogError($"BroadcastObserver | Can't find connections handler for connection {connection}.");
                return;
            }
            room.BroadcastExcept(connection, data);
        }
        
        public void Unsubscribe()
        {
            if (_serverManager != null) _disposeAction?.Invoke();
        }
    }
}