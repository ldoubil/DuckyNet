using System;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Client.Core.Utils;
using HarmonyLib;

namespace DuckyNet.Client
{
    /// <summary>
    /// DuckyNet 模组主行为类
    /// 负责模组生命周期管理，作为 Unity 和游戏系统的桥梁
    /// </summary>
    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private readonly EventSubscriberHelper _eventSub = new EventSubscriberHelper();
        /// <summary>
        /// 全局实例
        /// </summary>
        public static ModBehaviour? Instance { get; private set; }

        /// <summary>
        /// Harmony 实例
        /// </summary>
        private static Harmony? _harmony;

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
                
                // 初始化 Harmony 并应用所有 Patch
                InitializeHarmony();

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
            context.RegisterLocalPlayer(new Core.LocalPlayer());
            context.RegisterRpcClient(new RPC.RpcClient());
            context.RegisterInputManager(new Core.InputManager());
            context.RegisterAvatarManager(new Core.AvatarManager());
            var unitManager = new Core.UnitManager();
            context.RegisterUnitManager(unitManager);
            // 确保 UnitManager 订阅事件
            unitManager.EnsureSubscribed();
            context.RegisterCharacterCustomizationManager(new Core.CharacterCustomizationManager());
            context.RegisterSceneClientManager(new Core.SceneClientManager());
            context.RegisterRoomManager(new Core.RoomManager());
            context.RegisterUIManager(new Core.UIManager(context.RpcClient));

            // 注册客户端服务
            context.RpcClient.RegisterClientService<Shared.Services.IPlayerClientService>(new Services.PlayerClientServiceImpl());
            context.RpcClient.RegisterClientService<Shared.Services.IRoomClientService>(new Services.RoomClientServiceImpl());
            context.RpcClient.RegisterClientService<Shared.Services.ISceneClientService>(new Services.SceneClientServiceImpl());
            context.RpcClient.RegisterClientService<Shared.Services.ICharacterClientService>(new Services.CharacterClientServiceImpl());
            context.RpcClient.RegisterClientService<Shared.Services.ICharacterSyncClientService>(new Services.CharacterSyncClientServiceImpl());

            // 初始化 UI 系统
            context.UIManager.Initialize();

            // 注册输入按键
            RegisterInputKeys();

            // 创建并初始化场景事件桥接器（仅转发进入/离开地图事件）
            var sceneBridge = new Patches.SceneEventBridge();
            sceneBridge.Initialize();
            // 初始化场景信息提供者（用于查询当前场景）
            Core.Helpers.SceneInfoProvider.Initialize(sceneBridge);

            // 创建网络生命周期管理器
            var lifecycleManager = new Core.NetworkLifecycleManager(context);

            // 订阅连接/断开连接事件
            context.RpcClient.Connected += () => lifecycleManager.HandleConnected();
            context.RpcClient.Disconnected += lifecycleManager.HandleDisconnected;

            // 启动角色外观自动上传
            CharacterAppearanceHelper.StartAutoUpload();

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

            inputManager.RegisterKey(KeyCode.F3, () =>
            {
                uiManager.ToggleWindow("Debug");
                var window = uiManager.GetWindow<UI.DebugWindow>("Debug");
                Debug.Log($"[ModBehaviour] 调试窗口 {(window?.IsVisible == true ? "已显示" : "已隐藏")}");
            }, "切换调试窗口（包含所有调试模块）");

            Debug.Log("[ModBehaviour] 输入按键已注册");
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

        /// <summary>
        /// 初始化 Harmony 并应用所有 Patch
        /// </summary>
        private void InitializeHarmony()
        {
            try
            {
                const string harmonyId = "com.duckynet.client";
                
                Debug.Log($"[ModBehaviour] 初始化 Harmony (ID: {harmonyId})");
                
                // 创建 Harmony 实例
                _harmony = new Harmony(harmonyId);
                
                // 应用所有 Patch（会自动扫描带有 [HarmonyPatch] 特性的类）
                _harmony.PatchAll(typeof(ModBehaviour).Assembly);
                
                Debug.Log("[ModBehaviour] ✅ Harmony Patch 已全部应用");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModBehaviour] Harmony 初始化失败: {ex.Message}");
                Debug.LogException(ex);
            }
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

                // 释放事件订阅
                _eventSub.Dispose();

                // 取消 Harmony Patch
                if (_harmony != null)
                {
                    _harmony.UnpatchAll("com.duckynet.client");
                    Debug.Log("[ModBehaviour] Harmony Patch 已移除");
                }

                // 注意：RPC 客户端会在 Disconnect 时自动清理事件订阅

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
