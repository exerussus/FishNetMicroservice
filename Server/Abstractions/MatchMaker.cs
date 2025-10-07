using System.Threading;
using Cysharp.Threading.Tasks;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models;
using FishNet.Connection;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions
{
    public interface IMatchMaker<TConnection, TRoom, TUserMetaData, TRoomMetaData> : IMatchMaker 
        where TUserMetaData : IUserMetaData
        where TConnection : PlayerContext<TUserMetaData>, new()
        where TRoom : Room<TConnection, TUserMetaData, TRoomMetaData>
    {
        /// <summary> Распределение клиента по комнатам. </summary>
        public UniTask<TConnection> CreatePlayerContext(long userId, NetworkConnection connection, TUserMetaData metaData, CancellationToken ct);

        /// <summary> Получение комнаты по соединению.
        /// Если комната найдена, соединение валидно - isValidConnection = true, а room != null.
        /// Если комната не найдена и не может быть создана, то isValidConnection = false, а room = null.
        /// Если комната не найдена, но может быть создана, то isValidConnection = true, а room == null. </summary>
        public UniTask<long> GetRoomId(TConnection context, CancellationToken ct);
        
        /// <summary> Вызывается при создании новой комнаты. </summary>
        public virtual UniTask OnRoomCreated(TRoom room, CancellationToken ct) { return UniTask.CompletedTask; }
        /// <summary> Вызывается перед уничтожением комнаты. </summary>
        public virtual UniTask OnRoomDestroy(TRoom room, CancellationToken ct) { return UniTask.CompletedTask; }
        public virtual UniTask OnPlayerDisconnected(TConnection userData, CancellationToken ct) { return UniTask.CompletedTask; }
    }
    
    public interface IMatchMaker
    {
        public virtual void OnInitialize() {}
        public virtual void OnDestroy() {}
    }
}