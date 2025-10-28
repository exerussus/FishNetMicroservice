using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet;
using FishNet.Broadcast;
using FishNet.Managing.Client;
using Channel = FishNet.Transporting.Channel;

namespace Exerussus.MicroservicesModules.FishNetMicroservice.Client
{
    public static class FishnetQoL
    {
        private static readonly Dictionary<Type, object> RequestProcesses = new();
        
        public static async UniTask<TResponse> RequestAsync<TRequest, TResponse>(TRequest request = default, CancellationToken cancellationToken = default) 
            where TRequest : struct, IBroadcast
            where TResponse : struct, IBroadcast
        {
            var process = CreateProcess<TResponse>();
            process.Register(request);
            await UniTask.WaitUntil(() => process.IsDone || cancellationToken.IsCancellationRequested, cancellationToken: cancellationToken);
            var response = process.Response;
            ReleaseProcess(process);
            return response;
        }

        private static RequestProcess<T> CreateProcess<T>() where T : struct, IBroadcast
        {
            var type = typeof(T);
            if (RequestProcesses.TryGetValue(type, out var rawQueue))
            {
                var queue = (ConcurrentQueue<RequestProcess<T>>)rawQueue;
                return queue.TryDequeue(out var process) ? process : new RequestProcess<T>();
            }
            else
            {
                var process = new RequestProcess<T>();
                RequestProcesses[type] = new ConcurrentQueue<RequestProcess<T>>();
                return process;
            }
        }
        
        private static void ReleaseProcess<T>(RequestProcess<T> process) where T : struct, IBroadcast
        {
            process.IsDone = false;
            process.Response = default;

            var type = typeof(T);
            if (RequestProcesses.TryGetValue(type, out var rawQueue))
            {
                var queue = (ConcurrentQueue<RequestProcess<T>>)rawQueue;
                queue.Enqueue(process);
            }
        }

        private class RequestProcess<T> where T : struct, IBroadcast
        {
            public T Response { get; set; }
            public bool IsDone { get; set; }
            
            private ClientManager ClientManager { get; set; }
            private Action UnregisterAction { get; set; }
            
            public void Register<TRequest>(TRequest request) where TRequest : struct, IBroadcast
            {
                ClientManager = InstanceFinder.ClientManager;
                ClientManager.Broadcast(request);
                ClientManager.RegisterBroadcast<T>(OnBroadcastEvent);
                UnregisterAction = () => ClientManager.UnregisterBroadcast<T>(OnBroadcastEvent);
            }
            
            private void OnBroadcastEvent(T data, Channel channel)
            {
                UnregisterAction?.Invoke();
                UnregisterAction = null;
                Response = data;
                IsDone = true;
            }
        }
    }
}