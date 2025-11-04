using System;

namespace DuckyNet.Shared.Data
{
    /// <summary>
    /// 武器射击数据（客户端→服务器→其他客户端）
    /// 仅用于播放视觉和音效特效，不影响游戏逻辑
    /// </summary>
    [Serializable]
    public class WeaponFireData
    {
        /// <summary>开枪玩家的ID</summary>
        public string PlayerId { get; set; } = "";

        /// <summary>枪口位置X</summary>
        public float MuzzlePositionX { get; set; }
        
        /// <summary>枪口位置Y</summary>
        public float MuzzlePositionY { get; set; }
        
        /// <summary>枪口位置Z</summary>
        public float MuzzlePositionZ { get; set; }

        /// <summary>枪口方向X</summary>
        public float MuzzleDirectionX { get; set; }
        
        /// <summary>枪口方向Y</summary>
        public float MuzzleDirectionY { get; set; }
        
        /// <summary>枪口方向Z</summary>
        public float MuzzleDirectionZ { get; set; }

        /// <summary>是否使用消音器</summary>
        public bool IsSilenced { get; set; }

        /// <summary>武器类型ID（用于获取特效配置，可选）</summary>
        public int WeaponTypeId { get; set; }
    }
}

