using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DuckyNet.Client.UI;
using DuckyNet.Client.RPC;
using DuckyNet.Client.Services;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Client.Core.EventBus.Events;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services.Generated;
using System.Threading.Tasks;
using DuckyNet.Client.Core.EventBus;

namespace DuckyNet.Client.Core
{

    public class RoomManager : IDisposable
    {
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();
        private RoomServiceClientProxy _roomServiceClient;
        public RoomInfo? CurrentRoom { get; private set; }

        public List<PlayerInfo> RoomPlayers { get; private set; } = new List<PlayerInfo>();

        public RoomManager()
        {
            Debug.Log("[RoomManager] 构造函数开始");
            _eventSubscriber.EnsureInitializedAndSubscribe();
            _eventSubscriber.Subscribe<RoomJoinedEvent>(OnRoomJoined);
            _eventSubscriber.Subscribe<RoomLeftEvent>(OnRoomLeft);
            _eventSubscriber.Subscribe<NetworkDisconnectedEvent>(OnNetworkDisconnected);
            Debug.Log("[RoomManager] 构造函数完成 (事件已订阅)");
            var serverContext = new ClientServerContext(GameContext.Instance.RpcClient);
            _roomServiceClient = new RoomServiceClientProxy(serverContext);
        }




        public IReadOnlyList<PlayerInfo> GetRoomPlayers() => RoomPlayers;

