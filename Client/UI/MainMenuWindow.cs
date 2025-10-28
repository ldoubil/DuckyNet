using System;
using UnityEngine;
using DuckyNet.Client.RPC;
using DuckyNet.Client.Core;
using DuckyNet.Shared.Services;

namespace DuckyNet.Client.UI
{
    /// <summary>
    /// 主菜单窗口
    /// </summary>
    public class MainMenuWindow : IUIWindow
    {
        private readonly RpcClient _client;
        private Rect _windowRect = new Rect(100, 100, 400, 300);
        private bool _isVisible = false;

        // 连接页面
        private string _serverAddress = "127.0.0.1";
        private string _serverPort = "2025";

        // 当前页面
        public enum Page { Connect, Lobby, Room }
        private Page _currentPage = Page.Connect;

        // 子页面
        public LobbyPage LobbyPage { get; private set; }
        public RoomPage RoomPage { get; private set; }

        public bool IsVisible => _isVisible;

        private ChatWindow? _chatWindow;

        public MainMenuWindow(RpcClient client, ChatWindow chatWindow)
        {
            _client = client;
            _chatWindow = chatWindow;
            LobbyPage = new LobbyPage(client, this);
            RoomPage = new RoomPage(client, this);
            
            // 将聊天窗口传递给 RoomPage
            RoomPage.SetChatWindow(chatWindow);

            // 订阅连接事件
            _client.Connected += OnConnected;
            _client.Disconnected += OnDisconnectedHandler;
        }

        public void Toggle()
        {
            _isVisible = !_isVisible;
        }

        public void Show()
        {
            _isVisible = true;
        }

        public void Hide()
        {
            _isVisible = false;
        }

        public void SwitchToPage(Page page)
        {
            _currentPage = page;
        }

        private void OnConnected()
        {
            Debug.Log("[MainMenu] Connected to server");
            // 连接成功后自动登录
            LoginAsync();
        }

        private void OnDisconnectedHandler(string reason)
        {
            Debug.Log($"[MainMenu] Disconnected: {reason}");
            _currentPage = Page.Connect;
        }

        public void OnGUI()
        {
            if (!_isVisible) return;

            _windowRect = GUILayout.Window(1000, _windowRect, DrawWindow, "DuckyNet 主菜单");
        }

        private void DrawWindow(int windowId)
        {
            GUILayout.BeginVertical();

            switch (_currentPage)
            {
                case Page.Connect:
                    DrawConnectPage();
                    break;
                case Page.Lobby:
                    LobbyPage.Draw();
                    break;
                case Page.Room:
                    RoomPage.Draw();
                    break;
            }

            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void DrawConnectPage()
        {
            GUILayout.Label("连接到服务器", GUI.skin.box);
            GUILayout.Space(10);

            GUILayout.BeginHorizontal();
            GUILayout.Label("服务器地址:", GUILayout.Width(100));
            _serverAddress = GUILayout.TextField(_serverAddress);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("端口:", GUILayout.Width(100));
            _serverPort = GUILayout.TextField(_serverPort);
            GUILayout.EndHorizontal();

            GUILayout.Space(10);

            // 显示玩家信息（只读）
            if (GameContext.IsInitialized)
            {
                var localPlayer = GameContext.Instance.LocalPlayer;
                var playerInfo = localPlayer.Info;
                
                GUILayout.BeginHorizontal();
                GUILayout.Label("玩家名称:", GUILayout.Width(100));
                GUILayout.Label(playerInfo.SteamName, GUI.skin.box);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Steam ID:", GUILayout.Width(100));
                GUILayout.Label(playerInfo.SteamId, GUI.skin.box);
                GUILayout.EndHorizontal();

                // 显示头像（如果已加载）
                if (localPlayer.AvatarTexture != null)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("头像:", GUILayout.Width(100));
                    GUILayout.Box(localPlayer.AvatarTexture, GUILayout.Width(64), GUILayout.Height(64));
                    GUILayout.EndHorizontal();
                }
            }

            GUILayout.Space(10);

            if (_client.IsConnected)
            {
                GUILayout.Label("● 已连接并登录", GUI.skin.box);

                if (GUILayout.Button("断开连接"))
                {
                    _client.Disconnect();
                }
            }
            else
            {
                if (GUILayout.Button("连接服务器"))
                {
                    Connect();
                }
            }
        }

        private void Connect()
        {
            try
            {
                if (!int.TryParse(_serverPort, out int port))
                {
                    Debug.LogError("[MainMenu] Invalid port number");
                    return;
                }

                _client.Connect(_serverAddress, port);
                Debug.Log($"[MainMenu] Connecting to {_serverAddress}:{port}...");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MainMenu] Connect failed: {ex.Message}");
            }
        }

        private async void LoginAsync()
        {
            try
            {
                // 使用本地玩家信息
                if (!GameContext.IsInitialized)
                {
                    Debug.LogError("[MainMenu] 游戏上下文未初始化");
                    return;
                }

                var localPlayer = GameContext.Instance.LocalPlayer;
                var serverContext = new ClientServerContext(_client);
                var result = await serverContext.InvokeAsync<IPlayerService, LoginResult>(
                    "LoginAsync", localPlayer.Info);

                if (result.Success)
                {
                    Debug.Log("[MainMenu] 登录成功！");
                    localPlayer.UpdateStatus(PlayerStatus.InLobby);
                    _currentPage = Page.Lobby;
                }
                else
                {
                    Debug.LogError($"[MainMenu] 登录失败: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MainMenu] 登录错误: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _client.Connected -= OnConnected;
            _client.Disconnected -= OnDisconnectedHandler;
        }
    }
}

