using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Client.RPC;
using DuckyNet.Shared.Services;

namespace DuckyNet.Client.UI
{
    /// <summary>
    /// 房间页面
    /// </summary>
    public class RoomPage
    {
        private readonly RpcClient _client;
        private readonly MainMenuWindow _mainWindow;
        private RoomInfo? _currentRoom;
        private Vector2 _scrollPos;
        private List<PlayerInfo> _roomPlayers = new List<PlayerInfo>();
        private ChatWindow? _chatWindow;

        public RoomPage(RpcClient client, MainMenuWindow mainWindow)
        {
            _client = client;
            _mainWindow = mainWindow;
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
            GUILayout.Label($"房主: {_currentRoom.HostPlayerId}");
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
                GUILayout.Label($"{player.SteamName} (Lv.{player.Level})");
                GUILayout.FlexibleSpace();
                GUILayout.Label(player.Status.ToString());
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
                Debug.Log($"[RoomPage] Refreshed player list: {players.Length} players");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoomPage] Refresh player list failed: {ex.Message}");
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
                    
                    _mainWindow.SwitchToPage(MainMenuWindow.Page.Lobby);
                    Debug.Log("[RoomPage] Successfully left room");
                }
                else
                {
                    Debug.LogWarning("[RoomPage] Failed to leave room (server returned false)");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RoomPage] Leave room error: {ex.Message}");
            }
        }
    }
}

