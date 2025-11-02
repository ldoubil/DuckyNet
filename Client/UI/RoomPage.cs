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
        private readonly MainMenuWindow? _mainWindow;
        private Vector2 _scrollPos;
        private readonly RoomManager _manager = GameContext.Instance.RoomManager;
        private ChatWindow? _chatWindow;
        public RoomPage(RpcClient client, MainMenuWindow mainWindow)
        {
            _client = client;
            _mainWindow = mainWindow;
        }

        public void Dispose()
        {
            _manager?.Dispose();
        }

        public void SetChatWindow(ChatWindow chatWindow)
        {
            _chatWindow = chatWindow;
        }

        public void SetCurrentRoom(RoomInfo room)
        {
            _manager.SetCurrentRoom(room);

            // 通知聊天窗口已进入房间
            _chatWindow?.SetRoomStatus(true);
        }

        public void Draw()
        {
            if (_manager.CurrentRoom == null)
            {
                GUILayout.Label("未在房间中", GUI.skin.box);
                return;
            }

            GUILayout.Label($"房间: {_manager.CurrentRoom.RoomName}", GUI.skin.box);
            GUILayout.Space(10);

            // 房间信息
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"房间ID: {_manager.CurrentRoom.RoomId}");
            GUILayout.Label($"描述: {_manager.CurrentRoom.Description}");
            GUILayout.Label($"人数: {_manager.CurrentRoom.CurrentPlayers}/{_manager.CurrentRoom.MaxPlayers}");
            GUILayout.Label($"房主: {_manager.CurrentRoom.HostSteamId}");
            GUILayout.EndVertical();

            GUILayout.Space(10);

            // 玩家列表（自动更新）
            var playersView = _manager.GetRoomPlayers();
            GUILayout.Label($"房间玩家 ({playersView.Count}) - 自动刷新", GUI.skin.box);
            
            // 手动刷新按钮（可选，通常不需要）
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("🔄 手动刷新"))
            {
                _manager.RefreshPlayerListAsync();
            }
            GUILayout.Label("💡 列表会自动更新", GUI.skin.label);
            GUILayout.EndHorizontal();

            _scrollPos = GUILayout.BeginScrollView(_scrollPos, GUILayout.Height(150));

            foreach (var player in playersView)
            {
                GUILayout.BeginHorizontal(GUI.skin.box);

                // 头像
                var avatar = GameContext.Instance.AvatarManager.GetAvatar(player.SteamId);
                if (avatar != null)
                {
                    GUILayout.Label(avatar, GUILayout.Width(48), GUILayout.Height(48));
                }
                else
                {
                    GUILayout.Box("", GUILayout.Width(48), GUILayout.Height(48));
                }

                GUILayout.Space(8);

                // 文本信息（名称、场景、子场景）
                GUILayout.BeginVertical();
                GUILayout.Label(player.SteamName, GUI.skin.label);
                var sceneName = player.CurrentScenelData?.SceneName ?? "";
                var subSceneName = player.CurrentScenelData?.SubSceneName ?? "";
                GUILayout.Label($"场景: {sceneName}", GUI.skin.label);
                GUILayout.Label($"子场景: {subSceneName}", GUI.skin.label);
                GUILayout.EndVertical();
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

        // 刷新逻辑已迁移至 RoomPageManager

        private async void LeaveRoomAsync()
        {
            var success = await _manager.LeaveRoomAsync();
            if (success)
            {
                _manager.SetCurrentRoom(new RoomInfo());
                _chatWindow?.SetRoomStatus(false);
                _mainWindow?.SwitchToPage(MainMenuPage.Lobby);
            }
            else
            {
                UnityEngine.Debug.LogWarning("[RoomPage] Failed to leave room (server returned false)");
            }
        }
    }
}

