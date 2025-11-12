using System;
using System.Linq;
using System.Threading.Tasks;
using DuckyNet.Shared.RPC;
using DuckyNet.Shared.Data;
namespace DuckyNet.Shared.Services.Generated
{
    /// <summary>
    /// 客户端代理 - 用于调用服务器方法
    /// </summary>
    public class NpcSyncServiceClientProxy
    {
        private readonly IClientContext _ctx;
        public NpcSyncServiceClientProxy(IClientContext ctx) => _ctx = ctx;

        public Task NotifyNpcSpawned(NpcSpawnData spawnData) => _ctx.InvokeAsync<DuckyNet.Shared.Services.INpcSyncService, object>("NotifyNpcSpawned", spawnData);
        public Task NotifyNpcTransform(NpcTransformData transformData) => _ctx.InvokeAsync<DuckyNet.Shared.Services.INpcSyncService, object>("NotifyNpcTransform", transformData);
        public Task NotifyNpcBatchTransform(NpcBatchTransformData batchData) => _ctx.InvokeAsync<DuckyNet.Shared.Services.INpcSyncService, object>("NotifyNpcBatchTransform", batchData);
        public Task NotifyNpcDestroyed(NpcDestroyData destroyData) => _ctx.InvokeAsync<DuckyNet.Shared.Services.INpcSyncService, object>("NotifyNpcDestroyed", destroyData);
        public Task<NpcSpawnData[]> RequestSceneNpcs(string sceneName, string subSceneName) => _ctx.InvokeAsync<DuckyNet.Shared.Services.INpcSyncService, NpcSpawnData[]>("RequestSceneNpcs", sceneName, subSceneName);
        public Task<NpcSpawnData> RequestSingleNpc(string npcId) => _ctx.InvokeAsync<DuckyNet.Shared.Services.INpcSyncService, NpcSpawnData>("RequestSingleNpc", npcId);
    }
}
