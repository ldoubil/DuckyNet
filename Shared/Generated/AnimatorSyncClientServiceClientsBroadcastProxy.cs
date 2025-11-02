using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向指定客户端列表发送消息
    /// </summary>
    public class AnimatorSyncClientServiceClientsBroadcastProxy : DuckyNet.Shared.Services.IAnimatorSyncClientService
    {
        private readonly object _server;
        private readonly IEnumerable<string> _clientIds;
        public AnimatorSyncClientServiceClientsBroadcastProxy(object server, IEnumerable<string> clientIds)
        {
            _server = server;
            _clientIds = clientIds;
        }

        public void OnAnimatorStateUpdated(string steamId, AnimatorSyncData animatorData)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IAnimatorSyncClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnAnimatorStateUpdated", new object[] { steamId, animatorData } });
        }

    }
}
