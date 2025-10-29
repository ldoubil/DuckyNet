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

                var localPlayer = GameContext.Instance.LocalPlayer;
                if (steamId == localPlayer.Info.SteamId)
                {
                    Debug.Log("[CharacterClientService] 跳过本地玩家外观更新");
                    return;
                }

                // 更新场景管理器中的玩家外观
                if (appearanceData != null)
                {
                    var sceneManager = GameContext.Instance.SceneManager;
                    sceneManager.UpdatePlayerAppearance(steamId, appearanceData);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterClientService] 处理外观更新失败: {ex.Message}");
            }
        }
    }
}

