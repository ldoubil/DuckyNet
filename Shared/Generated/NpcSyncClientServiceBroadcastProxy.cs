using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向所有客户端发送消息
    /// </summary>
    public class NpcSyncClientServiceBroadcastProxy : DuckyNet.Shared.Services.INpcSyncClientService
    {
        private readonly object _server;
        public NpcSyncClientServiceBroadcastProxy(object server) => _server = server;

        public void OnNpcSpawned(NpcSpawnData spawnData)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.INpcSyncClientService));
            method.Invoke(_server, new object[] { "OnNpcSpawned", new object[] { spawnData } });
        }

        public void OnNpcBatchTransform(NpcBatchTransformData batchData)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.INpcSyncClientService));
            method.Invoke(_server, new object[] { "OnNpcBatchTransform", new object[] { batchData } });
        }

        public void OnNpcDestroyed(NpcDestroyData destroyData)
        {
            var method = _server.GetType().GetMethod("BroadcastToAll").MakeGenericMethod(typeof(DuckyNet.Shared.Services.INpcSyncClientService));
            method.Invoke(_server, new object[] { "OnNpcDestroyed", new object[] { destroyData } });
        }

    }
}
