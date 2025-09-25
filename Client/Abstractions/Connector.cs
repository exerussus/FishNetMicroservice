using FishNet.Broadcast;
using FishNet.Managing.Client;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Client.Abstractions
{
    public abstract class Connector<T> : IConnector where T : struct, IBroadcast
    {
        private T _data;

        public T Data => _data;

        void IConnector.PushBroadcast(ClientManager clientManager)
        {
            clientManager.Broadcast(_data);
        }
        
        /// <summary> Устанавливает данные для авторизации. </summary>
        public void SetAuthData(T data)
        {
            _data = data;
        }
        
        /// <summary> Вызывается перед подключением. </summary>
        public virtual void PreStartConnection() {}
        /// <summary> Вызывается при подключении, но до авторизации. </summary>
        public virtual void StartConnection() {}
        /// <summary> Вызывается при завершении подключения, независимо от авторизации и сессии. </summary>
        public virtual void EndConnection() {}
        /// <summary> Вызывается при успешной авторизации, но до старта сессии. </summary>
        public virtual void AuthenticateSuccess() {}
        /// <summary> Вызывается при неудачной авторизации. </summary>
        public virtual void AuthenticateFailed() {}
        /// <summary> Вызывается при начале сессии. </summary>
        public virtual void SessionStarted() {}
        /// <summary> Вызывается при завершении сессии. </summary>
        public virtual void SessionEnded() {}
    }
    
    public interface IConnector
    {
        /// <summary> Вызывается перед подключением. </summary>
        public virtual void PreStartConnection() {}
        /// <summary> Вызывается при подключении, но до авторизации. </summary>
        public virtual void StartConnection() {}
        /// <summary> Вызывается при завершении подключения, независимо от авторизации и сессии. </summary>
        public virtual void EndConnection() {}
        /// <summary> Вызывается при успешной авторизации, но до старта сессии. </summary>
        public virtual void AuthenticateSuccess() {}
        /// <summary> Вызывается при неудачной авторизации. </summary>
        public virtual void AuthenticateFailed() {}
        /// <summary> Вызывается при начале сессии. </summary>
        public virtual void SessionStarted() {}
        /// <summary> Вызывается при завершении сессии. </summary>
        public virtual void SessionEnded() {}
        internal void PushBroadcast(ClientManager clientManager);
    }
}