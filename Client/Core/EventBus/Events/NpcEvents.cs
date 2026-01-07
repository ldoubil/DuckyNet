using DuckyNet.Shared.Data;
using DuckyNet.Shared.Events;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 远程 NPC 生成事件
    /// </summary>
    public class RemoteNpcSpawnedEvent : EventBase
    {
        public NpcSpawnData SpawnData { get; }

        public RemoteNpcSpawnedEvent(NpcSpawnData spawnData)
        {
            SpawnData = spawnData;
        }
    }

    /// <summary>
    /// 远程 NPC 批量位置更新事件
    /// </summary>
    public class RemoteNpcBatchTransformEvent : EventBase
    {
        public NpcBatchTransformData BatchData { get; }

        public RemoteNpcBatchTransformEvent(NpcBatchTransformData batchData)
        {
            BatchData = batchData;
        }
    }

    /// <summary>
    /// 远程 NPC 销毁事件
    /// </summary>
    public class RemoteNpcDestroyedEvent : EventBase
    {
        public NpcDestroyData DestroyData { get; }

        public RemoteNpcDestroyedEvent(NpcDestroyData destroyData)
        {
            DestroyData = destroyData;
        }
    }
}
