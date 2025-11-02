using System;
using UnityEngine;
using static UnityEngine.Debug;
using Steamworks;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.Data;
using DuckyNet.Client.Core.Helpers;
using System.Collections.Generic;

namespace DuckyNet.Client.Core.Players
{
    public class PlayerManager : IDisposable
    {
        // 使用 Dictionary 替代 List - O(1) 查找
        private readonly Dictionary<string, RemotePlayer> _remotePlayers = new Dictionary<string, RemotePlayer>();
        public LocalPlayer LocalPlayer { get; private set; }
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();
        public PlayerManager()
        {
            LocalPlayer = new LocalPlayer(new PlayerInfo());
            _eventSubscriber.EnsureInitializedAndSubscribe();
            
            // 🔥 正确架构：
            // - 房间事件：创建/删除 RemotePlayer
            // - 场景事件：创建/删除角色
            _eventSubscriber.Subscribe<PlayerJoinedRoomEvent>(OnPlayerJoinedRoom);
            _eventSubscriber.Subscribe<PlayerLeftRoomEvent>(OnPlayerLeftRoom);
            _eventSubscriber.Subscribe<PlayerEnteredSceneEvent>(OnPlayerEnteredScene);
            _eventSubscriber.Subscribe<PlayerLeftSceneEvent>(OnPlayerLeftScene);
            _eventSubscriber.Subscribe<PlayerLeftEvent>(OnPlayerDisconnected);
            
            Log($"[PlayerManager] 初始化完成 - 房间+场景双层架构");
        }

        /// <summary>
        /// 玩家加入房间 - 创建 RemotePlayer
        /// </summary>
        private void OnPlayerJoinedRoom(PlayerJoinedRoomEvent @event)
        {
            Log($"[PlayerManager] ========== 收到 PlayerJoinedRoomEvent ==========");
            Log($"[PlayerManager] 玩家: {@event.Player.SteamName} ({@event.Player.SteamId})");
            Log($"[PlayerManager] 房间: {@event.Room.RoomName} ({@event.Room.RoomId})");
            Log($"[PlayerManager] 本地玩家: {LocalPlayer.Info.SteamName} ({LocalPlayer.Info.SteamId})");
            
            // 排除本地玩家
            if (@event.Player.SteamId == LocalPlayer.Info.SteamId)
            {
                Log($"[PlayerManager] ⚠️ 跳过：这是本地玩家");
                return;
            }
            
            // 🔥 创建 RemotePlayer（不创建角色）
            if (!_remotePlayers.ContainsKey(@event.Player.SteamId))
            {
                var remotePlayer = new RemotePlayer(@event.Player);
                _remotePlayers[@event.Player.SteamId] = remotePlayer;
                Log($"[PlayerManager] ✅ 创建 RemotePlayer: {@event.Player.SteamName}");
            }
            else
            {
                Log($"[PlayerManager] ⚠️ RemotePlayer 已存在: {@event.Player.SteamName}");
            }
            Log($"[PlayerManager] ========== 处理完成 ==========");
        }

        /// <summary>
        /// 玩家离开房间 - 删除 RemotePlayer
        /// </summary>
        private void OnPlayerLeftRoom(PlayerLeftRoomEvent @event)
        {
            Log($"[PlayerManager] 玩家离开房间: {@event.Player.SteamName}");
            
            // 排除本地玩家
            if (@event.Player.SteamId == LocalPlayer.Info.SteamId)
            {
                return;
            }
            
            // 🔥 销毁 RemotePlayer（会自动销毁角色）
            if (_remotePlayers.TryGetValue(@event.Player.SteamId, out var player))
            {
                player.Dispose();
                _remotePlayers.Remove(@event.Player.SteamId);
                Log($"[PlayerManager] 销毁 RemotePlayer: {@event.Player.SteamName}");
            }
        }

