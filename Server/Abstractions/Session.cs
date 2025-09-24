using Cysharp.Threading.Tasks;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions
{
    public interface ISession<TConnection, TRoom> : ISession 
        where TConnection : PlayerContext, new()
        where TRoom : Room<TConnection>, new()
    {
        /// <summary> Максимальное время ожидания комнаты при отсутствии игроков без окончания сессии. </summary>
        public abstract float MaxTimeOut { get; }
        /// <summary> Кикать ли немедленно игроков при окончании сессии. </summary>
        public abstract bool KickOnSessionStop { get; }
        
        /// <summary> Вызывается при создании новой комнаты. </summary>
        public virtual UniTask OnRoomCreated(TRoom room) { return UniTask.CompletedTask; }
        
        /// <summary> Добавление нового подключения </summary>
        public virtual UniTask OnNewConnection(TConnection context, TRoom room) { return UniTask.CompletedTask; }

        /// <summary> Отключение игрока до старта сессии. </summary>
        public virtual UniTask OnDisconnectionBeforeStart(TConnection context, TRoom room) { return UniTask.CompletedTask; }

        /// <summary> Отключение игрока во время сессии. </summary>
        public virtual UniTask OnDisconnectionWhileProcess(TConnection context, TRoom room) { return UniTask.CompletedTask; }

        /// <summary> Отключение игрока во время окончания сессии. </summary>
        public virtual UniTask OnDisconnectionAfterStop(TConnection context, TRoom room) { return UniTask.CompletedTask; }
        
        /// <summary> Старт игровой сессии. </summary>
        public virtual UniTask OnSessionStarted(TRoom room) { return UniTask.CompletedTask; }
        /// <summary> Конец игровой сессии. </summary>
        public virtual UniTask OnSessionStopped(TRoom room) { return UniTask.CompletedTask; }
        /// <summary> Отмена игровой сессии. </summary>
        public virtual UniTask OnSessionCancelled(TRoom room) { return UniTask.CompletedTask; }
        /// <summary> Закрытие игровой сессии по любой причине. </summary>
        public virtual UniTask OnSessionClose(TRoom room) { return UniTask.CompletedTask; }
    }

    public interface ISession
    {
        public virtual void OnInitialize() {}
        public virtual void OnDestroy() {}
    }
}