using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向所有客户端发送消息
    /// </summary>
    public class HealthSyncClientServiceBroadcastProxy : DuckyNet.Shared.Services.IHealthSyncClientService
    {
        private readonly object _server;
        public HealthSyncClientServiceBroadcastProxy(object server) => _server = server;

        public void OnHealthSyncReceived(HealthSyncData healthData)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.IHealthSyncClientService));
            method.Invoke(_server, new object[] { "OnHealthSyncReceived", new object[] { healthData } });
        }

    }
}
