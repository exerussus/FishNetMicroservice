
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Exerussus._1Extensions.DelayedActionsFeature;
using Exerussus._1Extensions.LoopFeature;
using Exerussus._1Extensions.Scripts.Extensions;
using Exerussus._1Extensions.ThreadGateFeature;
using Exerussus.Microservices.Runtime;
using Exerussus.MicroservicesModules.FishNetMicroservice.Global.Broadcasts;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Api;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.MonoBehaviours;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;
using Channel = FishNet.Transporting.Channel;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server
{
    [RequireComponent(typeof(ServiceCustomAuthenticator), typeof(ServerManager), typeof(Tugboat))]
    [RequireComponent(typeof(NetworkManager))]
    public class FishNetServerMicroservice : MonoBehaviour,
        IService,
        IChannelPuller<RunServer>,
        IChannelPuller<StartSession>
    {
        public ConnectionStart startType;
        public ServiceHandle Handle { get; set; }
        private ServerManager _serverManager;
        private Tugboat _tugboat;
        private NetworkManager _networkManager;
        private ServiceCustomAuthenticator _customAuthenticator;
        private readonly Dictionary<Type, AuthenticatorHandle> _authenticators = new ();
        
        private readonly Dictionary<int, ConnectionContext> _inProcess = new();
        private readonly Dictionary<int, ConnectionContext> _authenticated = new ();
        private readonly Dictionary<int, KickReason> _kickList = new();
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
            _serverManager.OnRemoteConnectionState -= OnConnectionStateChanged;
            ExerussusLoopHelper.OnUpdate -= Update;
        }

        private void InitializeService()
        {
            if (_isStarted) return;
            
            _isStarted = true;
            _serverManager = gameObject.GetComponent<ServerManager>();
            _tugboat = gameObject.GetComponent<Tugboat>();
            _customAuthenticator = gameObject.GetComponent<ServiceCustomAuthenticator>();
            _networkManager = gameObject.GetComponent<NetworkManager>();
            _serverManager.OnRemoteConnectionState += OnConnectionStateChanged;
            ExerussusLoopHelper.OnUpdate -= Update;
            ExerussusLoopHelper.OnUpdate += Update;
            
            MicroservicesApi.RegisterService(this);
        }
        
        public async UniTask PullBroadcast(RunServer channel)
        {
            if (_isInitialized)
            {
                Debug.LogWarning($"FishNetServerMicroservice | Server already initialized.");    
                return;
            }
            
            if (channel.Authenticators == null)
            {
                Debug.LogError("FishNetServerMicroservice | Authenticators is null");
                return;
            }
            
            if (channel.Authenticators.Length == 0)
            {
                Debug.LogError("FishNetServerMicroservice | Authenticators is empty");
                return;
            }
            
            await ThreadGate.CreateJob(() => InitializeAuthenticators(channel.Address, channel.Port, channel.Authenticators)).Run().AsUniTask();
            await DelayedAction.Create(0.1f, () => { })
                .WithCondition(() => _isInitialized && _isStarted)
                .Run().AsUniTask();
        }

        public UniTask PullBroadcast(StartSession channel)
        {
            _serverManager.Broadcast(new SessionStateChanged(true));
            return UniTask.CompletedTask;
        }
        
        public void Update()
        {
            foreach (var (clientId, context) in _inProcess)
            {
                if (context.DataApproved)
                {
                    _approvedList.Add(clientId);
                    continue;
                }
                
                if (context.KickTime < Time.time)
                {
                    _kickList.Add(clientId, KickReason.UnusualActivity);
                }
            }
            
            if (_kickList.Count > 0)
            {
                foreach (var (clientId, kickReason) in _kickList)
                {
                    if (_inProcess.TryPop(clientId, out var context))
                    {
                        _serverManager.Broadcast(context.NetworkConnection, new AuthenticationResult(false), false);
                    } 
                    else continue;
                    context.NetworkConnection.Kick(kickReason, LoggingType.Common, "Authentication timeout.");
                }

                _kickList.Clear();
            }

            if (_approvedList.Count > 0)
            {
                foreach (var clientId in _approvedList)
                {
                    if (!_inProcess.TryPop(clientId, out var context)) continue;
                    _authenticated[clientId] = context;
                    ConnectionContext.Handle.SetAuthenticated(context, true);
                    _customAuthenticator.SetAuthResult(context.NetworkConnection, true);
                    context.Authenticator.OnAuthenticationSuccess(context);
                    
                    _serverManager.Broadcast(context.NetworkConnection, new AuthenticationResult(true), false);
                }

                _approvedList.Clear();
            }
        }
        
        private void OnConnectionStateChanged(NetworkConnection connection, RemoteConnectionStateArgs data)
        {
            if (data.ConnectionState == RemoteConnectionState.Started)
            {
                var context = new ConnectionContext();
            
                ConnectionContext.Handle.SetKickTime(context, Time.time + AuthenticationTimeout);
                ConnectionContext.Handle.SetNetworkConnection(context, connection);
                
                _inProcess.Add(connection.ClientId, context);
                Debug.Log($"Added client {connection.ClientId} to authentication queue.");
            }
            else
            {
                if (_inProcess.TryPop(connection.ClientId, out var context)) return;
                
                if (_authenticated.TryPop(connection.ClientId, out context))
                {
                    context.Authenticator.OnAuthenticatedClientDisconnected(context);
                }
            }
        }

        private void InitializeAuthenticators(string address, ushort port, IAuthenticator[] authenticators)
        {
            if (_tugboat == null)
            {
                Debug.LogError("FishNetServerMicroservice | Tugboat is null. Please add Tugboat to the same GameObject as ServerManager.");
                return;
            }

            foreach (var authenticator in authenticators)
            {
                if (_authenticators.ContainsKey(authenticator.GetType()))
                {
                    Debug.LogError($"FishNetServerMicroservice | Authenticator already exists for type {authenticator.GetType()}"); 
                }
                else
                {
                    var handle = new AuthenticatorHandle(authenticator);
                    _authenticators.Add(authenticator.GetType(), handle);
                    RegisterBroadcastInAuthenticator(authenticator, handle);
                    authenticator.OnInitialize();
                }
            }

            _tugboat.SetClientAddress(address);
            _tugboat.SetPort(port);
            _serverManager.OnServerConnectionState += OnServerConnectionStateChanged;
            _serverManager.StartConnection();
            
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
                foreach (var authenticator in _authenticators.Values)
                {
                    authenticator.Dispose?.Invoke();
                    authenticator.Authenticator.OnDestroy();
                }
                _authenticators.Clear();
                _inProcess.Clear();
                _serverManager.OnServerConnectionState -= OnServerConnectionStateChanged;
                _isStarted = false;
                _isInitialized = false;
            }
        }

        private void RegisterBroadcastInAuthenticator(object instance, AuthenticatorHandle handle)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));

            var type = instance.GetType();

            var pullerInterfaces = type.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IAuthenticator<>))
                .Reverse();

            var registerMethod = typeof(FishNetServerMicroservice).GetMethod(nameof(RegisterOnAuthData), BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (registerMethod == null) throw new InvalidOperationException("Метод RegisterOnAuthData не найден в FishNetServerMicroservice.");

            foreach (var itf in pullerInterfaces)
            {
                var genericArg = itf.GetGenericArguments()[0];

                if (!genericArg.IsValueType)
                {
                    Debug.LogError($"Authenticator generic type {genericArg} must be a struct (value type). Skipping.");
                    continue;
                }

                if (!typeof(IBroadcast).IsAssignableFrom(genericArg))
                {
                    Debug.LogError($"Authenticator generic type {genericArg} must implement IBroadcast. Skipping.");
                    continue;
                }

                try
                {
                    var closed = registerMethod.MakeGenericMethod(genericArg);
                    closed.Invoke(this, new object[] { instance, handle });
                }
                catch (TargetInvocationException tie)
                {
                    Debug.LogError($"Failed to register authenticator for broadcast {genericArg}: {tie.InnerException?.Message}");
                    Debug.LogException(tie.InnerException ?? tie);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Unexpected error registering authenticator for {genericArg}: {ex.Message}");
                    Debug.LogException(ex);
                }
            }
        }

        internal void RegisterOnAuthData<T>(IAuthenticator<T> authenticator, AuthenticatorHandle handle) where T : struct, IBroadcast
        {
            Action<NetworkConnection, T, Channel> action = OnAuthData;
            
            _serverManager.RegisterBroadcast(action, false);
            handle.Dispose = () => _serverManager.UnregisterBroadcast(action);
            return;
            
            void OnAuthData(NetworkConnection connection, T data, Channel channel)
            {            
                if (!_inProcess.TryGetValue(connection.ClientId, out var context))
                {
                    connection.Kick(KickReason.Unset, LoggingType.Warning, "Authentication not found.");
                    return;
                }

                ConnectionContext.Handle.SetAuthenticator(context, authenticator);
                ConnectionContext.Handle.SetKickTime(context, authenticator.DataCheckTimeout + Time.time);
            
                CheckAsync(authenticator, context, data).Forget();
            }
        }

        private async UniTask CheckAsync<T>(IAuthenticator<T> authenticator, ConnectionContext context, T data) where T : struct, IBroadcast
        {
            var result = await authenticator.OnDataCheck(context, data);
            if (result) ConnectionContext.Handle.SetDataApproved(context, true);
            else ConnectionContext.Handle.SetKickTime(context, 0f);
        }

        public enum ConnectionStart
        {
            Manual,
            Awake,
            Start,
        }
    }
}