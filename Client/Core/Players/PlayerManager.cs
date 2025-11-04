using System;
using UnityEngine;
using static UnityEngine.Debug;
using Steamworks;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.Data;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Client.Core.EventBus;
using DuckyNet.Client.Core.EventBus.Events;
using System.Collections.Generic;

namespace DuckyNet.Client.Core.Players
{
    public class PlayerManager : IDisposable
    {
        // 使用 Dictionary 替代 List - O(1) 查找
        private readonly Dictionary<string, RemotePlayer> _remotePlayers = new Dictionary<string, RemotePlayer>();
        
        /// <summary>
        /// 获取所有远程玩家（只读）
        /// </summary>
        public IEnumerable<RemotePlayer> RemotePlayers => _remotePlayers.Values;
        
        public LocalPlayer LocalPlayer { get; private set; }
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();
        
        // 🎯 新增：远程动画同步管理器
        private readonly RemoteAnimatorSyncManager _remoteAnimatorSync = new RemoteAnimatorSyncManager();
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
            
            // 🎯 订阅角色创建事件（用于动画同步注册）
            _eventSubscriber.Subscribe<RemoteCharacterCreatedEvent>(OnRemoteCharacterCreated);
            
            // 🎯 订阅动画同步事件
            _eventSubscriber.Subscribe<RemoteAnimatorUpdateEvent>(OnRemoteAnimatorUpdate);
            
            Log($"[PlayerManager] 初始化完成 - 房间+场景双层架构 + 动画同步");
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
            // 🎯 角色创建后会发布 RemoteCharacterCreatedEvent，由 OnRemoteCharacterCreated 处理动画同步注册
            
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
                // 🎯 先注销动画同步
                _remoteAnimatorSync.UnregisterRemotePlayer(@event.PlayerInfo.SteamId);
                
                player.DestroyCharacter();
                Log($"[PlayerManager] 销毁角色（保留 RemotePlayer）: {@event.PlayerInfo.SteamName}");
            }
        }

        /// <summary>
        /// 远程角色创建完成 - 注册或更新动画同步系统
        /// </summary>
        private void OnRemoteCharacterCreated(RemoteCharacterCreatedEvent @event)
        {
            if (@event.Character == null)
            {
                LogWarning($"[PlayerManager] ⚠️ 角色创建事件的 Character 为空: {@event.PlayerId}");
                return;
            }
            
            // 🔥 检查是否已注册(场景切换后角色重新创建)
            if (_remoteAnimatorSync != null)
            {
                // 尝试更新 GameObject (如果已注册)
                _remoteAnimatorSync.UpdatePlayerGameObject(@event.PlayerId, @event.Character);
                
                // 如果是首次创建,则注册
                _remoteAnimatorSync.RegisterRemotePlayer(@event.PlayerId, @event.Character);
                
                Log($"[PlayerManager] ✅ 动画同步已就绪: {@event.PlayerId}");
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
        
        /// <summary>
        /// 🎯 处理远程动画更新事件
        /// </summary>
        private void OnRemoteAnimatorUpdate(RemoteAnimatorUpdateEvent @event)
        {
            Debug.Log($"[PlayerManager] 📬 接收到动画事件 - PlayerId:{@event.PlayerId}, State:{@event.AnimatorData.StateHash}");
            _remoteAnimatorSync.ReceiveAnimatorUpdate(@event.PlayerId, @event.AnimatorData);
        }

        /// <summary>
        /// 获取远程玩家
        /// </summary>
        public RemotePlayer? GetRemotePlayer(string steamId)
        {
            if (_remotePlayers.TryGetValue(steamId, out var player))
            {
                return player;
            }
            return null;
        }

        public void Dispose()
        {
            LocalPlayer.Dispose();
            _remoteAnimatorSync.Dispose();
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
        
        /// <summary>
        /// 🎯 LateUpdate - 更新远程动画
        /// </summary>
        public void LateUpdate()
        {
            _remoteAnimatorSync.UpdateAll();
        }
    }
}