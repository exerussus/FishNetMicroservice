
using System.Runtime.CompilerServices;
using Impl = System.Runtime.CompilerServices.MethodImplAttribute;
using Exerussus.MicroservicesModules.FishNetMicroservice.Server.Abstractions;
using FishNet.Connection;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server.Models
{
    public class ConnectionContext
    {
        internal ConnectionContext() { }
        public string NickName { get; private set; }
        public long UserId { get; private set; }
        public NetworkConnection NetworkConnection { get; private set; }
        public IAuthenticator Authenticator { get; private set; }
        public IMatchMaker MatchMaker { get; private set; }
        public ISession Session { get; private set; }
        public Room Room { get; private set; }
        public bool IsAuthenticated { get; private set; }
        public bool IsActive { get; private set; }
        public bool IsSessionStarted { get; private set; }
        public bool DataApproved { get; private set; }
        public float KickTime { get; private set; }
        public object Data { get; private set; }

        public static implicit operator NetworkConnection(ConnectionContext context) => context.NetworkConnection;
        
        public static class Handle
        {
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetNickName(ConnectionContext context, string nickName) => context.NickName = nickName;
            
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetAuthenticated(ConnectionContext context, bool isAuthenticated) => context.IsAuthenticated = isAuthenticated;
            
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetRoom(ConnectionContext context, Room room) => context.Room = room;
            
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetSessionStarted(ConnectionContext context, bool isSessionStarted) => context.IsSessionStarted = isSessionStarted;
            
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetApprovedUserId(ConnectionContext context, long userId)
            {
                context.UserId = userId;
                context.DataApproved = true;
            }

            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetKickTime(ConnectionContext context, float kickTime) => context.KickTime = kickTime;
            
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetNetworkConnection(ConnectionContext context, NetworkConnection networkConnection) => context.NetworkConnection = networkConnection;
            
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetPipeline(ConnectionContext context, IAuthenticator authenticator, IMatchMaker matchMaker, ISession session)
            {
                context.Authenticator = authenticator;
                context.MatchMaker = matchMaker;
                context.Session = session;
            }

            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetData<T>(ConnectionContext context, T data) => context.Data = data;
            
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static T GetData<T>(ConnectionContext context) => (T)context.Data;
            
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static void SetActive(ConnectionContext context, bool isActive) => context.IsActive = isActive;
            
            [Impl(MethodImplOptions.AggressiveInlining)]
            internal static bool TryGetData<T>(ConnectionContext context, out T data)
            {
                if (context.Data == null)
                {
                    data = default;
                    return false;
                }

                if (context.Data is not T castData)
                {
                    data = default;
                    return false;
                }

                data = castData;
                return true;
            }
        }
    }
}