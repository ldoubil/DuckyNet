using System;
using UnityEngine;
using static UnityEngine.Debug;
using Steamworks;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.Data;
using DuckyNet.Client.Services;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Client.Core.EventBus;
using DuckyNet.Client.Core.EventBus.Events;
using DuckyNet.Client.Core.Utils;
using System.Collections.Generic;
using ItemStatsSystem;
using Duckov.Utilities;

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

        /// <summary>
        /// 获取所有远程玩家的位置（用于热区计算）
        /// </summary>
        public List<Vector3> GetRemotePlayerPositions()
        {
            var positions = new List<Vector3>();
            
            foreach (var player in _remotePlayers.Values)
            {
                if (player.CharacterObject != null)
                {
                    positions.Add(player.CharacterObject.transform.position);
                }
            }
            
            return positions;
        }
        
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
            _eventSubscriber.Subscribe<NetworkDisconnectedEvent>(OnNetworkDisconnected);
            
            // 🎯 订阅角色创建事件（用于动画同步注册）
            _eventSubscriber.Subscribe<RemoteCharacterCreatedEvent>(OnRemoteCharacterCreated);
            
            // 🎯 订阅动画同步事件
            _eventSubscriber.Subscribe<RemoteAnimatorUpdateEvent>(OnRemoteAnimatorUpdate);
            _eventSubscriber.Subscribe<RemoteEquipmentSlotUpdatedEvent>(OnRemoteEquipmentSlotUpdated);
            _eventSubscriber.Subscribe<AllPlayersEquipmentReceivedEvent>(OnAllPlayersEquipmentReceived);
            _eventSubscriber.Subscribe<RemoteWeaponSlotUpdatedEvent>(OnRemoteWeaponSlotUpdated);
            _eventSubscriber.Subscribe<AllPlayersWeaponReceivedEvent>(OnAllPlayersWeaponReceived);
            _eventSubscriber.Subscribe<RemoteWeaponSwitchedEvent>(OnRemoteWeaponSwitched);
            _eventSubscriber.Subscribe<RemoteWeaponFiredEvent>(OnRemoteWeaponFired);
            
            Log($"[PlayerManager] 初始化完成 - 房间+场景双层架构 + 动画同步");
        }

        /// <summary>
        /// 玩家加入房间 - 创建 RemotePlayer
        /// 📌 可能在场景事件中已经创建（容错处理），需要检查重复
        /// </summary>
        private void OnPlayerJoinedRoom(PlayerJoinedRoomEvent @event)
        {
            Log($"[PlayerManager] ========== 收到 PlayerJoinedRoomEvent ==========");
            Log($"[PlayerManager] 玩家: {@event.Player.SteamName} ({@event.Player.SteamId})");
            Log($"[PlayerManager] 房间: {@event.Room.RoomName} ({@event.Room.RoomId})");
            
            // 排除本地玩家
            if (@event.Player.SteamId == LocalPlayer.Info.SteamId)
            {
                Log($"[PlayerManager] ⚠️ 跳过本地玩家");
                return;
            }
            
            // 🔥 创建 RemotePlayer（如果不存在）
            if (!_remotePlayers.ContainsKey(@event.Player.SteamId))
            {
                var remotePlayer = new RemotePlayer(@event.Player);
                _remotePlayers[@event.Player.SteamId] = remotePlayer;
                Log($"[PlayerManager] ✅ 创建 RemotePlayer: {@event.Player.SteamName}");
            }
            else
            {
                // 可能在场景事件中已经创建（容错处理）
                Log($"[PlayerManager] RemotePlayer 已存在（可能是场景事件先到达）: {@event.Player.SteamName}");
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
        /// 玩家进入场景 - 确保 RemotePlayer 存在（容错处理）
        /// 📌 场景进入事件可能比房间加入事件先到达，需要容错处理
        /// 📌 RemotePlayer 会自己订阅 PlayerEnteredSceneEvent 并创建角色
        /// </summary>
        private void OnPlayerEnteredScene(PlayerEnteredSceneEvent @event)
        {
            Log($"[PlayerManager] ========== PlayerEnteredSceneEvent 接收 ==========");
            Log($"[PlayerManager] 玩家: {@event.PlayerInfo.SteamName} ({@event.PlayerInfo.SteamId})");
            Log($"[PlayerManager] 场景: {@event.ScenelData.SceneName}/{@event.ScenelData.SubSceneName}");
            
            // 排除本地玩家
            if (@event.PlayerInfo.SteamId == LocalPlayer.Info.SteamId)
            {
                Log($"[PlayerManager] ⚠️ 跳过本地玩家的场景事件");
                return;
            }

            // 🔥 容错处理：如果 RemotePlayer 不存在，先创建它
            if (!_remotePlayers.ContainsKey(@event.PlayerInfo.SteamId))
            {
                Log($"[PlayerManager] ⚠️ RemotePlayer 不存在，先创建（可能是场景事件先于房间事件到达）: {@event.PlayerInfo.SteamName}");
                
                var remotePlayer = new RemotePlayer(@event.PlayerInfo);
                _remotePlayers[@event.PlayerInfo.SteamId] = remotePlayer;
                
                Log($"[PlayerManager] ✅ 容错创建 RemotePlayer: {@event.PlayerInfo.SteamName}");
            }
            else
            {
                Log($"[PlayerManager] RemotePlayer 已存在: {@event.PlayerInfo.SteamName}");
            }
            
            // RemotePlayer 会自己处理场景进入事件（订阅了 PlayerEnteredSceneEvent）
            // 这里不需要额外操作
            Log($"[PlayerManager] ========== PlayerEnteredSceneEvent 处理完成 ==========");
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
        /// 网络断开连接 - 清理所有远程玩家
        /// </summary>
        private void OnNetworkDisconnected(NetworkDisconnectedEvent @event)
        {
            Log($"[PlayerManager] 🔥 网络断开连接，清理所有远程玩家: {@event.Reason}");
            
            // 销毁所有 RemotePlayer
            foreach (var kvp in _remotePlayers)
            {
                kvp.Value.Dispose();
                Log($"[PlayerManager] 销毁 RemotePlayer: {kvp.Value.Info.SteamName}");
            }
            
            _remotePlayers.Clear();
            _remoteAnimatorSync?.Dispose();
            
            Log($"[PlayerManager] ✅ 所有远程玩家已清理");
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
            // Debug.Log($"[PlayerManager] 📬 接收到动画事件 - PlayerId:{@event.PlayerId}, State:{@event.AnimatorData.StateHash}");
            _remoteAnimatorSync.ReceiveAnimatorUpdate(@event.PlayerId, @event.AnimatorData);
        }

        private void OnRemoteEquipmentSlotUpdated(RemoteEquipmentSlotUpdatedEvent @event)
        {
            try
            {
                var notification = @event.Notification;
                var localPlayerId = LocalPlayer?.Info.SteamId;
                if (!string.IsNullOrEmpty(localPlayerId) && localPlayerId == notification.PlayerId)
                {
                    return;
                }

                var remotePlayer = GetRemotePlayer(notification.PlayerId);
                if (remotePlayer == null)
                {
                    LogWarning($"[PlayerManager] 找不到远程玩家: {notification.PlayerId}");
                    return;
                }

                remotePlayer.UpdateEquipmentSlot(notification.SlotType, notification.ItemTypeId);

                if (remotePlayer.CharacterObject != null)
                {
                    ApplyEquipmentToCharacter(remotePlayer, notification.SlotType, notification.ItemTypeId);
                }
            }
            catch (Exception ex)
            {
                LogError($"[PlayerManager] 处理装备更新失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnAllPlayersEquipmentReceived(AllPlayersEquipmentReceivedEvent @event)
        {
            try
            {
                var playerManager = GameContext.Instance.PlayerManager;
                if (playerManager == null)
                {
                    LogWarning("[PlayerManager] PlayerManager 未初始化");
                    return;
                }

                foreach (var kvp in @event.EquipmentData.PlayersEquipment)
                {
                    string playerId = kvp.Key;
                    PlayerEquipmentData equipmentData = kvp.Value;

                    if (playerId == playerManager.LocalPlayer?.Info.SteamId)
                    {
                        continue;
                    }

                    var remotePlayer = playerManager.GetRemotePlayer(playerId);
                    if (remotePlayer == null)
                    {
                        LogWarning($"[PlayerManager] 找不到远程玩家: {playerId}，跳过装备数据");
                        continue;
                    }

                    remotePlayer.SetEquipmentData(equipmentData);

                    if (remotePlayer.CharacterObject != null)
                    {
                        ApplyAllEquipmentToCharacter(remotePlayer);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"[PlayerManager] 处理批量装备数据失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnRemoteWeaponSlotUpdated(RemoteWeaponSlotUpdatedEvent @event)
        {
            try
            {
                var notification = @event.Notification;
                var localPlayerId = LocalPlayer?.Info.SteamId;
                if (!string.IsNullOrEmpty(localPlayerId) && localPlayerId == notification.PlayerId)
                {
                    return;
                }

                var remotePlayer = GetRemotePlayer(notification.PlayerId);
                if (remotePlayer == null)
                {
                    LogWarning($"[PlayerManager] 找不到远程玩家: {notification.PlayerId}");
                    return;
                }

                WeaponItemData? weaponData = null;
                if (notification.ItemTypeId > 0)
                {
                    weaponData = new WeaponItemData
                    {
                        ItemTypeId = notification.ItemTypeId,
                        ItemName = notification.ItemName,
                        IsDefaultItem = notification.IsDefaultItem,
                        ItemDataCompressed = notification.ItemDataCompressed
                    };
                }

                remotePlayer.UpdateWeaponSlot(notification.SlotType, weaponData);

                if (remotePlayer.CharacterObject != null)
                {
                    ApplyWeaponToCharacter(remotePlayer, notification.SlotType, weaponData);
                }
            }
            catch (Exception ex)
            {
                LogError($"[PlayerManager] 处理武器更新失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnAllPlayersWeaponReceived(AllPlayersWeaponReceivedEvent @event)
        {
            try
            {
                var playerManager = GameContext.Instance.PlayerManager;
                if (playerManager == null)
                {
                    LogWarning("[PlayerManager] PlayerManager 未初始化");
                    return;
                }

                foreach (var kvp in @event.WeaponData.PlayersWeapons)
                {
                    string playerId = kvp.Key;
                    PlayerWeaponData weaponData = kvp.Value;

                    if (playerId == playerManager.LocalPlayer?.Info.SteamId)
                    {
                        continue;
                    }

                    var remotePlayer = playerManager.GetRemotePlayer(playerId);
                    if (remotePlayer == null)
                    {
                        LogWarning($"[PlayerManager] 找不到远程玩家: {playerId}，跳过武器数据");
                        continue;
                    }

                    remotePlayer.SetWeaponData(weaponData);

                    if (remotePlayer.CharacterObject != null)
                    {
                        ApplyAllWeaponsToCharacter(remotePlayer);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"[PlayerManager] 处理批量武器数据失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnRemoteWeaponSwitched(RemoteWeaponSwitchedEvent @event)
        {
            try
            {
                var notification = @event.Notification;
                var localPlayerId = LocalPlayer?.Info.SteamId;
                if (!string.IsNullOrEmpty(localPlayerId) && localPlayerId == notification.PlayerId)
                {
                    return;
                }

                var remotePlayer = GetRemotePlayer(notification.PlayerId);
                if (remotePlayer == null)
                {
                    LogWarning($"[PlayerManager] 找不到远程玩家: {notification.PlayerId}");
                    return;
                }

                remotePlayer.SwitchWeaponSlot(notification.CurrentWeaponSlot);
            }
            catch (Exception ex)
            {
                LogError($"[PlayerManager] 处理武器切换失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnRemoteWeaponFired(RemoteWeaponFiredEvent @event)
        {
            try
            {
                var fireData = @event.FireData;
                var localPlayerId = GameContext.Instance.PlayerManager?.LocalPlayer?.Info.SteamId;
                if (!string.IsNullOrEmpty(localPlayerId) && localPlayerId == fireData.PlayerId)
                {
                    return;
                }

                var remotePlayer = GameContext.Instance.PlayerManager?.GetRemotePlayer(fireData.PlayerId);
                if (remotePlayer == null || remotePlayer.CharacterObject == null)
                {
                    return;
                }

                WeaponFireEffectsPlayer.PlayFireEffects(remotePlayer.CharacterObject, fireData);
            }
            catch (Exception ex)
            {
                LogError($"[PlayerManager] 播放开枪特效失败: {ex.Message}");
            }
        }

        private void ApplyEquipmentToCharacter(RemotePlayer remotePlayer, EquipmentSlotType slotType, int? itemTypeId)
        {
            try
            {
                var characterMainControl = remotePlayer.CharacterObject?.GetComponent<CharacterMainControl>();
                if (characterMainControl == null || characterMainControl.CharacterItem == null)
                {
                    LogWarning($"[PlayerManager] 角色组件无效");
                    return;
                }

                int slotHash = GetEquipmentSlotHash(slotType);
                var slot = characterMainControl.CharacterItem.Slots.GetSlot(slotHash);
                if (slot == null)
                {
                    LogWarning($"[PlayerManager] 槽位不存在: {slotType}");
                    return;
                }

                if (itemTypeId.HasValue && itemTypeId.Value > 0)
                {
                    bool success = EquipmentTools.CreateAndEquip(
                        itemTypeId.Value,
                        slot,
                        unpluggedItem => unpluggedItem.DestroyTree()
                    );

                    if (!success)
                    {
                        LogWarning($"[PlayerManager] 应用装备失败: {slotType}");
                    }
                }
                else
                {
                    if (slot.Content != null)
                    {
                        var removed = slot.Unplug();
                        if (removed != null)
                        {
                            removed.DestroyTree();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"[PlayerManager] 应用装备失败: {ex.Message}");
            }
        }

        private void ApplyAllEquipmentToCharacter(RemotePlayer remotePlayer)
        {
            try
            {
                var equipmentData = remotePlayer.GetEquipmentData();
                if (equipmentData == null)
                {
                    Log("[PlayerManager] 该玩家没有装备数据");
                    return;
                }

                foreach (var kvp in equipmentData.Equipment)
                {
                    EquipmentSlotType slotType = kvp.Key;
                    int? itemTypeId = kvp.Value;

                    if (itemTypeId.HasValue && itemTypeId.Value > 0)
                    {
                        ApplyEquipmentToCharacter(remotePlayer, slotType, itemTypeId);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"[PlayerManager] 应用所有装备失败: {ex.Message}");
            }
        }

        private int GetEquipmentSlotHash(EquipmentSlotType slotType)
        {
            return slotType switch
            {
                EquipmentSlotType.Armor => CharacterEquipmentController.armorHash,
                EquipmentSlotType.Helmet => CharacterEquipmentController.helmatHash,
                EquipmentSlotType.FaceMask => CharacterEquipmentController.faceMaskHash,
                EquipmentSlotType.Backpack => CharacterEquipmentController.backpackHash,
                EquipmentSlotType.Headset => CharacterEquipmentController.headsetHash,
                _ => 0
            };
        }

        private void ApplyWeaponToCharacter(RemotePlayer remotePlayer, WeaponSlotType slotType, WeaponItemData? weaponData)
        {
            try
            {
                var characterMainControl = remotePlayer.CharacterObject?.GetComponent<CharacterMainControl>();
                if (characterMainControl == null || characterMainControl.CharacterItem == null)
                {
                    LogWarning($"[PlayerManager] 角色组件无效");
                    return;
                }

                int slotHash = GetWeaponSlotHash(slotType);
                var slot = characterMainControl.CharacterItem.Slots.GetSlot(slotHash);

                if (slot == null)
                {
                    LogWarning($"[PlayerManager] 槽位不存在: {slotType}, Hash={slotHash}");
                    return;
                }

                if (weaponData != null && weaponData.ItemTypeId > 0)
                {
                    Item? weaponItem = WeaponSyncHelper.DeserializeItem(
                        weaponData.ItemDataCompressed,
                        weaponData.ItemTypeId
                    );

                    if (weaponItem != null)
                    {
                        bool success = slot.Plug(weaponItem, out Item unpluggedItem);

                        if (success)
                        {
                            if (unpluggedItem != null)
                            {
                                unpluggedItem.DestroyTree();
                            }

                            var currentSlot = remotePlayer.GetWeaponData()?.CurrentWeaponSlot;
                            if (currentSlot.HasValue && currentSlot.Value == slotType)
                            {
                                try
                                {
                                    characterMainControl.ChangeHoldItem(weaponItem);
                                }
                                catch (Exception agentEx)
                                {
                                    LogError($"[PlayerManager] ChangeHoldItem 失败: {agentEx.Message}");
                                }
                            }
                        }
                        else
                        {
                            weaponItem.DestroyTree();
                        }
                    }
                }
                else
                {
                    if (slot.Content != null)
                    {
                        var removed = slot.Unplug();
                        if (removed != null)
                        {
                            try
                            {
                                if (characterMainControl.CurrentHoldItemAgent?.Item == removed)
                                {
                                    characterMainControl.ChangeHoldItem(null);
                                }
                            }
                            catch (Exception agentEx)
                            {
                                LogError($"[PlayerManager] 清除武器显示失败: {agentEx.Message}");
                            }

                            removed.DestroyTree();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"[PlayerManager] 应用武器失败: {ex.Message}");
            }
        }

        private void ApplyAllWeaponsToCharacter(RemotePlayer remotePlayer)
        {
            try
            {
                var weaponData = remotePlayer.GetWeaponData();
                if (weaponData == null)
                {
                    Log("[PlayerManager] 该玩家没有武器数据");
                    return;
                }

                var weaponSlots = new[]
                {
                    (WeaponSlotType.PrimaryWeapon, weaponData.PrimaryWeapon),
                    (WeaponSlotType.SecondaryWeapon, weaponData.SecondaryWeapon),
                    (WeaponSlotType.MeleeWeapon, weaponData.MeleeWeapon)
                };

                foreach (var (slotType, weapon) in weaponSlots)
                {
                    if (weapon != null && weapon.ItemTypeId > 0)
                    {
                        ApplyWeaponToCharacter(remotePlayer, slotType, weapon);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"[PlayerManager] 应用所有武器失败: {ex.Message}");
            }
        }

        private int GetWeaponSlotHash(WeaponSlotType slotType)
        {
            return slotType switch
            {
                WeaponSlotType.PrimaryWeapon => "PrimaryWeapon".GetHashCode(),
                WeaponSlotType.SecondaryWeapon => "SecondaryWeapon".GetHashCode(),
                WeaponSlotType.MeleeWeapon => "MeleeWeapon".GetHashCode(),
                _ => 0
            };
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
