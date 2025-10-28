using System;
using UnityEngine;
using DuckyNet.Client.Core;

namespace DuckyNet.Client
{
    /// <summary>
    /// DuckyNet 模组主行为类
    /// 负责模组生命周期管理，作为 Unity 和游戏系统的桥梁
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        /// <summary>
        /// 全局实例
        /// </summary>
        public static ModBehaviour? Instance { get; private set; }

        void Awake()
        {
            try
            {
                // 设置全局实例
                Instance = this;

                // 初始化调试控制台（最先初始化，方便查看后续日志）
                ConsoleModule.Initialize();
                ConsoleModule.WriteSeparator("DuckyNet 模组加载中");

                // 输出模组加载信息
                LogModInfo();

                // 初始化游戏上下文
                InitializeGameContext();

                ConsoleModule.WriteSeparator("DuckyNet 模组加载完成");
                Debug.Log("[DuckyNet] Mod Loaded!");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModBehaviour] 初始化失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 初始化游戏上下文和所有服务
        /// </summary>
        private void InitializeGameContext()
        {
            // 创建游戏上下文
            GameContext.Initialize();
            var context = GameContext.Instance;

            // 初始化并注册各个服务
            context.RegisterLocalPlayer(new LocalPlayer());
            context.RegisterRpcClient(new RPC.RpcClient());
            context.RegisterInputManager(new Core.InputManager());
            context.RegisterAvatarManager(new Core.AvatarManager());
            context.RegisterUIManager(new Core.UIManager(context.RpcClient));

            // 初始化 UI 系统
            context.UIManager.Initialize();

            // 注册输入按键
            RegisterInputKeys();

            // 订阅断开连接事件
            context.RpcClient.Disconnected += OnDisconnected;

            Debug.Log("[ModBehaviour] 游戏上下文初始化完成");
        }

        /// <summary>
        /// 注册所有输入按键
        /// </summary>
        private void RegisterInputKeys()
        {
            var inputManager = GameContext.Instance.InputManager;
            var uiManager = GameContext.Instance.UIManager;

            inputManager.RegisterKey(KeyCode.F10, () =>
            {
                uiManager.ToggleWindow("MainMenu");
                var window = uiManager.GetWindow<UI.MainMenuWindow>("MainMenu");
                Debug.Log($"[ModBehaviour] 主菜单 {(window?.IsVisible == true ? "已显示" : "已隐藏")}");
            }, "切换主菜单");

            inputManager.RegisterKey(KeyCode.F9, () =>
            {
                uiManager.ToggleWindow("Chat");
                var window = uiManager.GetWindow<UI.ChatWindow>("Chat");
                Debug.Log($"[ModBehaviour] 聊天窗口 {(window?.IsVisible == true ? "已显示" : "已隐藏")}");
            }, "切换聊天窗口");

            inputManager.RegisterKey(KeyCode.F6, () =>
            {
                uiManager.ToggleWindow("PlayerList");
                var window = uiManager.GetWindow<UI.PlayerListWindow>("PlayerList");
                Debug.Log($"[ModBehaviour] 玩家列表 {(window?.IsVisible == true ? "已显示" : "已隐藏")}");
            }, "切换玩家列表");

            Debug.Log("[ModBehaviour] 输入按键已注册");
        }

        /// <summary>
        /// 处理断开连接事件
        /// </summary>
        private void OnDisconnected(string reason)
        {
            try
            {
                Debug.LogWarning($"[ModBehaviour] 与服务器断开连接: {reason}");
                
                var chatWindow = GameContext.Instance.UIManager.GetWindow<UI.ChatWindow>("Chat");
                chatWindow?.AddSystemMessage($"与服务器断开连接: {reason}", Shared.Services.MessageType.Warning);
                
                Debug.Log("[ModBehaviour] 断开连接处理完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModBehaviour] 处理断开连接事件失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 输出模组加载信息
        /// </summary>
        private void LogModInfo()
        {
            Debug.Log("=== DuckyNet Mod 初始化 ===");
            Debug.Log("[DuckyNet] 模组版本: 1.0.0");
            Debug.Log($"[DuckyNet] Unity版本: {Application.unityVersion}");
            Debug.Log($"[DuckyNet] 加载时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Debug.Log("===========================");
        }

        void Update()
        {
            try
            {
                if (GameContext.IsInitialized)
                {
                    GameContext.Instance.Update();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModBehaviour] Update 方法出错: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        void OnGUI()
        {
            try
            {
                if (GameContext.IsInitialized)
                {
                    GameContext.Instance.OnGUI();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModBehaviour] OnGUI 方法出错: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        void OnDestroy()
        {
            try
            {
                Debug.Log("[ModBehaviour] Mod 卸载中...");

                // 取消事件订阅
                if (GameContext.IsInitialized)
                {
                    GameContext.Instance.RpcClient.Disconnected -= OnDisconnected;
                }

                // 清理游戏上下文（会自动清理所有服务）
                GameContext.Cleanup();

                Debug.Log("[ModBehaviour] Mod 已卸载");

                // 最后清理控制台（确保所有日志都能输出）
                ConsoleModule.WriteSeparator("DuckyNet 模组已卸载");
                ConsoleModule.Cleanup();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModBehaviour] 卸载失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }
    }
}
