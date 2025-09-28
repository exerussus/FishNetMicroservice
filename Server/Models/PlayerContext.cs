
using System.Runtime.CompilerServices;
using Impl = System.Runtime.CompilerServices.MethodImplAttribute;
using FishNet.Connection;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public abstract class PlayerContext<TMetaData>
    {
        public long UserId { get; private set; }
        public NetworkConnection NetworkConnection { get; private set; }
        public long RoomId { get; private set; }
        public bool IsSessionStarted { get; private set; }
        public TMetaData MetaData { get; private set; }
        
        public static implicit operator NetworkConnection(PlayerContext<TMetaData> context) => context.NetworkConnection;
        
        public static class Handle
        {
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetRoom(PlayerContext<TMetaData> context, long room) => context.RoomId = room;
            
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetUserId(PlayerContext<TMetaData> context, long userId) => context.UserId = userId;
            
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetSessionStarted(PlayerContext<TMetaData> context, bool isSessionStarted) => context.IsSessionStarted = isSessionStarted;
            
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetNetworkConnection(PlayerContext<TMetaData> context, NetworkConnection networkConnection) => context.NetworkConnection = networkConnection;
            
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetMetaData(PlayerContext<TMetaData> context, TMetaData metaData) => context.MetaData = metaData;
        }
    }
}