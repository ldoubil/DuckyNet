using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向所有客户端发送消息
    /// </summary>
    public class AnimatorSyncClientServiceBroadcastProxy : DuckyNet.Shared.Services.IAnimatorSyncClientService
    {
        private readonly object _server;
        public AnimatorSyncClientServiceBroadcastProxy(object server) => _server = server;

        public void OnAnimatorStateUpdated(string steamId, AnimatorSyncData animatorData)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IAnimatorSyncClientService));
            method.Invoke(_server, new object[] { "OnAnimatorStateUpdated", new object[] { steamId, animatorData } });
        }

    }
}
