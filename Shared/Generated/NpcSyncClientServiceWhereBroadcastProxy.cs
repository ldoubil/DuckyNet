using System;
using System.Threading.Tasks;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 广播代理 - 用于向满足条件的客户端发送消息（使用过滤器）
    /// </summary>
    public class NpcSyncClientServiceWhereBroadcastProxy : DuckyNet.Shared.Services.INpcSyncClientService
    {
        private readonly object _server;
        private readonly Func<string, bool> _predicate;
        public NpcSyncClientServiceWhereBroadcastProxy(object server, Func<string, bool> predicate)
        {
            _server = server;
            _predicate = predicate;
        }

        public void OnNpcSpawned(NpcSpawnData spawnData)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.INpcSyncClientService));
            method.Invoke(_server, new object[] { _predicate, "OnNpcSpawned", new object[] { spawnData } });
        }

        public void OnNpcBatchTransform(NpcBatchTransformData batchData)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.INpcSyncClientService));
            method.Invoke(_server, new object[] { _predicate, "OnNpcBatchTransform", new object[] { batchData } });
        }

        public void OnNpcDestroyed(NpcDestroyData destroyData)
        {
            var method = _server.GetType().GetMethod("BroadcastWhere").MakeGenericMethod(typeof(DuckyNet.Shared.Services.INpcSyncClientService));
            method.Invoke(_server, new object[] { _predicate, "OnNpcDestroyed", new object[] { destroyData } });
        }

    }
}
