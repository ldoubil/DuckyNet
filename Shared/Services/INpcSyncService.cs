using System.Threading.Tasks;
using DuckyNet.Shared.Data;
using DuckyNet.RPC;
using DuckyNet.RPC.Messages;
using DuckyNet.RPC.Context;

namespace DuckyNet.Shared.Services
{
    /// <summary>
    /// NPC 同步服务（服务器端）
    /// </summary>
    [RpcService("NpcSyncService")]
    public interface INpcSyncService
    {
        /// <summary>
        /// 通知服务器 NPC 生成
        /// </summary>
        [ClientToServer]
        Task NotifyNpcSpawned(IClientContext client, NpcSpawnData spawnData);

        /// <summary>
        /// 通知服务器 NPC 位置更新（单个）
        /// </summary>
        [ClientToServer]
        Task NotifyNpcTransform(IClientContext client, NpcTransformData transformData);

        /// <summary>
        /// 通知服务器 NPC 位置更新（批量）
        /// </summary>
        [ClientToServer]
        Task NotifyNpcBatchTransform(IClientContext client, NpcBatchTransformData batchData);

        /// <summary>
        /// 通知服务器 NPC 销毁
        /// </summary>
        [ClientToServer]
        Task NotifyNpcDestroyed(IClientContext client, NpcDestroyData destroyData);

        /// <summary>
        /// 请求场景内所有 NPC（进入场景时）
        /// </summary>
        [ClientToServer]
        Task<NpcSpawnData[]> RequestSceneNpcs(IClientContext client, string sceneName, string subSceneName);

        /// <summary>
        /// 请求单个 NPC 信息（按需加载）
        /// </summary>
        [ClientToServer]
        Task<NpcSpawnData?> RequestSingleNpc(IClientContext client, string npcId);
    }

    /// <summary>
    /// NPC 同步客户端服务（接收广播）
    /// </summary>
    [RpcService("NpcSyncClientService")]
    public interface INpcSyncClientService
    {
        /// <summary>
        /// 接收其他客户端的 NPC 生成
        /// </summary>
        [ServerToClient]
        void OnNpcSpawned(NpcSpawnData spawnData);

        /// <summary>
        /// 接收 NPC 位置更新（批量）
        /// </summary>
        [ServerToClient]
        void OnNpcBatchTransform(NpcBatchTransformData batchData);

        /// <summary>
        /// 接收 NPC 销毁
        /// </summary>
        [ServerToClient]
        void OnNpcDestroyed(NpcDestroyData destroyData);
    }
}

