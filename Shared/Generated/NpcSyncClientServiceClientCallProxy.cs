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
    public class NpcSyncClientServiceClientCallProxy : DuckyNet.Shared.Services.INpcSyncClientService
    {
        private readonly IClientContext _client;
        public NpcSyncClientServiceClientCallProxy(IClientContext client) => _client = client;

        public void OnNpcSpawned(NpcSpawnData spawnData) => _client.Invoke<DuckyNet.Shared.Services.INpcSyncClientService>("OnNpcSpawned", spawnData);

        public void OnNpcBatchTransform(NpcBatchTransformData batchData) => _client.Invoke<DuckyNet.Shared.Services.INpcSyncClientService>("OnNpcBatchTransform", batchData);

        public void OnNpcDestroyed(NpcDestroyData destroyData) => _client.Invoke<DuckyNet.Shared.Services.INpcSyncClientService>("OnNpcDestroyed", destroyData);

    }
}
