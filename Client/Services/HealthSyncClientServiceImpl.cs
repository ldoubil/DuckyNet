using System;
using UnityEngine;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.Data;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 血量同步客户端服务实现类
    /// 负责处理服务器向客户端发送的血量同步数据
    /// </summary>
    public class HealthSyncClientServiceImpl : IHealthSyncClientService
    {
        /// <summary>
        /// 接收其他玩家的血量同步数据回调方法
        /// 由服务器调用，当房间/场景内的其他玩家血量变化时触发
        /// </summary>
        /// <param name="healthData">其他玩家的血量同步数据</param>
        public void OnHealthSyncReceived(HealthSyncData healthData)
        {
            try
            {
                if (!GameContext.IsInitialized)
                {
                    Debug.LogWarning("[HealthSyncClientService] GameContext 未初始化，无法处理血量同步");
                    return;
                }

                Debug.Log($"[HealthSyncClientService] 💚 收到玩家 {healthData.SteamId} 血量同步: {healthData.CurrentHealth:F0}/{healthData.MaxHealth:F0} (死亡:{healthData.IsDead})");

                // 通过全局 EventBus 发布血量同步事件
                GameContext.Instance.EventBus.Publish(new RemotePlayerHealthSyncEvent(healthData));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HealthSyncClientService] 处理血量同步数据失败: {ex.Message}");
            }
        }
    }
}

