using System;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 单客户端调用代理 - 用于向特定客户端发送消息
    /// </summary>
    public class AnimatorSyncClientServiceClientCallProxy : DuckyNet.Shared.Services.IAnimatorSyncClientService
    {
        private readonly IClientContext _client;
        public AnimatorSyncClientServiceClientCallProxy(IClientContext client) => _client = client;

        public void OnAnimatorStateUpdated(string steamId, AnimatorSyncData animatorData) => _client.Invoke<DuckyNet.Shared.Services.IAnimatorSyncClientService>("OnAnimatorStateUpdated", steamId, animatorData);

    }
}
