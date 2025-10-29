using System;
using UnityEngine;
using DuckyNet.Shared.Services;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 玩家客户端服务实现（接收服务器调用）
    /// </summary>
    public class PlayerClientServiceImpl : IPlayerClientService
    {
        public event Action<PlayerInfo, string>? OnChatMessageReceived;
        public event Action<PlayerInfo>? OnPlayerJoinedEvent;
        public event Action<PlayerInfo>? OnPlayerLeftEvent;

        public void OnChatMessage(PlayerInfo sender, string message)
        {
            Debug.Log($"[Chat] {sender.SteamName}: {message}");
            OnChatMessageReceived?.Invoke(sender, message);
        }

        public void OnPlayerJoined(PlayerInfo player)
        {
            Debug.Log($"[PlayerClientService] Player joined: {player.SteamName}");
            OnPlayerJoinedEvent?.Invoke(player);
        }

        public void OnPlayerLeft(PlayerInfo player)
        {
            Debug.Log($"[PlayerClientService] Player left: {player.SteamName}");
            OnPlayerLeftEvent?.Invoke(player);
        }


        public void OnServerMessage(string message, MessageType messageType)
        {
            string prefix = messageType switch
            {
                MessageType.Info => "[Server/Info]",
                MessageType.Warning => "[Server/Warning]",
                MessageType.Error => "[Server/Error]",
                MessageType.Success => "[Server/Success]",
                _ => "[Server]"
            };
            
            Debug.Log($"{prefix} {message}");
        }
    }
}

