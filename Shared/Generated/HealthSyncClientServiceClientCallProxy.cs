using System;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 单客户端调用代理 - 用于向特定客户端发送消息
    /// </summary>
    public class HealthSyncClientServiceClientCallProxy : DuckyNet.Shared.Services.IHealthSyncClientService
    {
        private readonly IClientContext _client;
        public HealthSyncClientServiceClientCallProxy(IClientContext client) => _client = client;

        public void OnHealthSyncReceived(HealthSyncData healthData) => _client.Invoke<DuckyNet.Shared.Services.IHealthSyncClientService>("OnHealthSyncReceived", healthData);

    }
}
