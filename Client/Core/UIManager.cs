using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Client.UI;
using DuckyNet.Client.RPC;
using DuckyNet.Client.Services;
using DuckyNet.Shared.Services;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// UI 管理器
    /// 负责管理所有 UI 窗口的生命周期、显示和隐藏
    /// </summary>
    public class UIManager : IDisposable
    {
        private readonly RpcClient _rpcClient;
        private readonly Dictionary<string, IUIWindow> _windows;

        // UI 窗口
        private MainMenuWindow? _mainMenuWindow;
        private ChatWindow? _chatWindow;
        private PlayerListWindow? _playerListWindow;
        private DebugWindow? _debugWindow;
        private AnimationDebugWindow? _animationDebugWindow;
        private AnimatorStateViewer? _animatorStateViewer;

        // 服务实现
        private PlayerClientServiceImpl? _playerClientService;
        private RoomClientServiceImpl? _roomClientService;

        public UIManager(RpcClient rpcClient)
        {
            _rpcClient = rpcClient ?? throw new ArgumentNullException(nameof(rpcClient));
            _windows = new Dictionary<string, IUIWindow>();
        }

        /// <summary>
        /// 初始化所有 UI 窗口
        /// </summary>
        public void Initialize()
        {
            try
            {
                Debug.Log("[UIManager] 开始初始化 UI 系统...");

                // 注册客户端服务
                RegisterClientServices();

                // 创建聊天窗口
                _chatWindow = new ChatWindow(_rpcClient);
                RegisterWindow("Chat", _chatWindow);

                // 订阅聊天消息事件
                if (_playerClientService != null)
                {
                    _playerClientService.OnChatMessageReceived += _chatWindow.AddMessage;
                }

                // 创建玩家列表窗口
                _playerListWindow = new PlayerListWindow(_rpcClient);
                RegisterWindow("PlayerList", _playerListWindow);

                // 创建主菜单窗口
                _mainMenuWindow = new MainMenuWindow(_rpcClient, _chatWindow);
                RegisterWindow("MainMenu", _mainMenuWindow);

                // 创建调试窗口
                _debugWindow = new DebugWindow(_rpcClient);
                RegisterWindow("Debug", _debugWindow);

            // 创建动画调试窗口
            _animationDebugWindow = new AnimationDebugWindow();
            RegisterWindow("AnimationDebug", _animationDebugWindow);
            Debug.Log("[UIManager] 动画调试窗口已创建");

            // 创建动画状态机可视化窗口
            _animatorStateViewer = new AnimatorStateViewer();
            RegisterWindow("AnimatorStateViewer", _animatorStateViewer);
            Debug.Log("[UIManager] 动画状态机可视化窗口已创建");

                Debug.Log($"[UIManager] UI 系统初始化完成，共注册 {_windows.Count} 个窗口");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIManager] UI 系统初始化失败: {ex.Message}");
                Debug.LogException(ex);
                throw;
            }
        }

        /// <summary>
        /// 注册客户端 RPC 服务
        /// </summary>
        private void RegisterClientServices()
        {
            _playerClientService = new PlayerClientServiceImpl();
            _rpcClient.RegisterClientService<IPlayerClientService>(_playerClientService);

            _roomClientService = new RoomClientServiceImpl();
            _rpcClient.RegisterClientService<IRoomClientService>(_roomClientService);

            Debug.Log("[UIManager] 客户端服务已注册");
        }

        /// <summary>
        /// 注册窗口到管理器
        /// </summary>
        private void RegisterWindow(string name, IUIWindow window)
        {
            if (_windows.ContainsKey(name))
            {
                Debug.LogWarning($"[UIManager] 窗口 '{name}' 已存在，将被覆盖");
            }
            _windows[name] = window;
            Debug.Log($"[UIManager] 窗口 '{name}' 已注册");
        }

        /// <summary>
        /// 获取窗口
        /// </summary>
        public T? GetWindow<T>(string name) where T : class, IUIWindow
        {
            if (_windows.TryGetValue(name, out var window))
            {
                return window as T;
            }
            return null;
        }

        /// <summary>
        /// 显示窗口
        /// </summary>
        public void ShowWindow(string name)
        {
            if (_windows.TryGetValue(name, out var window))
            {
                window.Show();
                Debug.Log($"[UIManager] 窗口 '{name}' 已显示");
            }
            else
            {
                Debug.LogWarning($"[UIManager] 窗口 '{name}' 不存在");
            }
        }

        /// <summary>
        /// 隐藏窗口
        /// </summary>
        public void HideWindow(string name)
        {
            if (_windows.TryGetValue(name, out var window))
            {
                window.Hide();
                Debug.Log($"[UIManager] 窗口 '{name}' 已隐藏");
            }
            else
            {
                Debug.LogWarning($"[UIManager] 窗口 '{name}' 不存在");
            }
        }

        /// <summary>
        /// 切换窗口显示状态
        /// </summary>
        public void ToggleWindow(string name)
        {
            if (_windows.TryGetValue(name, out var window))
            {
                Debug.Log($"[UIManager] 切换窗口: {name}");
                window.Toggle();
            }
            else
            {
                Debug.LogWarning($"[UIManager] 窗口 '{name}' 不存在，已注册窗口: {string.Join(", ", _windows.Keys)}");
            }
        }

        /// <summary>
        /// 隐藏所有窗口
        /// </summary>
        public void HideAllWindows()
        {
            foreach (var window in _windows.Values)
            {
                window.Hide();
            }
            Debug.Log("[UIManager] 所有窗口已隐藏");
        }

        /// <summary>
        /// 更新所有窗口（每帧调用）
        /// </summary>
        public void Update()
        {
            try
            {
                // 更新调试窗口（模块需要 Update）
                _debugWindow?.Update();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIManager] 更新窗口时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 渲染所有窗口
        /// </summary>
        public void OnGUI()
        {
            try
            {
                foreach (var window in _windows.Values)
                {
                    window.OnGUI();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIManager] 渲染窗口时出错: {ex.Message}");
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                // 取消事件订阅
                if (_playerClientService != null && _chatWindow != null)
                {
                    _playerClientService.OnChatMessageReceived -= _chatWindow.AddMessage;
                }

                // 清理所有窗口
                foreach (var kvp in _windows)
                {
                    try
                    {
                        kvp.Value.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"[UIManager] 清理窗口 '{kvp.Key}' 失败: {ex.Message}");
                    }
                }

                _windows.Clear();

                _mainMenuWindow = null;
                _chatWindow = null;
                _playerListWindow = null;
                _debugWindow = null;
                _playerClientService = null;
                _roomClientService = null;

                Debug.Log("[UIManager] UI 管理器已清理");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[UIManager] 清理失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// UI 窗口接口
    /// </summary>
    public interface IUIWindow : IDisposable
    {
        bool IsVisible { get; }
        void Show();
        void Hide();
        void Toggle();
        void OnGUI();
    }
}

