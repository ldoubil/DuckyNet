using System;

namespace DuckyNet.Shared.Data
{
    /// <summary>
    /// NPC 生成数据
    /// </summary>
    [Serializable]
    public class NpcSpawnData
    {
        /// <summary>
        /// NPC 全局唯一 ID（UUID）
        /// </summary>
        public string NpcId { get; set; } = "";

        /// <summary>
        /// 场景名称（主场景）
        /// </summary>
        public string SceneName { get; set; } = "";

        /// <summary>
        /// 子场景名称
        /// </summary>
        public string SubSceneName { get; set; } = "";

        /// <summary>
        /// NPC 类型名称（GameObject name）
        /// </summary>
        public string NpcType { get; set; } = "";

        /// <summary>
        /// 初始位置 X
        /// </summary>
        public float PositionX { get; set; }

        /// <summary>
        /// 初始位置 Y
        /// </summary>
        public float PositionY { get; set; }

        /// <summary>
        /// 初始位置 Z
        /// </summary>
        public float PositionZ { get; set; }

        /// <summary>
        /// 初始旋转 Y（简化为只同步朝向）
        /// </summary>
        public float RotationY { get; set; }

        /// <summary>
        /// 最大血量
        /// </summary>
        public float MaxHealth { get; set; }

        /// <summary>
        /// 生成时间戳（服务器时间）
        /// </summary>
        public long SpawnTimestamp { get; set; }
    }

    /// <summary>
    /// NPC 位置更新数据（轻量级，高频同步）
    /// </summary>
    [Serializable]
    public class NpcTransformData
    {
        /// <summary>
        /// NPC ID
        /// </summary>
        public string NpcId { get; set; } = "";

        /// <summary>
        /// 位置 X
        /// </summary>
        public float PositionX { get; set; }

        /// <summary>
        /// 位置 Y
        /// </summary>
        public float PositionY { get; set; }

        /// <summary>
        /// 位置 Z
        /// </summary>
        public float PositionZ { get; set; }

        /// <summary>
        /// 旋转 Y（朝向角度）
        /// </summary>
        public float RotationY { get; set; }
    }

    /// <summary>
    /// NPC 销毁数据
    /// </summary>
    [Serializable]
    public class NpcDestroyData
    {
        /// <summary>
        /// NPC ID
        /// </summary>
        public string NpcId { get; set; } = "";

        /// <summary>
        /// 销毁原因（0=正常销毁，1=死亡，2=场景卸载）
        /// </summary>
        public int Reason { get; set; }
    }

    /// <summary>
    /// NPC 批量位置更新（性能优化）
    /// </summary>
    [Serializable]
    public class NpcBatchTransformData
    {
        /// <summary>
        /// NPC 数量
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// NPC ID 列表
        /// </summary>
        public string[] NpcIds { get; set; } = Array.Empty<string>();

        /// <summary>
        /// 位置 X 列表
        /// </summary>
        public float[] PositionsX { get; set; } = Array.Empty<float>();

        /// <summary>
        /// 位置 Y 列表
        /// </summary>
        public float[] PositionsY { get; set; } = Array.Empty<float>();

        /// <summary>
        /// 位置 Z 列表
        /// </summary>
        public float[] PositionsZ { get; set; } = Array.Empty<float>();

        /// <summary>
        /// 旋转 Y 列表
        /// </summary>
        public float[] RotationsY { get; set; } = Array.Empty<float>();
    }
}

