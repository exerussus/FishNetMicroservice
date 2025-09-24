using Cysharp.Threading.Tasks;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models;


namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions
{
    public interface IMatchMaker<TConnection> : IMatchMaker where TConnection : PlayerContext, new()
    {
        /// <summary> Распределение клиента по комнатам. </summary>
        public UniTask<TConnection> CreatePlayerContext(long userId);
        public UniTask<long> GetRoomId(PlayerContext context);
    }
    
    public interface IMatchMaker
    {
        public virtual void OnInitialize() {}
        public virtual void OnDestroy() {}
    }
}