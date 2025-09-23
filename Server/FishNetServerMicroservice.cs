
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
        IChannelPuller<RunServer>
    {
        public ConnectionStart startType;
        public ServiceHandle Handle { get; set; }
        private ServerManager _serverManager;
        private ServerSettings _serverSettings;
        private Tugboat _tugboat;
        private NetworkManager _networkManager;
        private ServiceCustomAuthenticator _customAuthenticator;
        private readonly Dictionary<Type, GameHandle> _gameHandles = new ();
        
        private readonly Dictionary<int, ConnectionContext> _inProcess = new();
        private readonly Dictionary<int, ConnectionContext> _authenticated = new ();
        private readonly Dictionary<int, KickReason> _kickList = new();
        private readonly Dictionary<long, Room> _rooms = new();
        private readonly Dictionary<long, Room> _startedRooms = new();
        // roomId to Hashset of clients
        private readonly Dictionary<long, HashSet<long>> _connectedClients = new();
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

        internal async UniTask StartSession(long roomId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                Debug.LogError($"FishNetServerMicroservice | Room {roomId} not found.");
                return;
            }

            if (room.IsSessionStarted)
            {
                Debug.LogError($"FishNetServerMicroservice | Room {roomId} already started.");
                return;
            }
            
            if (room.IsSessionDone)
            {
                Debug.LogError($"FishNetServerMicroservice | Room {roomId} already stopped.");
                return;
            }
            
            room.SetSessionStarted(true);
            room.Broadcast(new SessionStateChanged(true));
            
            Debug.Log($"FishNetServerMicroservice | Starting session for room {roomId}");
            await room.Session.OnSessionStarted(room);
        }

        internal async UniTask StopSession(long roomId)
        {
            if (!_rooms.TryGetValue(roomId, out var room))
            {
                Debug.LogError($"FishNetServerMicroservice | Room {roomId} not found.");
                return;
            }

            if (!room.IsSessionStarted)
            {
                Debug.LogError($"FishNetServerMicroservice | Room {roomId} not started.");
                return;
            }
            
            if (room.IsSessionDone)
            {
                Debug.LogError($"FishNetServerMicroservice | Room {roomId} already stopped.");
                return;
            }
            
            room.SetSessionDone(true);
            room.Broadcast(new SessionStateChanged(false));
            await room.Session.OnSessionStopped(room);
            _rooms.Remove(room.UniqRoomId);
        }
        
        public void Update()
        {
            var time = Time.time;
            
            foreach (var (clientId, context) in _inProcess)
            {
                if (context.DataApproved)
                {
                    _approvedList.Add(clientId);
                    continue;
                }
                
                if (context.KickTime < time)
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
                    ConnectionContext.Handle.SetActive(context, true);
                    ProvideClientToRoom(context, false).Forget();
                }

                _approvedList.Clear();
            }
        }

        private async UniTask ProvideClientToRoom(ConnectionContext context, bool isReconnected)
        {
            var roomId = await context.MatchMaker.GetRoomId(context);
            
            if (_rooms.ContainsKey(roomId))
            {
                var room = _rooms[roomId];
                ConnectionContext.Handle.SetRoom(context, room);
                if (_connectedClients.TryGetValue(roomId, out var clients) && clients.Contains(context.UserId))
                {
                    await context.Session.OnReconnection(context, room);
                }
                else
                {
                    context.Room.AddClient(context);
                    context.Room.SetClientActive(context, true);
                    if (isReconnected) await context.Session.OnReconnection(context, room);
                    else await context.Session.OnNewConnection(context, room);
                }
            }
            else
            {
                var room = new Room(roomId, context.Session, _serverManager, this);
                _rooms[roomId] = room;
                ConnectionContext.Handle.SetRoom(context, room);
                context.Room.AddClient(context);
                context.Room.SetClientActive(context, true);
                await context.Session.OnRoomCreated(room);
                await context.Session.OnNewConnection(context, room);
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
                    if (context.Room != null)
                    {
                        context.Room.RemoveClient(context);
                        if (context.Session != null) context.Session.OnDisconnection(context, context.Room);
                    }
                }
            }
        }

        private void InitializeAuthenticators(ServerSettings settings)
        {
            _serverSettings = settings;
            
            if (_tugboat == null)
            {
                Debug.LogError("FishNetServerMicroservice | Tugboat is null. Please add Tugboat to the same GameObject as ServerManager.");
                return;
            }

            foreach (var (authenticator, matchMaker, session) in _serverSettings.Pipelines)
            {
                if (_gameHandles.ContainsKey(authenticator.GetType()))
                {
                    Debug.LogError($"FishNetServerMicroservice | Authenticator already exists for type {authenticator.GetType()}"); 
                }
                else
                {
                    var handle = new GameHandle(authenticator, matchMaker, session);
                    _gameHandles.Add(authenticator.GetType(), handle);
                    RegisterBroadcastInAuthenticator(authenticator, handle);
                    authenticator.OnInitialize();
                }
            }

            _tugboat.SetClientAddress(settings.Address);
            _tugboat.SetPort(settings.Port);
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
                foreach (var gameHandle in _gameHandles.Values)
                {
                    gameHandle.Dispose?.Invoke();
                    gameHandle.Authenticator.OnDestroy();
                }
                _gameHandles.Clear();
                _inProcess.Clear();
                if (_serverSettings != null)
                {
                    _serverSettings.OnServerStopped?.Invoke();
                    _serverSettings.OnServerStopped = null;
                    _serverSettings = null;
                }
                _serverManager.OnServerConnectionState -= OnServerConnectionStateChanged;
                _isStarted = false;
                _isInitialized = false;
            }
        }

        private void RegisterBroadcastInAuthenticator(object instance, GameHandle handle)
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

        internal void RegisterOnAuthData<T>(IAuthenticator<T> authenticator, GameHandle handle) where T : struct, IBroadcast
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

                ConnectionContext.Handle.SetPipeline(context, authenticator, handle.MatchMaker, handle.Session);
                ConnectionContext.Handle.SetKickTime(context, authenticator.DataCheckTimeout + Time.time);
            
                CheckAsync(authenticator, context, data).Forget();
            }
        }

        private async UniTask CheckAsync<T>(IAuthenticator<T> authenticator, ConnectionContext context, T data) where T : struct, IBroadcast
        {
            var result = await authenticator.OnDataCheck(context, data);
            if (result.isApproved) ConnectionContext.Handle.SetApprovedUserId(context, result.userId);
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