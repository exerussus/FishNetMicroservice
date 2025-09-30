
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Exerussus._1Extensions.Scripts.Extensions;
using Exerussus._1Extensions.SignalSystem;
using Exerussus.MicroservicesModules.FishNetMicroservice.Global.Broadcasts;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.MonoBehaviours;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing.Logging;
using FishNet.Managing.Server;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;
using Channel = FishNet.Transporting.Channel;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public sealed class NetworkPipeline<TPlayerContext, TAuthenticator, TRoom, TMatchMaker, TSession, TAuthenticatorData, TMetaData> : 
        IPipeline, 
        IRoomReceiver<TRoom, TPlayerContext, TMetaData> 
        where TMetaData : IMetaData
        where TAuthenticatorData : struct, IBroadcast
        where TPlayerContext : PlayerContext<TMetaData>, new()
        where TRoom : Room<TPlayerContext, TMetaData>, new()
        where TAuthenticator : IAuthenticator<TAuthenticatorData, TMetaData>, new()
        where TMatchMaker : IMatchMaker<TPlayerContext, TRoom, TMetaData>, new()
        where TSession : ISession<TPlayerContext, TRoom, TMetaData>, new()
    {
        public NetworkPipeline(RoomBuilder<TRoom, TPlayerContext, TMetaData> roomBuilder, TAuthenticator authenticator = default, TMatchMaker matchMaker = default, TSession session = default)
        {
            _authenticator = authenticator;
            _matchMaker = matchMaker;
            _session = session;
            _roomBuilder = roomBuilder;
            _roomBuilder.SetRefs(this);
        }

        private ServiceCustomAuthenticator _customAuthenticator;
        private ServerManager _serverManager;
        private Tugboat _tugboat;
        private FishNetServerMicroservice _fishNetServerMicroservice;
        private CancellationTokenSource _cts;
        
        private TAuthenticator _authenticator;
        private TMatchMaker _matchMaker;
        private TSession _session;

        private readonly Dictionary<int, AuthenticationContext<TAuthenticatorData, TMetaData>> _inProcess = new();
        private readonly Dictionary<int, TPlayerContext> _authenticated = new ();
        private readonly Dictionary<int, IRoom> _roomsByNetworkConnectionId = new ();
        private readonly Dictionary<int, KickReason> _kickList = new();
        private readonly HashSet<int> _approvedList = new();
        private readonly HashSet<long> _emptyRooms = new();
        private readonly HashSet<long> _emptyRoomsToClear = new();
        private RoomBuilder<TRoom, TPlayerContext, TMetaData> _roomBuilder;
        
        internal readonly Dictionary<long, TPlayerContext> Connections = new();
        internal readonly Dictionary<long, TRoom> Rooms = new();

        public IAuthenticator Authenticator => _authenticator;
        public IMatchMaker MatchMaker => _matchMaker;
        public ISession Session => _session;
        
        public bool TryGetRoom(NetworkConnection connection, out IRoom room)
        {
            return _roomsByNetworkConnectionId.TryGetValue(connection.ClientId, out room);
        }

        public void Initialize(FishNetServerMicroservice fishNetMicroService)
        {
            _authenticator ??= new TAuthenticator();
            _matchMaker ??= new TMatchMaker();
            _session ??= new TSession();

            _cts = fishNetMicroService._cts;
            _customAuthenticator = fishNetMicroService.CustomAuthenticator;
            _serverManager = fishNetMicroService.ServerManager;
            _tugboat = _serverManager.GetComponent<Tugboat>();
            _fishNetServerMicroservice = fishNetMicroService;
            _serverManager.RegisterBroadcast<TAuthenticatorData>(OnAuthData, false);
            _authenticator.OnInitialize();
            _matchMaker.OnInitialize();
            _session.OnInitialize();
        }

        public void OnDestroy()
        {
            _serverManager.UnregisterBroadcast<TAuthenticatorData>(OnAuthData);
            _authenticator.OnDestroy();
            _matchMaker.OnDestroy();
            _session.OnDestroy();
        }
        
        private void OnAuthData(NetworkConnection connection, TAuthenticatorData data, Channel channel)
        {
            if (!_fishNetServerMicroservice.AwaitingAuthenticators.TryPop(connection.ClientId, out var process)) return;
            
            _fishNetServerMicroservice.SegregatedClients.Add(connection.ClientId, this);
            
            var context = new AuthenticationContext<TAuthenticatorData, TMetaData>
            {
                NetworkConnection = connection,
                KickTime = process.KickTime
            };
            
            _inProcess.Add(connection.ClientId, context);
            
            context.KickTime = _authenticator.DataCheckTimeout + Time.time;
            context.AuthData = data;
            CheckAsync(context).Forget();
        }
        
        private async UniTask CheckAsync(AuthenticationContext<TAuthenticatorData, TMetaData> context)
        {
            var result = await _authenticator.OnDataCheck(context.NetworkConnection, context.AuthData, _cts.Token);
            context.MetaData = result.metaData;
            
            if (result.isApproved)
            {
                context.UserId = result.metaData.UserId;
                context.DataApproved = true;
            }
            else context.KickTime = 0f;
        }

        public async UniTask PushCreatedRoom(long roomId, TRoom room, CancellationToken ct)
        {
            room.SetRoomRefs(roomId, _serverManager, this);
            Rooms[roomId] = room;
            await _matchMaker.OnRoomCreated(room, ct);
            await _session.OnRoomCreated(room, ct);
        }
        
        public bool TryGetRoom(long roomId, out IRoom room)
        {
            if (Rooms.TryGetValue(roomId, out var r))
            {
                room = r;
                return true;
            }
            
            room = null;
            return false;
        }

        public async UniTask StartSession(long roomId, CancellationToken ct)
        {
            if (!Rooms.TryGetValue(roomId, out var room))
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
            await _session.OnSessionStarted(room, ct);
            foreach (var playerContext in room.ActiveClients) PlayerContext<TMetaData>.Handle.SetSessionStarted(playerContext, true);
        }

        public async UniTask StopSession(long roomId, CancellationToken ct)
        {
            if (!Rooms.TryGetValue(roomId, out var room))
            {
                Debug.LogError($"FishNetServerMicroservice | Room {roomId} not found.");
                return;
            }
            
            if (room.IsSessionDone)
            {
                Debug.LogError($"FishNetServerMicroservice | Room {roomId} already stopped.");
                return;
            }

            if (!room.IsSessionStarted && room.IsSessionCancelled)
            {
                Debug.LogError($"FishNetServerMicroservice | Room {roomId} already cancelled.");
            }

            if (!room.IsSessionStarted)
            {
                room.SetSessionCancelled(true);
                await _session.OnSessionCancelled(room, ct);
            }
            else
            {
                room.SetSessionDone(true);
                room.Broadcast(new SessionStateChanged(false));
                await _session.OnSessionStopped(room, ct);
            }
            
            if (_session.KickOnSessionStop)
            {
                foreach (var playerContext in room.ActiveClients)
                {
                    await _session.OnDisconnectionAfterStop(playerContext, room, ct);
                }
                
                foreach (var playerContext in room.ActiveClients)
                {
                    _fishNetServerMicroservice.Tugboat.StopConnection(playerContext.NetworkConnection.ClientId, false);
                }
            }

            await _session.OnRoomDestroy(room, ct);
            await _matchMaker.OnRoomDestroy(room, ct);
            Rooms.Remove(room.UniqRoomId);
            await _roomBuilder.DestroyRoom(room, ct);
        }

        public void Update(float time)
        {
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
                    if (!_inProcess.TryPop(clientId, out var authContext)) continue;
                    _customAuthenticator.SetAuthResult(authContext.NetworkConnection, true);
                    _authenticator.OnAuthenticationSuccess(authContext.NetworkConnection, authContext.MetaData);
                    _serverManager.Broadcast(authContext.NetworkConnection, new AuthenticationResult(true), false);
                    ProvideClientToRoom(authContext, _cts.Token).Forget();
                }

                _approvedList.Clear();
            }
            
            if (_emptyRooms.Count > 0)
            {
                _emptyRoomsToClear.Clear();
                foreach (var roomId in _emptyRooms)
                {
                    if (Rooms.TryGetValue(roomId, out var room))
                    {
                        if (room.ActiveClients.Count > 0 || room.SessionStopTime < time) _emptyRoomsToClear.Add(roomId);
                    }
                    else
                    {
                        _emptyRoomsToClear.Add(roomId);
                    }
                }
            }

            if (_emptyRoomsToClear.Count > 0)
            {
                foreach (var roomId in _emptyRoomsToClear)
                {
                    _emptyRooms.Remove(roomId);
                    if (Rooms.TryGetValue(roomId, out var room) && room.ActiveClients.Count > 0) continue;
                    StopSession(roomId, _cts.Token).Forget();
                }
                
                _emptyRoomsToClear.Clear();
            }
        }

        private async UniTask ProvideClientToRoom(AuthenticationContext<TAuthenticatorData, TMetaData> authContext, CancellationToken ct)
        {
            var playerContext = await _matchMaker.CreatePlayerContext(authContext.UserId, authContext.MetaData, ct);
            PlayerContext<TMetaData>.Handle.SetMetaData(playerContext, authContext.MetaData);
            _authenticated[authContext.NetworkConnection.ClientId] = playerContext;
            
            Debug.Log($"Игрок {authContext.UserId} авторизован");
            
            PlayerContext<TMetaData>.Handle.SetUserId(playerContext, authContext.UserId);
            PlayerContext<TMetaData>.Handle.SetNetworkConnection(playerContext, authContext.NetworkConnection);
            var roomId = await _matchMaker.GetRoomId(playerContext, ct);
            
            if (!Rooms.TryGetValue(roomId, out var room))
            {
                Debug.LogWarning($"Невозможно найти комнату для игрока {authContext.UserId}.");
                _tugboat.StopConnection(authContext.NetworkConnection.ClientId, false);
                return;
            }
            
            PlayerContext<TMetaData>.Handle.SetRoom(playerContext, roomId);
            _roomsByNetworkConnectionId[authContext.NetworkConnection.ClientId] = room;
            room.AddClient(playerContext);
            await _session.OnNewConnection(playerContext, room, ct);
        }

        public void OnConnectionStateChanged(NetworkConnection connection, RemoteConnectionStateArgs data)
        {
            if (data.ConnectionState != RemoteConnectionState.Stopped) return;
            
            _roomsByNetworkConnectionId.Remove(connection.ClientId);
            
            if (_inProcess.TryPop(connection.ClientId, out _))
            {
                Debug.LogError($"FishNetServerMicroservice | Player {connection.ClientId} not found.");
                return;
            }
            if (!_authenticated.TryPop(connection.ClientId, out var context))
            {
                Debug.LogError($"FishNetServerMicroservice | Player {connection.ClientId} not found.");
                return;
            }
            
            Debug.Log($"Авторизация игрока {context.UserId} слетает.");
            
            if (!Rooms.TryGetValue(context.RoomId, out var room))
            {
                //Debug.LogError($"FishNetServerMicroservice | Room {context.RoomId} not found.");
                return;
            }
            
            room.RemoveClient(context);
            Debug.Log($"FishNetServerMicroservice | Player {context.UserId} disconnected from room {room.UniqRoomId}.");
            if (!room.IsSessionStarted)
            {
                _session.OnDisconnectionBeforeStart(context, room, _cts.Token);
            }
            else if (room.IsSessionStarted && !room.IsSessionDone)
            {
                _session.OnDisconnectionWhileProcess(context, room, _cts.Token);
            }
            else if (room.IsSessionDone)
            {
                _session.OnDisconnectionAfterStop(context, room, _cts.Token);
            }

            if (!room.IsSessionDone && !room.IsSessionCancelled)
            {
                room.SessionStopTime = Time.time + _session.MaxTimeOut;
                _emptyRooms.Add(room.UniqRoomId);
            }
        }
    }

    public interface IPipeline
    {
        public void Initialize(FishNetServerMicroservice fishNetMicroService);
        public void OnDestroy();
        public bool TryGetRoom(long roomId, out IRoom room);
        public UniTask StartSession(long roomId, CancellationToken ct);
        public UniTask StopSession(long roomId, CancellationToken ct);
        public void Update(float time);
        public void OnConnectionStateChanged(NetworkConnection connection, RemoteConnectionStateArgs data);
        public IAuthenticator Authenticator { get; }
        public IMatchMaker MatchMaker { get; }
        public ISession Session { get; }
        public bool TryGetRoom(NetworkConnection connection, out IRoom room);
    }

    public static class PipelineExtensions
    {
        public static ServerRelay CreateServerRelay(this IPipeline pipeline, Signal globalSignal, ServerManager serverManager, bool isLogsEnabled = false)
        {
            return new ServerRelay(globalSignal, serverManager, pipeline, isLogsEnabled);
        }
        public static ObserverRelay CreateObserverRelay(this IPipeline pipeline, Signal globalSignal, ServerManager serverManager, bool isLogsEnabled = false)
        {
            return new ObserverRelay(globalSignal, serverManager, pipeline, isLogsEnabled);
        }
    }
}