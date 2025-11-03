using System;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.EventBus.Events;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 角色外观客户端服务实现
    /// 接收服务器推送的外观数据并应用到角色
    /// </summary>
    public class CharacterAppearanceClientServiceImpl : ICharacterAppearanceClientService
    {
        public void OnAppearanceReceived(string steamId, CharacterAppearanceData appearanceData)
        {
            Debug.Log($"[CharacterAppearanceClientService] 📦 收到服务器推送的外观数据: SteamId={steamId}");
            Debug.Log($"[CharacterAppearanceClientService] 外观数据详情 - HeadScale: {appearanceData.HeadSetting.ScaleX}, Parts: {appearanceData.Parts.Length}");

            // 通过事件总线发布外观接收事件
            if (GameContext.IsInitialized)
            {
                Debug.Log($"[CharacterAppearanceClientService] ✅ 正在发布CharacterAppearanceReceivedEvent事件: {steamId}");
                GameContext.Instance.EventBus.Publish(new CharacterAppearanceReceivedEvent(steamId, appearanceData));
                Debug.Log($"[CharacterAppearanceClientService] ✅ 事件已发布到EventBus");
            }
            else
            {
                Debug.LogError("[CharacterAppearanceClientService] ❌ GameContext未初始化，无法发布事件！");
            }
        }
    }

    /// <summary>
    /// 角色外观接收事件
    /// </summary>
    public class CharacterAppearanceReceivedEvent
    {
        public string SteamId { get; }
        public CharacterAppearanceData AppearanceData { get; }

        public CharacterAppearanceReceivedEvent(string steamId, CharacterAppearanceData appearanceData)
        {
            SteamId = steamId;
            AppearanceData = appearanceData;
        }
    }
}
