using Cysharp.Threading.Tasks;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions
{
    public interface ISession
    {
        /// <summary> Максимальное время ожидание клиента перед киком после потери соединения. </summary>
        public abstract float MaxTimeOut { get; }
        
        /// <summary> Вызывается при создании новой комнаты. </summary>
        public UniTask OnRoomCreated(Room room); 
        /// <summary> Добавление нового подключения </summary>
        public UniTask OnNewConnection(ConnectionContext context, Room room);
        /// <summary> Потеря соединения с клиентом без явного отключения, или кика. </summary>
        public UniTask OnConnectionLost(ConnectionContext context, Room room);
        /// <summary> Переподключение клиента после потери соединения. </summary>
        public UniTask OnReconnection(ConnectionContext context, Room room);
        /// <summary> Явное отключение клиента при выходе, или кике. </summary>
        public UniTask OnDisconnection(ConnectionContext context, Room room);
        /// <summary> Старт игровой сессии. </summary>
        public UniTask OnSessionStarted(Room room);
        /// <summary> Конец игровой сессии. </summary>
        public UniTask OnSessionStopped(Room room);
    }
}