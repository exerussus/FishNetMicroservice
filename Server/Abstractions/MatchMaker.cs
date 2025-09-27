using Cysharp.Threading.Tasks;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions
{
    public interface IMatchMaker<TConnection, TRoom> : IMatchMaker 
        where TConnection : PlayerContext, new()
        where TRoom : Room<TConnection>
    {
        /// <summary> Распределение клиента по комнатам. </summary>
        public UniTask<TConnection> CreatePlayerContext(long userId);
        public UniTask<(bool isNewCreated, long roomId, TRoom room)> GetRoomId(TConnection context);
        public UniTask OnRoomDestroy(TRoom context);
    }
    
    public interface IMatchMaker
    {
        public virtual void OnInitialize() {}
        public virtual void OnDestroy() {}
    }
}