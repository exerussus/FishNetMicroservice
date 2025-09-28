using Cysharp.Threading.Tasks;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions
{
    public interface IMatchMaker<TConnection, TRoom, TMetaData> : IMatchMaker 
        where TConnection : PlayerContext<TMetaData>, new()
        where TRoom : Room<TConnection, TMetaData>
    {
        /// <summary> Распределение клиента по комнатам. </summary>
        public UniTask<TConnection> CreatePlayerContext(long userId, TMetaData metaData);
        public UniTask<(bool isNewCreated, long roomId, TRoom room)> GetRoom(TConnection context);
        public UniTask OnRoomDestroy(TRoom context);
    }
    
    public interface IMatchMaker
    {
        public virtual void OnInitialize() {}
        public virtual void OnDestroy() {}
    }
}