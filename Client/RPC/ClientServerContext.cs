using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Context;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.RPC
{
    /// <summary>
    /// 客户端上下文实现（用于调用服务器）
    /// </summary>
    public class ClientServerContext : IClientContext
    {
        private readonly RpcClient _client;

        public string ClientId => GameContext.IsInitialized ? GameContext.Instance.PlayerManager.LocalPlayer.Info.SteamId : "local";
        public object NetPeer => _client;
        public ClientSessionState SessionState { get; internal set; } = ClientSessionState.Connected;
        public DateTime LastHeartbeat { get; internal set; } = DateTime.UtcNow;
        public bool IsDisconnected => !_client.IsConnected;
        public int ReconnectCount { get; internal set; } = 0;

        public ClientServerContext(RpcClient client)
        {
            _client = client;
        }

        public void Invoke<TService>(string methodName, params object[] parameters) where TService : class
        {
            _client.InvokeServer<TService>(methodName, parameters);
        }

        public async Task<TResult> InvokeAsync<TService, TResult>(string methodName, params object[] parameters) where TService : class
        {
            return await _client.InvokeServerAsync<TService, TResult>(methodName, parameters);
        }
    }
}

