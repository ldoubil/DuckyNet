using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DuckyNet.Client.UI;
using DuckyNet.Client.RPC;
using DuckyNet.Client.Services;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services.Generated;
using System.Threading.Tasks;

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
            _eventSubscriber.Subscribe<PlayerJoinedRoomEvent>(OnPlayerJoinedRoom);
            _eventSubscriber.Subscribe<PlayerLeftRoomEvent>(OnPlayerLeftRoom);
            _eventSubscriber.Subscribe<RoomJoinedEvent>(OnRoomJoined);
            _eventSubscriber.Subscribe<RoomLeftEvent>(OnRoomLeft);
            Debug.Log("[RoomManager] 构造函数完成 (事件已订阅)");
            var serverContext = new ClientServerContext(GameContext.Instance.RpcClient);
            _roomServiceClient = new RoomServiceClientProxy(serverContext);
        }



        private void OnPlayerJoinedRoom(PlayerJoinedRoomEvent evt)
        {
            Debug.Log($"[RoomManager] 其他玩家 {evt.Player.SteamName} 进入房间: {evt.Room.RoomId}");
            var idx = RoomPlayers.FindIndex(p => p.SteamId == evt.Player.SteamId);
            if (idx >= 0)
            {
                RoomPlayers[idx] = evt.Player;
            }
            else
            {
                RoomPlayers.Add(evt.Player);
            }
        }

        private void OnPlayerLeftRoom(PlayerLeftRoomEvent evt)
        {
            Debug.Log($"[RoomManager] 其他玩家 {evt.Player.SteamName} 离开房间: {evt.Room.RoomId}");
            var idx = RoomPlayers.FindIndex(p => p.SteamId == evt.Player.SteamId);
            if (idx >= 0)
            {
                RoomPlayers.RemoveAt(idx);
            }
        }

        public IReadOnlyList<PlayerInfo> GetRoomPlayers() => RoomPlayers;

        public void SetCurrentRoom(RoomInfo room)
        {
            CurrentRoom = room;
            RefreshPlayerListAsync();
            if (GameContext.IsInitialized)
            {
                GameContext.Instance.EventBus.Publish(new RoomJoinedEvent(GameContext.Instance.LocalPlayer.Info, room));
            }
        }

        public async void RefreshPlayerListAsync()
        {
            if (CurrentRoom == null) return;
            try
            {
                var players = await _roomServiceClient.GetRoomPlayersAsync(CurrentRoom.RoomId);
                RoomPlayers = new List<PlayerInfo>(players);
                // 详情打印 RoomPlayers
                foreach (var player in RoomPlayers)
                {
                    Debug.Log($"[RoomManager] 玩家: {player.SteamName}, 场景: {player.CurrentScenelData.SceneName}, 子场景: {player.CurrentScenelData.SubSceneName}");
                }
                Debug.Log($"[RoomManager] 刷新房间玩家: {RoomPlayers.Count}");
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
                        GameContext.Instance.EventBus.Publish(new RoomLeftEvent(GameContext.Instance.LocalPlayer.Info, leftRoom ?? new RoomInfo()));
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
            Debug.Log($"[RoomManager] 自己进入房间: {evt.Room.RoomId}");
            CurrentRoom = evt.Room;
            try
            {
                var players = await _roomServiceClient.GetRoomPlayersAsync(evt.Room.RoomId);
                RoomPlayers = new List<PlayerInfo>(players);
                Debug.Log($"[RoomManager] 房间玩家: {string.Join(", ", RoomPlayers.Select(p => p.SteamName))}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoomManager] 获取房间玩家失败: {ex.Message}");
            }
        }

        private void OnRoomLeft(RoomLeftEvent evt)
        {
            Debug.Log($"[RoomManager] 自己离开房间: {evt.Room.RoomId}");
            CurrentRoom = null;
            RoomPlayers.Clear();
        }

        public void Dispose()
        {
            _eventSubscriber.Dispose();
        }
    }
}