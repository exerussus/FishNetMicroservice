using System.Threading;
using Cysharp.Threading.Tasks;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions
{
    public abstract class RoomBuilder<TRoom, TConnection, TMetaData> 
        where TRoom : Room<TConnection, TMetaData>
        where TConnection : PlayerContext<TMetaData>, new()
    {
        private IRoomReceiver<TRoom, TConnection, TMetaData> _receiver;
        
        internal void SetRefs(IRoomReceiver<TRoom, TConnection, TMetaData> receiver)
        {
            _receiver = receiver;
        }

        internal async UniTask DestroyRoom(TRoom room, CancellationToken ct)
        {
            await OnRoomDestroy(room, ct);
        }

        protected abstract UniTask<TRoom> CreateNewRoom(long roomId, CancellationToken ct);
        /// <summary> Вызывается при уничтожении комнаты. </summary>
        protected abstract UniTask OnRoomDestroy(TRoom context, CancellationToken ct);

        public async UniTask<TRoom> CreateRoom(long roomId, CancellationToken ct)
        {
            var room = await CreateNewRoom(roomId, ct);
            await _receiver.PushCreatedRoom(roomId, room, ct);
            return room;
        }
    }

    public interface IRoomReceiver<TRoom, TConnection, TMetaData> 
        where TRoom : Room<TConnection, TMetaData>
        where TConnection : PlayerContext<TMetaData>, new()
    {
        public UniTask PushCreatedRoom(long roomId, TRoom room, CancellationToken ct);
    }
}