        /// <summary>
        /// 玩家进入场景 - 创建角色（RemotePlayer 必须已存在）
        /// </summary>
        private void OnPlayerEnteredScene(PlayerEnteredSceneEvent @event)
        {
            Log($"[PlayerManager] ========== 收到 PlayerEnteredSceneEvent ==========");
            Log($"[PlayerManager] 玩家: {@event.PlayerInfo.SteamName} ({@event.PlayerInfo.SteamId})");
            Log($"[PlayerManager] 事件场景: {@event.ScenelData.SceneName} / {@event.ScenelData.SubSceneName}");
            Log($"[PlayerManager] 本地玩家: {LocalPlayer.Info.SteamName} ({LocalPlayer.Info.SteamId})");
            Log($"[PlayerManager] 本地场景: {LocalPlayer.Info.CurrentScenelData.SceneName} / {LocalPlayer.Info.CurrentScenelData.SubSceneName}");
            
            // 排除本地玩家
            if (@event.PlayerInfo.SteamId == LocalPlayer.Info.SteamId)
            {
                Log($"[PlayerManager] ⚠️ 跳过：这是本地玩家");
                return;
            }
            
            // 🔥 检查是否在同一场景
            if (!IsInSameScene(@event.ScenelData))
            {
                Log($"[PlayerManager] ⚠️ 跳过：玩家 {@event.PlayerInfo.SteamName} 在不同场景");
                return;
            }
            
            // 🔥 RemotePlayer 必须已经存在（应该在加入房间时创建）
            if (!_remotePlayers.TryGetValue(@event.PlayerInfo.SteamId, out var remotePlayer))
            {
                Log($"[PlayerManager] ⚠️⚠️⚠️ 错误：RemotePlayer 不存在，无法创建角色！玩家: {@event.PlayerInfo.SteamName}");
                return;
            }
            
            Log($"[PlayerManager] ✅ 玩家进入当前场景，RemotePlayer 已存在，等待位置同步创建角色");
            // 🔥 注意：角色会在 RemotePlayer 收到位置同步时自动创建
            Log($"[PlayerManager] ========== 处理完成 ==========");
        }

        /// <summary>
        /// 玩家离开场景 - 只销毁角色，不销毁 RemotePlayer
        /// </summary>
        private void OnPlayerLeftScene(PlayerLeftSceneEvent @event)
        {
            // 排除本地玩家
            if (@event.PlayerInfo.SteamId == LocalPlayer.Info.SteamId)
            {
                return;
            }
            
            Log($"[PlayerManager] 玩家离开场景: {@event.PlayerInfo.SteamName}");
            
            // 🔥 只销毁角色，RemotePlayer 保留（玩家还在房间中）
            if (_remotePlayers.TryGetValue(@event.PlayerInfo.SteamId, out var player))
            {
                player.DestroyCharacter();
                Log($"[PlayerManager] 销毁角色（保留 RemotePlayer）: {@event.PlayerInfo.SteamName}");
            }
        }

        /// <summary>
        /// 玩家断开连接 - 销毁 RemotePlayer
        /// </summary>
        private void OnPlayerDisconnected(PlayerLeftEvent @event)
        {
            // 排除本地玩家
            if (@event.Player.SteamId == LocalPlayer.Info.SteamId)
            {
                return;
            }
            
            Log($"[PlayerManager] 玩家断开连接: {@event.Player.SteamName}");
            
            // 销毁 RemotePlayer
            if (_remotePlayers.TryGetValue(@event.Player.SteamId, out var player))
            {
                player.Dispose();
                _remotePlayers.Remove(@event.Player.SteamId);
                Log($"[PlayerManager] 销毁 RemotePlayer: {@event.Player.SteamName}");
            }
        }

        /// <summary>
        /// 检查远程玩家是否在同一场景
        /// </summary>
        private bool IsInSameScene(ScenelData remoteSceneData)
        {
            // 🔥 直接比较场景数据
            bool sameScene = remoteSceneData.SceneName == LocalPlayer.Info.CurrentScenelData.SceneName &&
                   remoteSceneData.SubSceneName == LocalPlayer.Info.CurrentScenelData.SubSceneName;
            
            Log($"[PlayerManager] 场景匹配检查: 远程({remoteSceneData.SceneName}/{remoteSceneData.SubSceneName}) vs 本地({LocalPlayer.Info.CurrentScenelData.SceneName}/{LocalPlayer.Info.CurrentScenelData.SubSceneName}) = {sameScene}");
            
            return sameScene;
        }

        public void Dispose()
        {
            LocalPlayer.Dispose();
            foreach (var kvp in _remotePlayers)
            {
                kvp.Value.Dispose();
            }
            _remotePlayers.Clear();
        }

        /// <summary>
        /// 更新本地玩家和远程玩家（每帧调用）
        /// </summary>
        public void Update()
        {
            LocalPlayer?.LateUpdate();
            
            // 更新所有远程玩家位置（平滑同步）
            foreach (var kvp in _remotePlayers)
            {
                kvp.Value?.UpdatePosition();
            }
        }
    }
}