using FishNet.Broadcast;
using FishNet.Managing.Client;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Client.Abstractions
{
    public abstract class Connector<T> : IConnector where T : struct, IBroadcast
    {
        private T _data;

        public T Data => _data;

        void IConnector.PreStartConnection()
        {
            OnPreStartConnection();
        }

        void IConnector.PushBroadcast(ClientManager clientManager)
        {
            clientManager.Broadcast(_data);
        }
        
        void IConnector.StartConnection()
        {
            OnStartConnection();
        }
        
        void IConnector.EndConnection()
        {
            OnEndConnection();
        }
        
        void IConnector.AuthenticateSuccess()
        {
            OnAuthenticateSuccess();
        }
        
        void IConnector.AuthenticateFailed()
        {
            OnAuthenticateFailed();
        }
        
        void IConnector.SessionStarted()
        {
            OnSessionStarted();
        }
        
        void IConnector.SessionEnded()
        {
            OnSessionEnded();
        }

        /// <summary> Устанавливает данные для авторизации. </summary>
        public void SetAuthData(T data)
        {
            _data = data;
        }

        /// <summary> Вызывается перед подключением. </summary>
        protected abstract void OnPreStartConnection();
        
        /// <summary> Вызывается при подключении, но до авторизации. </summary>
        protected abstract void OnStartConnection();
        
        /// <summary> Вызывается при завершении подключения, независимо от авторизации и сессии. </summary>
        protected abstract void OnEndConnection();
        
        /// <summary> Вызывается при успешной авторизации, но до старта сессии. </summary>
        protected abstract void OnAuthenticateSuccess();
        
        /// <summary> Вызывается при неудачной авторизации. </summary>
        protected abstract void OnAuthenticateFailed();
        
        /// <summary> Вызывается при начале сессии. </summary>
        protected abstract void OnSessionStarted();
        
        /// <summary> Вызывается при завершении сессии. </summary>
        protected abstract void OnSessionEnded();
    }
    
    public interface IConnector
    {
        internal void PreStartConnection();
        internal void StartConnection();
        internal void EndConnection();
        internal void AuthenticateSuccess();
        internal void AuthenticateFailed();
        internal void SessionStarted();
        internal void SessionEnded();
        internal void PushBroadcast(ClientManager clientManager);
    }
}