using Cysharp.Threading.Tasks;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions
{
    public interface IMatchMaker
    {
        /// <summary> Распределение клиента по комнатам. </summary>
        public abstract UniTask<long> GetRoomId(ConnectionContext context);
    }
}