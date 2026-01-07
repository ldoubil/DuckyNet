using DuckyNet.Shared.Events;
using UnityEngine;

namespace DuckyNet.Client.Core.EventBus.Events
{
    /// <summary>
    /// 角色创建完成事件
    /// </summary>
    public class CharacterCreatedEvent : EventBase
    {
        public string SteamId { get; }
        public GameObject? Character { get; }
        
        public CharacterCreatedEvent(string steamId, GameObject? character)
        {
            SteamId = steamId;
            Character = character;
        }
    }

    /// <summary>
    /// 主角色创建完成事件（本地玩家角色创建完成）
    /// </summary>
    public class MainCharacterCreatedEvent : EventBase
    {
        public GameObject Character { get; }
        
        public MainCharacterCreatedEvent(GameObject character)
        {
            Character = character;
        }
    }

    /// <summary>
    /// 创建远程角色请求事件
    /// </summary>
    public class CreateRemoteCharacterRequestEvent : EventBase
    {
        public string PlayerId { get; }
        
        public CreateRemoteCharacterRequestEvent(string playerId)
        {
            PlayerId = playerId;
        }
    }

    /// <summary>
    /// 远程角色已创建事件
    /// </summary>
    public class RemoteCharacterCreatedEvent : EventBase
    {
        public string PlayerId { get; }
        public GameObject? Character { get; }
        
        public RemoteCharacterCreatedEvent(string playerId, GameObject? character)
        {
            PlayerId = playerId;
            Character = character;
        }
    }

    /// <summary>
    /// 玩家外观更新事件
    /// </summary>
    public class PlayerAppearanceUpdatedEvent : EventBase
    {
        public string SteamId { get; }
        public byte[] AppearanceData { get; }
        
        public PlayerAppearanceUpdatedEvent(string steamId, byte[] appearanceData)
        {
            SteamId = steamId;
            AppearanceData = appearanceData;
        }
    }

    /// <summary>
    /// 角色外观数据接收事件
    /// </summary>
    public class CharacterAppearanceReceivedEvent : EventBase
    {
        public string SteamId { get; }
        public byte[] AppearanceData { get; }
        
        public CharacterAppearanceReceivedEvent(string steamId, byte[] appearanceData)
        {
            SteamId = steamId;
            AppearanceData = appearanceData;
        }
    }
}
