using System;

namespace DuckyNet.Shared.Data
{
    /// <summary>
    /// 角色同步数据 - 包含位置、旋转和动画状态
    /// </summary>
    [Serializable]
    public class CharacterSyncData
    {
        /// <summary>
        /// 玩家 ID
        /// </summary>
        public string PlayerId { get; set; } = "";

        /// <summary>
        /// 场景ID/房间ID（用于场景过滤）
        /// </summary>
        public string SceneId { get; set; } = "";

        /// <summary>
        /// 时间戳（服务器时间）
        /// </summary>
        public long Timestamp { get; set; }

        // ========== 位置和旋转 ==========
        
        /// <summary>
        /// 位置 X
        /// </summary>
        public float PosX { get; set; }

        /// <summary>
        /// 位置 Y
        /// </summary>
        public float PosY { get; set; }

        /// <summary>
        /// 位置 Z
        /// </summary>
        public float PosZ { get; set; }

        /// <summary>
        /// 旋转（Y轴角度）
        /// </summary>
        public float RotationY { get; set; }

        // ========== 速度（用于预测） ==========
        
        /// <summary>
        /// 速度 X
        /// </summary>
        public float VelocityX { get; set; }

        /// <summary>
        /// 速度 Y
        /// </summary>
        public float VelocityY { get; set; }

        /// <summary>
        /// 速度 Z
        /// </summary>
        public float VelocityZ { get; set; }

        // ========== 动画参数 ==========
        
        /// <summary>
        /// 移动速度（动画参数）
        /// </summary>
        public float MoveSpeed { get; set; }

        /// <summary>
        /// 移动方向 X
        /// </summary>
        public float MoveDirX { get; set; }

        /// <summary>
        /// 移动方向 Y
        /// </summary>
        public float MoveDirY { get; set; }

        /// <summary>
        /// 是否冲刺
        /// </summary>
        public bool IsDashing { get; set; }

        /// <summary>
        /// 是否死亡
        /// </summary>
        public bool IsDead { get; set; }

        /// <summary>
        /// 手部状态（0=空手, 1=单手, 2=双手）
        /// </summary>
        public int HandState { get; set; }

        /// <summary>
        /// 是否在重装
        /// </summary>
        public bool IsReloading { get; set; }

        // ========== 动作触发 ==========
        
        /// <summary>
        /// 攻击触发计数（每次攻击递增）
        /// </summary>
        public int AttackTriggerCount { get; set; }

        /// <summary>
        /// 攻击索引
        /// </summary>
        public int AttackIndex { get; set; }

        public CharacterSyncData()
        {
        }
    }
}

