using System;
using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Exerussus.Microservices.Runtime;
using Exerussus.Microservices.Runtime.Registration;
using FishNet;
using FishNet.Broadcast;
using FishNet.Connection;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Server
{
    public class FishNetRequestRegistratorService : IServiceInspector
    {
        public ServiceHandle Handle { get; set; }

        public Dictionary<Type, object> AsyncChannelsSubs { get; } = null;
        public Dictionary<int, RegisteredService> RegisteredServices { get; } = null;
        public Dictionary<int, HashSet<Type>> AsyncPushersToChannels { get; } = null;
        public Dictionary<Type, HashSet<int>> AsyncChannelsToPullers { get; } = null;
        public Dictionary<int, HashSet<Type>> PushersToChannels { get; } = null;
        public Dictionary<Type, HashSet<int>> ChannelsToPullers { get; } = null;
        public Dictionary<Type, object> ChannelsSubs { get; } = null;

        public void OnServiceRegistered(RegisteredService registeredService)
        {
            TryRegisterReceiver(registeredService.Service);
        }

        public void TryRegisterReceiver(object instance)
        {
            var instanceType = instance.GetType();

            foreach (var iface in instanceType.GetInterfaces())
            {
                if (!iface.IsGenericType) continue;

                var def = iface.GetGenericTypeDefinition();
                var args = iface.GetGenericArguments();

                if (args.Length != 2) continue;

                var requestType = args[0];
                var responseType = args[1];

                if (def == typeof(IRequestResponseReceiver<,>))
                {
                    RegisterSync(instance, requestType, responseType);
                }
                else if (def == typeof(IRequestResponseReceiverAsync<,>))
                {
                    RegisterAsync(instance, requestType, responseType);
                }
            }
        }

        private void RegisterSync(object instance, Type requestType, Type responseType)
        {
            var method = typeof(FishNetRequestRegistratorService)
                .GetMethod(nameof(RegisterGenericSync), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(requestType, responseType);
            method.Invoke(this, new[] { instance });
        }

        private void RegisterAsync(object instance, Type requestType, Type responseType)
        {
            var method = typeof(FishNetRequestRegistratorService)
                .GetMethod(nameof(RegisterGenericAsync), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(requestType, responseType);
            method.Invoke(this, new[] { instance, instance });
        }

        private void RegisterGenericSync<TRequest, TResponse>(object instance)
            where TRequest : struct, IBroadcast
            where TResponse : struct, IBroadcast
        {
            var receiver = (IRequestResponseReceiver<TRequest, TResponse>)instance;

            Action<NetworkConnection, TRequest, FishNet.Transporting.Channel> wrapper = (conn, request, channel) =>
            {
                var response = receiver.OnRequest(request);
                InstanceFinder.ServerManager.Broadcast(conn, response);
            };

            InstanceFinder.ServerManager.RegisterBroadcast(wrapper);
            if (instance is RegisteredService registeredService)
            {
                registeredService.DisposeActions += () => InstanceFinder.ServerManager.UnregisterBroadcast(wrapper);
            }
        }

        private void RegisterGenericAsync<TRequest, TResponse>(object instance)
            where TRequest : struct, IBroadcast
            where TResponse : struct, IBroadcast
        {
            var receiver = (IRequestResponseReceiverAsync<TRequest, TResponse>)instance;

            Action<NetworkConnection, TRequest, FishNet.Transporting.Channel> wrapper = (conn, request, _) =>
            {
                OnRequestAsync(conn, receiver, request).Forget();
            };

            InstanceFinder.ServerManager.RegisterBroadcast(wrapper);
            if (instance is RegisteredService registeredService)
            {
                registeredService.DisposeActions += () => InstanceFinder.ServerManager.UnregisterBroadcast(wrapper);
            }
        }

        public void OnServiceUnregistered(RegisteredService registeredService)
        {
            // всё снимается через DisposeActions
        }

        private static async UniTask OnRequestAsync<TRequest, TResponse>(NetworkConnection connection, IRequestResponseReceiverAsync<TRequest, TResponse> receiver, TRequest request)
            where TRequest : struct, IBroadcast
            where TResponse : struct, IBroadcast
        {
            var response = await receiver.OnRequest(request);
            InstanceFinder.ServerManager.Broadcast(connection, response);
        }
    }
    
    public interface IRequestResponseReceiver<TRequest, TResponse>
        where TRequest : struct, IBroadcast
        where TResponse : struct, IBroadcast
    {
        TResponse OnRequest(TRequest request);
    }

    public interface IRequestResponseReceiverAsync<TRequest, TResponse>
        where TRequest : struct, IBroadcast
        where TResponse : struct, IBroadcast
    {
        UniTask<TResponse> OnRequest(TRequest request);
    }
}
