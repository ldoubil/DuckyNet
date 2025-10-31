using System;
using UnityEngine;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 角色客户端服务实现
    /// </summary>
    public class CharacterClientServiceImpl : ICharacterClientService
    {
        public void OnPlayerAppearanceUpdated(string steamId, byte[] appearanceData)
        {
            try
            {
                Debug.Log($"[CharacterClientService] 收到玩家外观更新: {steamId} ({appearanceData?.Length ?? 0} bytes)");

                if (!GameContext.IsInitialized)
                {
                    Debug.LogWarning("[CharacterClientService] GameContext 未初始化");
                    return;
                }

                var localPlayer = GameContext.Instance.PlayerManager.LocalPlayer;
                if (steamId == localPlayer.Info.SteamId)
                {
                    Debug.Log("[CharacterClientService] 跳过本地玩家外观更新");
                    return;
                }

                // 发布外观更新事件（而不是直接调用 SceneManager）
                if (appearanceData != null)
                {
                    GameContext.Instance.EventBus.Publish(new PlayerAppearanceUpdatedEvent(steamId, appearanceData));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterClientService] 处理外观更新失败: {ex.Message}");
            }
        }
    }
}

