using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Client.RPC;
using DuckyNet.Shared.Services;

namespace DuckyNet.Client.UI
{
    /// <summary>
    /// 大厅页面
    /// </summary>
    public class LobbyPage
    {
        private readonly RpcClient _client;
        private readonly MainMenuWindow _mainWindow;
        private Vector2 _scrollPos;
        private List<RoomInfo> _roomList = new List<RoomInfo>();

        // 创建房间输入
        private string _newRoomName = "我的房间";
        private string _newRoomPassword = "";
        private string _newRoomDescription = "";
        private int _newRoomMaxPlayers = 8;

        public LobbyPage(RpcClient client, MainMenuWindow mainWindow)
        {
            _client = client;
            _mainWindow = mainWindow;
        }

        public void Draw()
        {
            GUILayout.Label("游戏大厅", GUI.skin.box);
            GUILayout.Space(10);

            // 刷新房间列表按钮
            if (GUILayout.Button("刷新房间列表"))
            {
                RefreshRoomListAsync();
            }

            GUILayout.Space(10);

            // 房间列表
            GUILayout.Label($"房间列表 ({_roomList.Count})", GUI.skin.box);
            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(150));
            
            foreach (var room in _roomList)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);
                
                string lockIcon = room.RequirePassword ? "🔒" : "🔓";
                GUILayout.Label($"{lockIcon} {room.RoomName}");
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{room.CurrentPlayers}/{room.MaxPlayers}");
                
                if (GUILayout.Button("加入", GUILayout.Width(60)))
                {
                    JoinRoomAsync(room.RoomId, room.RequirePassword);
                }
                
                GUILayout.EndHorizontal();
            }
            
            GUILayout.EndScrollView();

            GUILayout.Space(10);

            // 创建房间
            GUILayout.Label("创建房间", GUI.skin.box);
            
            GUILayout.BeginHorizontal();
            GUILayout.Label("房间名称:", GUILayout.Width(80));
            _newRoomName = GUILayout.TextField(_newRoomName);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("密码:", GUILayout.Width(80));
            _newRoomPassword = GUILayout.TextField(_newRoomPassword);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("描述:", GUILayout.Width(80));
            _newRoomDescription = GUILayout.TextField(_newRoomDescription);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("最大人数:", GUILayout.Width(80));
            _newRoomMaxPlayers = (int)GUILayout.HorizontalSlider(_newRoomMaxPlayers, 2, 16);
            GUILayout.Label(_newRoomMaxPlayers.ToString(), GUILayout.Width(30));
            GUILayout.EndHorizontal();

            if (GUILayout.Button("创建房间"))
            {
                CreateRoomAsync();
            }
        }

        private async void RefreshRoomListAsync()
        {
            try
            {
                var serverContext = new ClientServerContext(_client);
                var rooms = await serverContext.InvokeAsync<IRoomService, RoomInfo[]>(
                    "GetRoomListAsync");
                
                _roomList = new List<RoomInfo>(rooms);
                Debug.Log($"[LobbyPage] Refreshed room list: {rooms.Length} rooms");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyPage] Refresh room list failed: {ex.Message}");
            }
        }

        private async void CreateRoomAsync()
        {
            try
            {
                var serverContext = new ClientServerContext(_client);
                var request = new CreateRoomRequest
                {
                    RoomName = _newRoomName,
                    Password = _newRoomPassword,
                    Description = _newRoomDescription,
                    MaxPlayers = _newRoomMaxPlayers
                };

                var result = await serverContext.InvokeAsync<IRoomService, RoomOperationResult>(
                    "CreateRoomAsync", request);

                if (result.Success && result.Room != null)
                {
                    Debug.Log($"[LobbyPage] Room created: {result.Room.RoomId}");
                    _mainWindow.RoomPage.SetCurrentRoom(result.Room);
                    _mainWindow.SwitchToPage(MainMenuWindow.Page.Room);
                }
                else
                {
                    Debug.LogError($"[LobbyPage] Create room failed: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyPage] Create room error: {ex.Message}");
            }
        }

        private async void JoinRoomAsync(string roomId, bool requirePassword)
        {
            try
            {
                string password = "";
                if (requirePassword)
                {
                    // TODO: 显示密码输入对话框
                    password = "1234"; // 临时测试值
                }

                var serverContext = new ClientServerContext(_client);
                var request = new JoinRoomRequest
                {
                    RoomId = roomId,
                    Password = password
                };

                var result = await serverContext.InvokeAsync<IRoomService, RoomOperationResult>(
                    "JoinRoomAsync", request);

                if (result.Success && result.Room != null)
                {
                    Debug.Log($"[LobbyPage] Joined room: {roomId}");
                    _mainWindow.RoomPage.SetCurrentRoom(result.Room);
                    _mainWindow.SwitchToPage(MainMenuWindow.Page.Room);
                }
                else
                {
                    Debug.LogError($"[LobbyPage] Join room failed: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LobbyPage] Join room error: {ex.Message}");
            }
        }
    }
}

