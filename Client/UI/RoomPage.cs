using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Client.RPC;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Shared.Services;

namespace DuckyNet.Client.UI
{
    /// <summary>
    /// 房间页面
    /// </summary>
    public class RoomPage : IDisposable
    {
        private readonly RpcClient _client;
        private readonly MainMenuWindow _mainWindow;
        private RoomInfo? _currentRoom;
        private Vector2 _scrollPos;
        private List<PlayerInfo> _roomPlayers = new List<PlayerInfo>();
        private ChatWindow? _chatWindow;
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();

        public RoomPage(RpcClient client, MainMenuWindow mainWindow)
        {
            _client = client;
            _mainWindow = mainWindow;

            // 订阅房间玩家变化事件（自动更新玩家列表）
            if (GameContext.IsInitialized)
            {
                SubscribeToEvents();
            }
        }

        /// <summary>
        /// 订阅 EventBus 事件
        /// </summary>
        private void SubscribeToEvents()
        {
            _eventSubscriber.EnsureInitializedAndSubscribe();
            
            // 订阅玩家加入/离开房间事件
            _eventSubscriber.Subscribe<PlayerJoinedRoomEvent>(OnPlayerJoinedRoom);
            _eventSubscriber.Subscribe<PlayerLeftRoomEvent>(OnPlayerLeftRoom);
        }

        /// <summary>
        /// 处理玩家加入房间事件
        /// </summary>
        private void OnPlayerJoinedRoom(PlayerJoinedRoomEvent evt)
        {
            // 如果是当前房间的玩家，刷新列表
            if (_currentRoom != null && evt.Room.RoomId == _currentRoom.RoomId)
            {
                RefreshPlayerListAsync();
            }
        }

        /// <summary>
        /// 处理玩家离开房间事件
        /// </summary>
        private void OnPlayerLeftRoom(PlayerLeftRoomEvent evt)
        {
            // 如果是当前房间的玩家，刷新列表
            if (_currentRoom != null && evt.Room.RoomId == _currentRoom.RoomId)
            {
                RefreshPlayerListAsync();
            }
        }

        public void Dispose()
        {
            _eventSubscriber?.Dispose();
        }

        public void SetChatWindow(ChatWindow chatWindow)
        {
            _chatWindow = chatWindow;
        }

        public void SetCurrentRoom(RoomInfo room)
        {
            _currentRoom = room;
            RefreshPlayerListAsync();

            // 通知聊天窗口已进入房间
            _chatWindow?.SetRoomStatus(true);
            GameContext.Instance.EventBus.Publish(new RoomJoinedEvent(GameContext.Instance.LocalPlayer.Info, room));
        }

        public void Draw()
        {
            if (_currentRoom == null)
            {
                GUILayout.Label("未在房间中", GUI.skin.box);
                return;
            }

            GUILayout.Label($"房间: {_currentRoom.RoomName}", GUI.skin.box);
            GUILayout.Space(10);

            // 房间信息
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"房间ID: {_currentRoom.RoomId}");
            GUILayout.Label($"描述: {_currentRoom.Description}");
            GUILayout.Label($"人数: {_currentRoom.CurrentPlayers}/{_currentRoom.MaxPlayers}");
            GUILayout.Label($"房主: {_currentRoom.HostSteamId}");
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // 玩家列表
            GUILayout.Label($"房间玩家 ({_roomPlayers.Count})", GUI.skin.box);
            
            if (GUILayout.Button("刷新玩家列表"))
            {
                RefreshPlayerListAsync();
            }

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(150));
            
            foreach (var player in _roomPlayers)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }
            
            GUILayout.EndScrollView();

            GUILayout.Space(10);

            // 提示信息
            GUILayout.Label("💡 提示: 在房间内即可与其他玩家交换数据", GUI.skin.box);
            
            GUILayout.Space(5);

            // 房间控制
            if (GUILayout.Button("离开房间"))
            {
                LeaveRoomAsync();
            }
        }

        private async void RefreshPlayerListAsync()
        {
            if (_currentRoom == null) return;

            try
            {
                var serverContext = new ClientServerContext(_client);
                var players = await serverContext.InvokeAsync<IRoomService, PlayerInfo[]>(
                    "GetRoomPlayersAsync", _currentRoom.RoomId);
                
                _roomPlayers = new List<PlayerInfo>(players);
                UnityEngine.Debug.Log($"[RoomPage] Refreshed player list: {players.Length} players");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[RoomPage] Refresh player list failed: {ex.Message}");
            }
        }


        private async void LeaveRoomAsync()
        {
            try
            {
                var serverContext = new ClientServerContext(_client);
                bool success = await serverContext.InvokeAsync<IRoomService, bool>("LeaveRoomAsync");
                
                if (success)
                {
                    _currentRoom = null;
                    _roomPlayers.Clear();
                    
                    // 通知聊天窗口已离开房间
                    _chatWindow?.SetRoomStatus(false);
                    
                    // 通过事件发布操作，而不是直接调用管理器方法
                    if (GameContext.IsInitialized)
                    {
                        var eventBus = GameContext.Instance.EventBus;
                        eventBus.Publish(SyncStopRequestEvent.Instance);
                        // 注意：SceneManager.OnLeftRoom() 仍在内部使用，但离开房间通常由 NetworkLifecycleManager 处理
                        // 这里可以发布 RoomLeftEvent，NetworkLifecycleManager 会处理
                        UnityEngine.Debug.Log("[RoomPage] 已发布停止同步事件");
                    }
                    
                    _mainWindow.SwitchToPage(MainMenuPage.Lobby);
                    UnityEngine.Debug.Log("[RoomPage] Successfully left room");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[RoomPage] Failed to leave room (server returned false)");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[RoomPage] Leave room error: {ex.Message}");
            }
        }
    }
}

