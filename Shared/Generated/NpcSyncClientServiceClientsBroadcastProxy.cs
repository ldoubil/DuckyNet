using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向指定客户端列表发送消息
    /// </summary>
    public class NpcSyncClientServiceClientsBroadcastProxy : DuckyNet.Shared.Services.INpcSyncClientService
    {
        private readonly object _server;
        private readonly IEnumerable<string> _clientIds;
        public NpcSyncClientServiceClientsBroadcastProxy(object server, IEnumerable<string> clientIds)
        {
            _server = server;
            _clientIds = clientIds;
        }

        public void OnNpcSpawned(NpcSpawnData spawnData)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.INpcSyncClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnNpcSpawned", new object[] { spawnData } });
        }

        public void OnNpcBatchTransform(NpcBatchTransformData batchData)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.INpcSyncClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnNpcBatchTransform", new object[] { batchData } });
        }

        public void OnNpcDestroyed(NpcDestroyData destroyData)
        {
            var method = _server.GetType().GetMethod("BroadcastToClients").MakeGenericMethod(typeof(DuckyNet.Shared.Services.INpcSyncClientService));
            method.Invoke(_server, new object[] { _clientIds, "OnNpcDestroyed", new object[] { destroyData } });
        }

    }
}
