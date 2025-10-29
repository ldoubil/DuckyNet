using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Client.RPC;
using DuckyNet.Client.Core;
using DuckyNet.Shared.Services;

namespace DuckyNet.Client.UI
{
    /// <summary>
    /// 玩家列表窗口
    /// </summary>
    public class PlayerListWindow : IUIWindow
    {
        private readonly RpcClient _client;
        private Rect _windowRect = new Rect(Screen.width - 320, 100, 300, 400);
        private bool _isVisible = false;
        
        private List<PlayerInfo> _players = new List<PlayerInfo>();
        private Vector2 _scrollPos;

        public bool IsVisible => _isVisible;

        public PlayerListWindow(RpcClient client)
        {
            _client = client;
        }

        public void Show()
        {
            _isVisible = true;
            RefreshPlayerListAsync();
        }

        public void Hide()
        {
            _isVisible = false;
        }

        public void Toggle()
        {
            _isVisible = !_isVisible;
            
            if (_isVisible)
            {
                RefreshPlayerListAsync();
            }
        }

        public void OnGUI()
        {
            if (!_isVisible) return;

            _windowRect = GUILayout.Window(1002, _windowRect, DrawWindow, "在线玩家");
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();

            // 刷新按钮
            if (GUILayout.Button("刷新"))
            {
                RefreshPlayerListAsync();
            }

            GUILayout.Label($"在线玩家 ({_players.Count})", GUI.skin.box);

            // 玩家列表
            _scrollPos = GUILayout.BeginScrollView(_scrollPos);
            
            foreach (var player in _players)
            {
                GUILayout.BeginVertical(GUI.skin.box);
                
                GUILayout.Label($"{player.SteamName} (Lv.{player.Level})");
                GUILayout.Label($"状态: {player.Status}");
                
                GUILayout.EndVertical();
                GUILayout.Space(2);
            }
            
            GUILayout.EndScrollView();

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private async void RefreshPlayerListAsync()
        {
            if (!_client.IsConnected) return;

            try
            {
                var serverContext = new ClientServerContext(_client);
                var players = await serverContext.InvokeAsync<IPlayerService, PlayerInfo[]>(
                    "GetAllOnlinePlayersAsync");
                
                _players = new List<PlayerInfo>(players);
                UnityEngine.Debug.Log($"[PlayerListWindow] Refreshed: {players.Length} players");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[PlayerListWindow] Refresh failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _players.Clear();
        }
    }
}

