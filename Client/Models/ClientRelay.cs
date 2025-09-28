using System;
using System.Collections.Generic;
using Exerussus._1Extensions.SignalSystem;
using FishNet;
using FishNet.Broadcast;
using FishNet.Managing.Client;
using FishNet.Transporting;
using UnityEngine;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Client.Models
{
    public class ClientRelay
    {
        private Action<ClientManager> _disposeAction;
        private Signal _signal;
        private HashSet<Type> _types;
        private bool _isLogsEnabled;
        
        public ClientRelay(Signal signal, bool isLogsEnabled = false)
        {
            _disposeAction = _ => { }; 
            _signal = signal;
            _types = new HashSet<Type>();
            _isLogsEnabled = isLogsEnabled;
        }
        
        public ClientRelay AddSignal<T>() where T : struct, IBroadcast
        {
            if (_isLogsEnabled) Debug.Log($"ClientRelay.AddSignal | Subscribed to {typeof(T).Name}.");
            if (!_types.Add(typeof(T))) return this;
            
            InstanceFinder.ClientManager.RegisterBroadcast<T>(OnBroadcast);
            _disposeAction += clientManager =>
            {
                if (_isLogsEnabled) Debug.Log($"ClientRelay | Unsubscribed to {typeof(T).Name}.");
                clientManager.UnregisterBroadcast<T>(OnBroadcast);
            };
            return this;
        }

        public ClientRelay AddSignalTranslator<TBroadcast, TSignal>(SignalTranslator<TBroadcast, TSignal> function) 
            where TBroadcast : struct, IBroadcast
            where TSignal : struct
        {
            if (_isLogsEnabled) Debug.Log($"ClientRelay.AddSignalTranslator | Subscribed to {typeof(TBroadcast).Name}.");
            Action<TBroadcast, Channel> action = (data, _) =>
            {
                var result = function.Invoke(data, out var signal);
                if (result) _signal.RegistryRaise(ref signal);
            };
            
            InstanceFinder.ClientManager.RegisterBroadcast(action);
            _disposeAction += clientManager =>
            {
                if (_isLogsEnabled) Debug.Log($"ClientRelay | Unsubscribed to {typeof(TBroadcast).Name}.");
                clientManager.UnregisterBroadcast(action);
            };
            
            return this;
        }

        public void Unsubscribe()
        {
            var clientManager = InstanceFinder.ClientManager;
            if (clientManager == null)
            {
                if (_isLogsEnabled) Debug.LogWarning("ServerRelay.Unsubscribe | ClientManager is null.");
                return;
            }
            _disposeAction?.Invoke(clientManager);
            _disposeAction = null;
        }
        
        private void OnBroadcast<T>(T data, Channel channel) where T : struct, IBroadcast
        {
            if (_isLogsEnabled) Debug.Log($"ClientRelay | Broadcast : {typeof(T).Name}.");
            _signal.RegistryRaise(ref data);
        }
        
        public delegate bool SignalTranslator<in TBroadcast, TSignal>(TBroadcast broadcast, out TSignal signal)
            where TBroadcast : struct, IBroadcast 
            where TSignal : struct;
    }
}