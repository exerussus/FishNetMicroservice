
using Cysharp.Threading.Tasks;
using Exerussus._1Extensions.DelayedActionsFeature;
using Exerussus._1Extensions.LoopFeature;
using Exerussus._1Extensions.ThreadGateFeature;
using Exerussus.Microservices.Runtime;
using Exerussus.MicroservicesModules.FishNetMicroservice.Client.Abstractions;
using Exerussus.MicroservicesModules.FishNetMicroservice.Client.API;
using Exerussus.MicroservicesModules.FishNetMicroservice.Client.Models;
using Exerussus.MicroservicesModules.FishNetMicroservice.Global.Broadcasts;
using FishNet.Managing;
using FishNet.Managing.Client;
using FishNet.Transporting;
using FishNet.Transporting.Tugboat;
using UnityEngine;
using Channel = FishNet.Transporting.Channel;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Client
{
    [RequireComponent(typeof(NetworkManager), typeof(ClientManager), typeof(Tugboat))]
    public class FishNetClientMicroservice : MonoBehaviour,
        IService,
        IChannelPuller<RunClient>,
        IChannelPuller<StopClient>
    {
        public ConnectionStart startType;
        public ServiceHandle Handle { get; set; }
        private ClientManager _clientManager;
        private Tugboat _tugboat;
        private NetworkManager _networkManager;
        private IConnector _currentConnector;

        private RunResult _currentRunResult;
        private bool _isStarted;
        private bool _isConnectionInProcess;
        private bool _isStopClient;
        private bool _isConnectionStarted;
        private bool _isAuthenticated;
        private bool _isSessionStarted;
        
        private void Awake()
        {
            if (startType == ConnectionStart.Awake) InitializeService();
        }
        
        private void Start()
        {
            if (startType == ConnectionStart.Start) InitializeService();
        }

        private void InitializeService()
        {
            if (_isStarted) return;
            
            _isStarted = true;
            _clientManager = gameObject.GetComponent<ClientManager>();
            _tugboat = gameObject.GetComponent<Tugboat>();
            _networkManager = gameObject.GetComponent<NetworkManager>();
            
            _clientManager.RegisterBroadcast<AuthenticationResult>(OnAuthenticationResult);
            _clientManager.RegisterBroadcast<SessionStateChanged>(OnSessionStateChanged);
            
            ExerussusLoopHelper.OnUpdate -= Update;
            ExerussusLoopHelper.OnUpdate += Update;
            
            MicroservicesApi.RegisterService(this);
        }

        private void OnDestroy()
        {
            _clientManager.UnregisterBroadcast<AuthenticationResult>(OnAuthenticationResult);
            _clientManager.UnregisterBroadcast<SessionStateChanged>(OnSessionStateChanged);
        }

        private void Update()
        {
            if (_isStopClient)
            {
                _isStopClient = false;
                _clientManager.StopConnection();
            }
        }

        public async UniTask PullBroadcast(RunClient channel)
        {
            if (_isConnectionInProcess)
            {
                Debug.LogError($"FishNetClientMicroservice | Connection already in process with connector {_currentConnector.GetType()}.");
                return;
            }
            
            if (channel.Connector == null)
            {
                Debug.LogError($"FishNetClientMicroservice | Connector is null");
                return;
            }

            _isConnectionStarted = false;
            _isSessionStarted = false;
            _isStopClient = false;
            _isAuthenticated = false;
            _isConnectionInProcess = true;
            _currentConnector = channel.Connector;
            
            await ThreadGate.CreateJob(() => StartConnection(channel.Address, channel.Port)).Run().AsUniTask();
            await DelayedAction.Create(0.05f, () => Debug.Log($"FishNetClientMicroservice | Client authenticated and completely started."))
                .WithValidation(() => _isConnectionStarted && !_isStopClient)
                .WithCondition(() => _isStarted && _isAuthenticated)
                .Run().AsUniTask();
            
            RunClientResponse.Handle.SetResult(channel.Response, _currentRunResult);
            _isConnectionInProcess = false;
        }

        public async UniTask PullBroadcast(StopClient channel)
        {
            if (!_isConnectionStarted) return;
            
            _isStopClient = true;
            await DelayedAction.Create(0.1f, () => Debug.Log($"FishNetClientMicroservice | StopClient"))
                .WithCondition(() => !_isConnectionStarted)
                .Run().AsUniTask();
        }

        private void StartConnection(string address, ushort port)
        {
            _tugboat.SetClientAddress(address);
            _tugboat.SetPort(port);

            _clientManager.OnClientConnectionState += OnConnectionStateChanged;
            _currentConnector.PreStartConnection();
            _clientManager.StartConnection();
            _currentConnector.StartConnection();
            _isConnectionStarted = true;
        }

        private void OnAuthenticationResult(AuthenticationResult data, Channel _)
        {
            if (!_isStarted) return;
            if (_currentConnector == null)
            {
                Debug.LogError($"FishNetClientMicroservice | Connector is null.");
                return;
            }
            
            if (data.Success)
            {
                _isAuthenticated = true;
                _currentRunResult = RunResult.Authenticated;
                _currentConnector.AuthenticateSuccess();
            }
            else
            {
                _isStopClient = true;
                _currentConnector.AuthenticateFailed();
            }
        }

        private void OnSessionStateChanged(SessionStateChanged data, Channel _)
        {
            if (!_isStarted) return;
            if (_currentConnector == null)
            {
                Debug.LogError($"FishNetClientMicroservice | Connector is null.");
                return;
            }
            
            if (data.Started)
            {
                _isSessionStarted = true;
                _currentConnector.SessionStarted();
            }
        }

        private void OnConnectionStateChanged(ClientConnectionStateArgs data)
        {
            if (data.ConnectionState == LocalConnectionState.Started)
            {
                _currentRunResult = RunResult.AuthenticationError;
                _currentConnector.PushBroadcast(_clientManager);
            }
            else if (data.ConnectionState == LocalConnectionState.Stopped)
            {
                if (_isSessionStarted) _currentConnector.SessionEnded();
                _currentConnector.EndConnection();
                _clientManager.OnClientConnectionState -= OnConnectionStateChanged;
                _currentConnector = null;
                _isConnectionStarted = false;
                _isSessionStarted = false;
                _isStopClient = false;
                _isAuthenticated = false;
            }
            else if (data.ConnectionState == LocalConnectionState.Starting)
            {
                _currentRunResult = RunResult.NotConnected;
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