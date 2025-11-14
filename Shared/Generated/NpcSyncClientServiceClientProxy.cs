using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.RPC;
using DuckyNet.RPC.Context;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class NpcSyncClientServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public NpcSyncClientServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public void OnNpcSpawned(NpcSpawnData spawnData) => _ctx.Invoke<DuckyNet.Shared.Services.INpcSyncClientService>("OnNpcSpawned", spawnData);
        public void OnNpcBatchTransform(NpcBatchTransformData batchData) => _ctx.Invoke<DuckyNet.Shared.Services.INpcSyncClientService>("OnNpcBatchTransform", batchData);
        public void OnNpcDestroyed(NpcDestroyData destroyData) => _ctx.Invoke<DuckyNet.Shared.Services.INpcSyncClientService>("OnNpcDestroyed", destroyData);
    }
}
