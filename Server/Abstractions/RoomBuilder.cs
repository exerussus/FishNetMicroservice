using System.Threading;
using Cysharp.Threading.Tasks;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions
{
    public abstract class RoomBuilder<TRoom, TConnection, TMetaUserData, TRoomMetaData> 
        where TMetaUserData : IUserMetaData
        where TRoom : Room<TConnection, TMetaUserData, TRoomMetaData>
        where TConnection : PlayerContext<TMetaUserData>, new()
    {
        private IRoomReceiver<TRoom, TConnection, TMetaUserData, TRoomMetaData> _receiver;
        
        internal void SetRefs(IRoomReceiver<TRoom, TConnection, TMetaUserData, TRoomMetaData> receiver)
        {
            _receiver = receiver;
        }

        internal async UniTask DestroyRoom(TRoom room, CancellationToken ct)
        {
            await OnRoomDestroy(room, ct);
        }

        protected abstract UniTask<TRoom> CreateNewRoom(long roomId, TRoomMetaData roomMetaData, CancellationToken ct);
        /// <summary> Вызывается при уничтожении комнаты. </summary>
        protected abstract UniTask OnRoomDestroy(TRoom context, CancellationToken ct);

        public async UniTask<TRoom> CreateRoom(long roomId, TRoomMetaData roomMetaData, CancellationToken ct)
        {
            var room = await CreateNewRoom(roomId, roomMetaData, ct);
            room.SetRoomMetaData(roomMetaData);
            await _receiver.PushCreatedRoom(roomId, room, ct);
            return room;
        }
    }

    public interface IRoomReceiver<TRoom, TConnection, TMetaUserData, TRoomMetaData> 
        where TMetaUserData : IUserMetaData
        where TRoom : Room<TConnection, TMetaUserData, TRoomMetaData>
        where TConnection : PlayerContext<TMetaUserData>, new()
    {
        public UniTask PushCreatedRoom(long roomId, TRoom room, CancellationToken ct);
    }
}