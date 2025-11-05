using System;

namespace DuckyNet.Shared.Data
{
    /// <summary>
    /// 血量同步数据
    /// </summary>
    [Serializable]
    public class HealthSyncData
    {
        /// <summary>
        /// 玩家ID (SteamId) - 由服务器填充
        /// </summary>
        public string SteamId { get; set; } = string.Empty;

        /// <summary>
        /// 当前血量
        /// </summary>
        public float CurrentHealth { get; set; }

        /// <summary>
        /// 最大血量
        /// </summary>
        public float MaxHealth { get; set; }

        /// <summary>
        /// 是否死亡
        /// </summary>
        public bool IsDead { get; set; }

        /// <summary>
        /// 时间戳 (用于排序和去重)
        /// </summary>
        public long Timestamp { get; set; }

        public HealthSyncData()
        {
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }

        public override string ToString()
        {
            return $"Player:{SteamId} Health:{CurrentHealth:F0}/{MaxHealth:F0} Dead:{IsDead}";
        }
    }
}

