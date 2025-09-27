
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Exerussus._1Extensions.DelayedActionsFeature;
using Exerussus._1Extensions.Scripts.Extensions;
using Exerussus._1Extensions.ThreadGateFeature;
using Exerussus.Microservices.Runtime;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Api;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.MonoBehaviours;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server
{
    [RequireComponent(typeof(ServiceCustomAuthenticator), typeof(ServerManager), typeof(Tugboat))]
    [RequireComponent(typeof(NetworkManager))]
    public class FishNetServerMicroservice : MonoBehaviour,
        IService,
        IChannelPuller<RunServer>
    {
        public ConnectionStart startType;
        public ServiceHandle Handle { get; set; }
        internal ServerManager ServerManager;
        internal ServerSettings ServerSettings;
        internal Tugboat Tugboat;
        internal NetworkManager NetworkManager;
        internal ServiceCustomAuthenticator CustomAuthenticator;
        internal readonly Dictionary<Type, IPipeline> Pipelines = new ();
        internal readonly Dictionary<int, AuthenticationAwaiter> AwaitingAuthenticators = new();
        internal readonly Dictionary<int, IPipeline> SegregatedClients = new();
        
        // roomId to Hashset of clients
        private readonly HashSet<int> _approvedList = new();

        private const float AuthenticationTimeout = 5f;
        public float UpdateDelay => 0.5f;
        
        private bool _isInitialized = false;
        private bool _isStarted = false;

        private void Awake()
        {
            if (startType == ConnectionStart.Awake) InitializeService();
        }
        
        private void Start()
        {
            if (startType == ConnectionStart.Start) InitializeService();
        }

        private void OnDestroy()
        {
            ServerManager.OnRemoteConnectionState -= OnConnectionStateChanged;
        }

        public async UniTask InitializeAndRunServer(ServerSettings settings)
        {
            await ThreadGate.CreateJob(InitializeService).Run().AsUniTask();
            await PullBroadcast(new RunServer(settings));
        }
        
        public void InitializeService()
        {
            if (_isStarted) return;
            
            _isStarted = true;
            ServerManager = gameObject.GetComponent<ServerManager>();
            Tugboat = gameObject.GetComponent<Tugboat>();
            CustomAuthenticator = gameObject.TryGetComponent<ServiceCustomAuthenticator>(out var customAuth) ? customAuth : gameObject.AddComponent<ServiceCustomAuthenticator>();
            NetworkManager = gameObject.GetComponent<NetworkManager>();
            ServerManager.OnRemoteConnectionState += OnConnectionStateChanged;
            
            MicroservicesApi.RegisterService(this);
        }
        
        public async UniTask PullBroadcast(RunServer channel)
        {
            if (_isInitialized)
            {
                Debug.LogWarning($"FishNetServerMicroservice | Server already initialized.");    
                return;
            }
            
            if (channel.Settings.Pipelines == null)
            {
                Debug.LogError("FishNetServerMicroservice | Authenticators is null");
                return;
            }
            
            if (channel.Settings.Pipelines.Length == 0)
            {
                Debug.LogError("FishNetServerMicroservice | Authenticators is empty");
                return;
            }
            
            await ThreadGate.CreateJob(() => InitializeAuthenticators(channel.Settings)).Run().AsUniTask();
            await DelayedAction.Create(0.1f, () => { })
                .WithCondition(() => _isInitialized && _isStarted)
                .Run().AsUniTask();
        }
        
        public void Update()
        {
            var time = Time.time;
            
            foreach (var pipeline in Pipelines.Values) pipeline.Update(time);
            foreach (var awaiter in AwaitingAuthenticators.Values)
            {
                if (awaiter.KickTime < time && !awaiter.Kicked)
                {
                    awaiter.Kicked = true;
                    awaiter.NetworkConnection.Kick(KickReason.Unset, LoggingType.Warning, "Authentication not found.");
                }
            }
        }
        
        private void OnConnectionStateChanged(NetworkConnection connection, RemoteConnectionStateArgs data)
        {
            if (data.ConnectionState == RemoteConnectionState.Started)
            {
                var process = new AuthenticationAwaiter();
                process.KickTime = Time.time + AuthenticationTimeout;
                process.NetworkConnection = connection;
                
                AwaitingAuthenticators.Add(connection.ClientId, process);
                Debug.Log($"Added client {connection.ClientId} to authentication queue.");
            }
            else
            {
                if (AwaitingAuthenticators.TryPop(connection.ClientId, out var context)) return;
                
                if (SegregatedClients.TryPop(connection.ClientId, out var pipeline))
                {
                    pipeline.OnConnectionStateChanged(connection, data);
                }
            }
        }

        private void InitializeAuthenticators(ServerSettings settings)
        {
            ServerSettings = settings;
            
            if (Tugboat == null)
            {
                Debug.LogError("FishNetServerMicroservice | Tugboat is null. Please add Tugboat to the same GameObject as ServerManager.");
                return;
            }

            foreach (var pipeline in ServerSettings.Pipelines)
            {
                pipeline.Initialize(this);
                var authenticatorType = pipeline.GetType();
                if (Pipelines.ContainsKey(authenticatorType))
                {
                    Debug.LogError($"FishNetServerMicroservice | Authenticator already exists for type {authenticatorType}"); 
                }
                else
                {
                    Pipelines.Add(authenticatorType, pipeline);
                }
            }

            Tugboat.SetClientAddress(settings.Address);
            Tugboat.SetPort(settings.Port);
            ServerManager.OnServerConnectionState += OnServerConnectionStateChanged;
            ServerManager.StartConnection();
            
            _isInitialized = true;
        }

        private void OnServerConnectionStateChanged(ServerConnectionStateArgs state)
        {
            if (state.ConnectionState == LocalConnectionState.Started)
            {
                _isStarted = true;
            }
            else if (state.ConnectionState == LocalConnectionState.Stopped)
            {
                foreach (var pipeline in Pipelines.Values)
                {
                    pipeline.OnDestroy();
                }
                Pipelines.Clear();
                AwaitingAuthenticators.Clear();
                if (ServerSettings != null)
                {
                    ServerSettings.OnServerStopped?.Invoke();
                    ServerSettings.OnServerStopped = null;
                    ServerSettings = null;
                }
                ServerManager.OnServerConnectionState -= OnServerConnectionStateChanged;
                _isStarted = false;
                _isInitialized = false;
            }
        }

        public enum ConnectionStart
        {
            Manual,
            Awake,
            Start,
        }
    }
}