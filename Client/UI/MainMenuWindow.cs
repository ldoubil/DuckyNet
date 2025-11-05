using System;
using UnityEngine;
using DuckyNet.Client.RPC;
using DuckyNet.Client.Core;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.Services.Generated;


namespace DuckyNet.Client.UI
{
    /// <summary>
    /// 主菜单页面枚举
    /// </summary>
    public enum MainMenuPage
    {
        Connect,
        Lobby,
        Room
    }

    /// <summary>
    /// 主菜单窗口
    /// </summary>
    public class MainMenuWindow : IUIWindow
    {
        private readonly RpcClient _client;
        private Rect _windowRect = new Rect(100, 100, 400, 300);
        private bool _isVisible = false;

        private PlayerServiceClientProxy _playerServiceClient;

        // 连接页面
        private string _serverAddress = "127.0.0.1";
        private string _serverPort = "9050";
        private string _connectionStatus = "";
        private bool _isConnecting = false;

        // 当前页面
        
        private MainMenuPage _currentPage = MainMenuPage.Connect;

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
            var serverContext = new ClientServerContext(_client);
            _playerServiceClient = new PlayerServiceClientProxy(serverContext);
            // 订阅连接事件
            _client.Connected += OnConnected;
            _client.Disconnected += OnDisconnectedHandler;
            _client.ConnectionFailed += OnConnectionFailed;
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

        public void SwitchToPage(MainMenuPage page)
        {
            _currentPage = page;
        }

        private void OnConnected()
        {
            UnityEngine.Debug.Log("[MainMenu] Connected to server");
            _isConnecting = false;
            _connectionStatus = "✓ 已连接，正在登录...";
            // 连接成功后自动登录
            LoginAsync();
        }

        private void OnDisconnectedHandler(string reason)
        {
            UnityEngine.Debug.Log($"[MainMenu] Disconnected: {reason}");
            _isConnecting = false;
            _currentPage = MainMenuPage.Connect;
            
            // 如果不是主动断开，显示断开原因
            if (_connectionStatus != "")
            {
                _connectionStatus = $"✗ 已断开: {reason}";
            }
        }
        
        private void OnConnectionFailed(string errorMessage)
        {
            UnityEngine.Debug.LogError($"[MainMenu] Connection failed: {errorMessage}");
            _isConnecting = false;
            _connectionStatus = $"✗ {errorMessage}";
            
            // 在聊天窗口显示错误
            _chatWindow?.AddSystemMessage($"连接失败: {errorMessage}", MessageType.Error);
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
                case MainMenuPage.Connect:
                    DrawConnectPage();
                    break;
                case MainMenuPage.Lobby:
                    LobbyPage.Draw();
                    break;
                case MainMenuPage.Room:
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
                var localPlayer = GameContext.Instance.PlayerManager.LocalPlayer;
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

            // 显示连接状态
            if (!string.IsNullOrEmpty(_connectionStatus))
            {
                var style = new GUIStyle(GUI.skin.box);
                if (_connectionStatus.StartsWith("✓"))
                {
                    style.normal.textColor = Color.green;
                }
                else if (_connectionStatus.StartsWith("✗"))
                {
                    style.normal.textColor = Color.red;
                }
                else if (_connectionStatus.StartsWith("●"))
                {
                    style.normal.textColor = Color.yellow;
                }
                
                GUILayout.Label(_connectionStatus, style);
                GUILayout.Space(5);
            }

            if (_client.IsConnected)
            {
                GUILayout.Label("● 已连接并登录", GUI.skin.box);

                if (GUILayout.Button("断开连接"))
                {
                    _connectionStatus = "";
                    _client.Disconnect();
                }
            }
            else if (_isConnecting)
            {
                GUILayout.Label("● 正在连接...", GUI.skin.box);
                
                if (GUILayout.Button("取消"))
                {
                    _client.Disconnect();
                    _isConnecting = false;
                    _connectionStatus = "✗ 已取消连接";
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
                    _connectionStatus = "✗ 端口号无效";
                    UnityEngine.Debug.LogError("[MainMenu] Invalid port number");
                    return;
                }

                if (string.IsNullOrWhiteSpace(_serverAddress))
                {
                    _connectionStatus = "✗ 服务器地址不能为空";
                    UnityEngine.Debug.LogError("[MainMenu] Server address is empty");
                    return;
                }

                _isConnecting = true;
                _connectionStatus = $"● 正在连接 {_serverAddress}:{port}...";
                _client.Connect(_serverAddress, port);
                UnityEngine.Debug.Log($"[MainMenu] Connecting to {_serverAddress}:{port}...");
            }
            catch (Exception ex)
            {
                _isConnecting = false;
                _connectionStatus = $"✗ 连接失败: {ex.Message}";
                UnityEngine.Debug.LogError($"[MainMenu] Connect failed: {ex.Message}");
            }
        }

        private async void LoginAsync()
        {
            try
            {
                // 使用本地玩家信息
                if (!GameContext.IsInitialized)
                {
                    UnityEngine.Debug.LogError("[MainMenu] 游戏上下文未初始化");
                    return;
                }

                var localPlayer = GameContext.Instance.PlayerManager.LocalPlayer;
                var serverContext = new ClientServerContext(_client);

                var result = await _playerServiceClient.LoginAsync(localPlayer.Info);

                if (result.Success)
                {
                    UnityEngine.Debug.Log("[MainMenu] 登录成功！");
                    _currentPage = MainMenuPage.Lobby;
                }
                else
                {
                    UnityEngine.Debug.LogError($"[MainMenu] 登录失败: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[MainMenu] 登录错误: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _client.Connected -= OnConnected;
            _client.Disconnected -= OnDisconnectedHandler;
            _client.ConnectionFailed -= OnConnectionFailed;
            
            // 清理子页面资源
            RoomPage?.Dispose();
        }
    }
}