        public void SetCurrentRoom(RoomInfo room)
        {
            CurrentRoom = room;
            RefreshPlayerListAsync();
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RoomJoinedEvent(GameContext.Instance.PlayerManager.LocalPlayer.Info, room));
            }
        }

        public async void RefreshPlayerListAsync()
        {
            if (CurrentRoom == null) return;
            try
            {
                var oldPlayers = RoomPlayers.ToList(); // 保存旧列表
                var players = await _roomServiceClient.GetRoomPlayersAsync(CurrentRoom.RoomId);
                RoomPlayers = new List<PlayerInfo>(players);
                
                // 详情打印 RoomPlayers
                foreach (var player in RoomPlayers)
                {
                    Debug.Log($"[RoomManager] 玩家: {player.SteamName}, 场景: {player.CurrentScenelData.SceneName}, 子场景: {player.CurrentScenelData.SubSceneName}");
                }
                Debug.Log($"[RoomManager] 刷新房间玩家: {RoomPlayers.Count}");
                
                // 🔥 关键修复：对比新旧列表，为新增玩家发布 PlayerJoinedRoomEvent
                if (GameContext.IsInitialized && CurrentRoom != null)
                {
                    var localSteamId = GameContext.Instance.PlayerManager.LocalPlayer.Info.SteamId;
                    
                    foreach (var newPlayer in RoomPlayers)
                    {
                        // 跳过自己
                        if (newPlayer.SteamId == localSteamId)
                            continue;
                        
                        // 检查是否是新玩家（不在旧列表中）
                        bool isNewPlayer = !oldPlayers.Any(p => p.SteamId == newPlayer.SteamId);
                        
                        if (isNewPlayer)
                        {
                            Debug.Log($"[RoomManager] 🔥 检测到新玩家，发布 PlayerJoinedRoomEvent: {newPlayer.SteamName}");
                            GameContext.Instance.EventBus.Publish(new PlayerJoinedRoomEvent(newPlayer, CurrentRoom));
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[RoomManager] 刷新玩家列表失败: {ex.Message}");
            }
        }

        public async Task<bool> LeaveRoomAsync()
        {

            try
            {
                var success = await _roomServiceClient.LeaveRoomAsync();
                if (success)
                {
                    var leftRoom = CurrentRoom;
                    CurrentRoom = null;
                    RoomPlayers.Clear();
                    if (GameContext.IsInitialized)
                    {
                        GameContext.Instance.EventBus.Publish(new RoomLeftEvent(GameContext.Instance.PlayerManager.LocalPlayer.Info, leftRoom ?? new RoomInfo()));
                    }
                }
                return success;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[RoomManager] 离开房间失败: {ex.Message}");
                return false;
            }
        }

        private async void OnRoomJoined(RoomJoinedEvent evt)
        {

            if (evt.Player.SteamId == GameContext.Instance.PlayerManager.LocalPlayer.Info.SteamId)
            {
                Debug.Log($"[RoomManager] 自己进入房间: {evt.Room.RoomId}");
                CurrentRoom = evt.Room;
                try
                {
                    var players = await _roomServiceClient.GetRoomPlayersAsync(evt.Room.RoomId);
                    RoomPlayers = new List<PlayerInfo>(players);
                    Debug.Log($"[RoomManager] 房间玩家: {string.Join(", ", RoomPlayers.Select(p => p.SteamName))}");
                    
                    // 🔥 关键修复：为房间内其他玩家发布 PlayerJoinedRoomEvent
                    var localSteamId = GameContext.Instance.PlayerManager.LocalPlayer.Info.SteamId;
                    Debug.Log($"[RoomManager] 🔥 准备为房间内玩家发布事件，总玩家数: {RoomPlayers.Count}，本地玩家ID: {localSteamId}");
                    
                    int publishedCount = 0;
                    foreach (var otherPlayer in RoomPlayers)
                    {
                        // 跳过自己
                        if (otherPlayer.SteamId == localSteamId)
                        {
                            Debug.Log($"[RoomManager] 跳过本地玩家: {otherPlayer.SteamName}");
                            continue;
                        }
                        
                        Debug.Log($"[RoomManager] 🔥 为已在房间的玩家发布 PlayerJoinedRoomEvent: {otherPlayer.SteamName} (AvatarUrl: {otherPlayer.AvatarUrl ?? "(null)"})");
                        GameContext.Instance.EventBus.Publish(new PlayerJoinedRoomEvent(otherPlayer, evt.Room));
                        publishedCount++;
                    }
                    
                    Debug.Log($"[RoomManager] ✅ 共发布了 {publishedCount} 个 PlayerJoinedRoomEvent 事件");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[RoomManager] 获取房间玩家失败: {ex.Message}");
                }
            }
            else
            {

                Debug.Log($"[RoomManager] ✅ 玩家加入房间: {evt.Player.SteamName} → 自动更新列表");
                var idx = RoomPlayers.FindIndex(p => p.SteamId == evt.Player.SteamId);
                if (idx >= 0)
                {
                    RoomPlayers[idx] = evt.Player;
                    Debug.Log($"[RoomManager] 更新现有玩家信息: {evt.Player.SteamName}");
                }
                else
                {
                    RoomPlayers.Add(evt.Player);
                    Debug.Log($"[RoomManager] 添加新玩家: {evt.Player.SteamName}, 当前总数: {RoomPlayers.Count}");
                }

                // 🔥 预加载玩家头像
                if (GameContext.IsInitialized)
                {
                    GameContext.Instance.AvatarManager.PreloadAvatar(evt.Player.SteamId);
                }
            }



        }

        private void OnRoomLeft(RoomLeftEvent evt)
        {
            if (evt.Player.SteamId == GameContext.Instance.PlayerManager.LocalPlayer.Info.SteamId)
            {

                Debug.Log($"[RoomManager] 自己离开房间: {evt.Room.RoomId}");
                CurrentRoom = null;
                RoomPlayers.Clear();
            }
            else
            {
                Debug.Log($"[RoomManager] ❌ 玩家离开房间: {evt.Player.SteamName} → 自动更新列表");
                var idx = RoomPlayers.FindIndex(p => p.SteamId == evt.Player.SteamId);
                if (idx >= 0)
                {
                    RoomPlayers.RemoveAt(idx);
                    Debug.Log($"[RoomManager] 移除玩家: {evt.Player.SteamName}, 当前总数: {RoomPlayers.Count}");
                }
                else
                {
                    Debug.LogWarning($"[RoomManager] ⚠️ 尝试移除不存在的玩家: {evt.Player.SteamName}");
                }
            }
        }

        private void OnNetworkDisconnected(NetworkDisconnectedEvent evt)
        {
            Debug.Log($"[RoomManager] 🔥 网络断开连接，清理房间状态: {evt.Reason}");
            CurrentRoom = null;
            RoomPlayers.Clear();
            Debug.Log($"[RoomManager] ✅ 房间状态已清理");
        }

        public void Dispose()
        {
            _eventSubscriber.Dispose();
        }
    }
}