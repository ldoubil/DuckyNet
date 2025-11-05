using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向指定客户端列表发送消息
    /// </summary>
    public class HealthSyncClientServiceClientsBroadcastProxy : DuckyNet.Shared.Services.IHealthSyncClientService
    {
        private readonly object _server;
        private readonly IEnumerable<string> _clientIds;
        public HealthSyncClientServiceClientsBroadcastProxy(object server, IEnumerable<string> clientIds)
        {
            _server = server;
            _clientIds = clientIds;
        }

        public void OnHealthSyncReceived(HealthSyncData healthData)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IHealthSyncClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnHealthSyncReceived", new object[] { healthData } });
        }

    }
}
