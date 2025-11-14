
using System;
using Cysharp.Threading.Tasks;
using Exerussus._1Extensions.DelayedActionsFeature;
using Exerussus._1Extensions.LoopFeature;
using Exerussus._1Extensions.ThreadGateFeature;
using Exerussus.Microservices.Runtime;
using Exerussus.MicroservicesModules.FishNetMicroservice.Client.Models;
using Exerussus.MicroservicesModules.FishNetMicroservice.Global.Broadcasts;
using FishNet;
using FishNet.Broadcast;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using Sirenix.OdinInspector;
using UnityEngine;
using Channel = FishNet.Transporting.Channel;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Client
{
    [Serializable]
    public abstract class Connector<T> : IService, IDisposable
        where T : struct, IBroadcast
    {
        public ServiceHandle Handle { get; set; }
        
        private ClientManager _clientManager;
        private Tugboat _tugboat;
        private NetworkManager _networkManager;
        
        [ShowInInspector, ReadOnly] private T _data;
        [ShowInInspector, ReadOnly] private string _ip;
        [ShowInInspector, ReadOnly] private ushort _port;
        [ShowInInspector, ReadOnly] private RunResult _currentRunResult;
        [ShowInInspector, ReadOnly] private bool _isInitialized;
        [ShowInInspector, ReadOnly] private bool _isStarted;
        [ShowInInspector, ReadOnly] private bool _isConnectionInProcess;
        [ShowInInspector, ReadOnly] private bool _isStopClient;
        [ShowInInspector, ReadOnly] private bool _isConnectionStarted;
        [ShowInInspector, ReadOnly] private bool _isAuthenticated;
        [ShowInInspector, ReadOnly] private bool _isSessionStarted;

        public void Dispose()
        {
            ExerussusLoopHelper.OnUpdate -= Update;
            if (_clientManager != null)
            {
                _clientManager.StopConnection();
                _clientManager.UnregisterBroadcast<AuthenticationResult>(OnAuthenticationResult);
                _clientManager.UnregisterBroadcast<SessionStateChanged>(OnSessionStateChanged);
            }
            Handle.Unregister();
        }
        
        public async UniTask<(bool isSuccess, RunResult resultDetails)> RunClient(string address, ushort port, T data)
        {
            _data = data;
            _ip = address;
            _port = port;
            
            await ThreadGate.CreateJob(InitializeConnector).Run().AsUniTask();

            if (_isConnectionInProcess)
            {
                Debug.LogError($"FishNetClientMicroservice | Connection already in process with connector {GetType()}.");
                return (false, RunResult.AlreadyInProcess);
            }

            if (_isConnectionStarted)
            {
                Debug.LogWarning($"FishNetClientMicroservice | Connector already started with connector {GetType()}.");
                return (false, RunResult.AlreadyInProcess);
            }
            
            _isConnectionStarted = false;
            _isSessionStarted = false;
            _isStopClient = false;
            _isAuthenticated = false;
            _isConnectionInProcess = true;
            
            await ThreadGate.CreateJob(() => StartConnection(_ip, _port)).Run().AsUniTask();
            await DelayedAction.Create(0.05f, () => Debug.Log($"FishNetClientMicroservice | Client authenticated and completely started."))
                .WithValidation(() => _isInitialized && _isConnectionStarted && !_isStopClient)
                .WithCondition(() => _isStarted && _isAuthenticated)
                .Run().AsUniTask();
            
            _isConnectionInProcess = false;
            return (_currentRunResult == RunResult.Authenticated, _currentRunResult);
        }

        public async UniTask StopClient()
        {
            if (!_isConnectionStarted) return;
            
            _isStopClient = true;
            await DelayedAction.Create(0.1f, () => Debug.Log($"FishNetClientMicroservice | StopClient"))
                .WithCondition(() => !_isConnectionStarted)
                .Run().AsUniTask();
        }
        
        private void InitializeConnector()
        {
            if (_isInitialized) return;
            
            _isInitialized = true;
            _clientManager = InstanceFinder.ClientManager;
            _tugboat = _clientManager.GetComponent<Tugboat>();
            _networkManager = _clientManager.NetworkManager;
            
            _clientManager.RegisterBroadcast<AuthenticationResult>(OnAuthenticationResult);
            _clientManager.RegisterBroadcast<SessionStateChanged>(OnSessionStateChanged);
            
            ExerussusLoopHelper.OnUpdate -= Update;
            ExerussusLoopHelper.OnUpdate += Update;
            
            MicroservicesApi.RegisterService(this);
        }

        private void OnDestroy()
        {
        }

        private void Update()
        {
            if (_isStopClient)
            {
                _isStopClient = false;
                _clientManager.StopConnection();
            }
        }

        private void StartConnection(string address, ushort port)
        {
            _tugboat.SetClientAddress(address);
            _tugboat.SetPort(port);

            _clientManager.OnClientConnectionState += OnConnectionStateChanged;
            OnPreStartConnection();
            _clientManager.StartConnection();
            _isConnectionStarted = true;
        }

        private void OnAuthenticationResult(AuthenticationResult data, Channel _)
        {
            if (!_isInitialized) return;

            if (data.Success)
            {
                _isAuthenticated = true;
                _currentRunResult = RunResult.Authenticated;
                OnAuthenticateSuccess();
            }
            else
            {
                _isStopClient = true;
                OnAuthenticateFailed();
            }
        }

        private void OnSessionStateChanged(SessionStateChanged data, Channel _)
        {
            if (!_isInitialized) return;
            
            if (data.Started)
            {
                _isSessionStarted = true;
                OnSessionStarted();
            }
        }

        private void OnConnectionStateChanged(ClientConnectionStateArgs data)
        {
            if (data.ConnectionState == LocalConnectionState.Started)
            {
                _isStarted = true;
                Debug.Log($"FishNetClientMicroservice | Started connection to {_ip}:{_port} with connector {GetType().Name}.");
                _currentRunResult = RunResult.AuthenticationError;
                Debug.Log($"FishNetClientMicroservice | Sending authentication broadcast from connector {GetType().Name}.");
                _clientManager.Broadcast<T>(_data);
                OnStartConnection();
            }
            else if (data.ConnectionState == LocalConnectionState.Stopped)
            {
                Debug.Log($"FishNetClientMicroservice | Stopped connection to {_ip}:{_port} with connector {GetType().Name}.");
                if (_isSessionStarted) OnSessionEnded();
                OnEndConnection();
                _clientManager.OnClientConnectionState -= OnConnectionStateChanged;
                _isConnectionStarted = false;
                _isSessionStarted = false;
                _isStopClient = false;
                _isAuthenticated = false;
                _isStarted = false;
            }
            else if (data.ConnectionState == LocalConnectionState.Starting)
            {
                Debug.Log($"FishNetClientMicroservice | Starting connection to {_ip}:{_port} with connector {GetType().Name}.");
                _currentRunResult = RunResult.NotConnected;
            }
            else if (data.ConnectionState == LocalConnectionState.Stopping)
            {
                Debug.Log($"FishNetClientMicroservice | Stopping connection to {_ip}:{_port} with connector {GetType().Name}.");
            }
        }

        #region EVENTS

        /// <summary> Вызывается перед подключением. </summary>
        public virtual void OnPreStartConnection() {}

        /// <summary> Вызывается при подключении, но до авторизации. </summary>
        public virtual void OnStartConnection() {}

        /// <summary> Вызывается при завершении подключения, независимо от авторизации и сессии. </summary>
        public virtual void OnEndConnection() {}

        /// <summary> Вызывается при успешной авторизации, но до старта сессии. </summary>
        public virtual void OnAuthenticateSuccess() {}

        /// <summary> Вызывается при неудачной авторизации. </summary>
        public virtual void OnAuthenticateFailed() {}

        /// <summary> Вызывается при начале сессии. </summary>
        public virtual void OnSessionStarted() {}

        /// <summary> Вызывается при завершении сессии. </summary>
        public virtual void OnSessionEnded() {}

        #endregion

        public enum ConnectionStart
        {
            Manual,
            Awake,
            Start,
        }
    }
}