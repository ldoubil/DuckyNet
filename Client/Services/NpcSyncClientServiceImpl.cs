using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// NPC 同步客户端服务 - 接收服务器广播的 NPC 事件
    /// </summary>
    public class NpcSyncClientServiceImpl : INpcSyncClientService
    {
        /// <summary>
        /// 接收其他客户端的 NPC 生成
        /// </summary>
        public void OnNpcSpawned(NpcSpawnData spawnData)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RemoteNpcSpawnedEvent(spawnData));
            }
        }

        /// <summary>
        /// 接收 NPC 批量位置更新
        /// </summary>
        public void OnNpcBatchTransform(NpcBatchTransformData batchData)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RemoteNpcBatchTransformEvent(batchData));
            }
        }

        /// <summary>
        /// 接收 NPC 销毁
        /// </summary>
        public void OnNpcDestroyed(NpcDestroyData destroyData)
        {
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RemoteNpcDestroyedEvent(destroyData));
            }
        }
    }
}
