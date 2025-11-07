using System;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Client.Core.EventBus;
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
        
        /// <summary>
        /// 单位生命周期管理器
        /// </summary>
        private Core.CharacterLifecycleManager? _characterLifecycleManager;
        
        /// <summary>
        /// 本地玩家开枪事件桥接器
        /// </summary>
        private Patches.LocalPlayerShootBridge? _localPlayerShootBridge;

        void Awake()
        {
            try
            {
                // 设置全局实例
                Instance = this;

                // 输出模组加载信息
                LogModInfo();
                
                // 初始化 Harmony 并应用所有 Patch
                InitializeHarmony();

                // 初始化游戏上下文
                InitializeGameContext();

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
            context.RegisterPlayerManager(new Core.Players.PlayerManager());
            context.RegisterRpcClient(new RPC.RpcClient());
            context.RegisterInputManager(new Core.InputManager());
            context.RegisterAvatarManager(new Core.AvatarManager());
            // 确保 UnitManager 订阅事件
            context.RegisterCharacterCustomizationManager(new Core.CharacterCustomizationManager());
            context.RegisterSceneClientManager(new Core.SceneClientManager());
            context.RegisterRoomManager(new Core.RoomManager());
            context.RegisterAnimatorSyncManager(new Core.AnimatorSyncManager());
            context.RegisterUIManager(new Core.UIManager(context.RpcClient));

            // 注册客户端服务
            context.RpcClient.RegisterClientService<Shared.Services.IPlayerClientService>(new Services.PlayerClientServiceImpl());
            context.RpcClient.RegisterClientService<Shared.Services.IRoomClientService>(new Services.RoomClientServiceImpl());
            context.RpcClient.RegisterClientService<Shared.Services.ISceneClientService>(new Services.SceneClientServiceImpl());
            context.RpcClient.RegisterClientService<Shared.Services.ICharacterClientService>(new Services.CharacterClientServiceImpl());
            context.RpcClient.RegisterClientService<Shared.Services.ICharacterAppearanceClientService>(new Services.CharacterAppearanceClientServiceImpl());
            
            // 注册动画同步客户端服务并保存实例
            var animatorSyncClientService = new Services.AnimatorSyncClientServiceImpl();
            context.RpcClient.RegisterClientService<Shared.Services.IAnimatorSyncClientService>(animatorSyncClientService);
            context.AnimatorSyncClientService = animatorSyncClientService;

            // 注册物品同步服务
            var itemSyncClientService = new Services.ItemSyncClientServiceImpl();
            context.RpcClient.RegisterClientService<Shared.Services.IItemSyncClientService>(itemSyncClientService);

            // 注册装备同步服务
            var equipmentClientService = new Services.EquipmentClientServiceImpl();
            context.RpcClient.RegisterClientService<Shared.Services.IEquipmentClientService>(equipmentClientService);

            // 注册武器同步服务
            var weaponSyncClientService = new Services.WeaponSyncClientServiceImpl();
            context.RpcClient.RegisterClientService<Shared.Services.IWeaponSyncClientService>(weaponSyncClientService);

            // 注册血量同步服务
            var healthSyncClientService = new Services.HealthSyncClientServiceImpl();
            context.RpcClient.RegisterClientService<Shared.Services.IHealthSyncClientService>(healthSyncClientService);

            // 创建并注册物品网络协调器（需要在 RpcClient 之后）
            // 使用生成的 ClientProxy 来调用服务器
            var clientContext = new RPC.ClientServerContext(context.RpcClient);
            var itemSyncServiceProxy = new Shared.Services.Generated.ItemSyncServiceClientProxy(clientContext);
            var itemNetworkCoordinator = new Services.ItemNetworkCoordinator(itemSyncServiceProxy);
            context.RegisterItemNetworkCoordinator(itemNetworkCoordinator);

            // 初始化动画同步管理器（需要在 PlayerManager 之后）
            context.AnimatorSyncManager.Initialize();

            // 初始化 UI 系统
            context.UIManager.Initialize();

            // 注册输入按键
            RegisterInputKeys();

            // 创建并初始化场景事件桥接器（仅转发进入/离开地图事件）
            var sceneBridge = new Patches.SceneEventBridge();
            sceneBridge.Initialize();
            // 初始化场景信息提供者（用于查询当前场景）
            Core.Helpers.SceneInfoProvider.Initialize(sceneBridge);

            // 创建并初始化单位生命周期管理器（监控怪物/NPC 创建、销毁、死亡）
            _characterLifecycleManager = new Core.CharacterLifecycleManager();
            Debug.Log("[ModBehaviour] 单位生命周期管理器已初始化");

            // 创建并初始化本地玩家开枪事件桥接器
            _localPlayerShootBridge = new Patches.LocalPlayerShootBridge();
            _localPlayerShootBridge.Initialize();
            Debug.Log("[ModBehaviour] 本地玩家开枪事件监听已启动");

            // 🔥 提前初始化武器特效系统（避免第一次开火时的反射查找开销）
            Core.Utils.WeaponEffectsPlayer.Initialize();
            Services.WeaponFireEffectsPlayer.Initialize();
            Debug.Log("[ModBehaviour] 武器特效系统已预初始化");

    

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

        void LateUpdate()
        {
            try
            {
                if (GameContext.IsInitialized)
                {
                    GameContext.Instance.LateUpdate();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModBehaviour] LateUpdate 方法出错: {ex.Message}");
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

                // 清理单位生命周期管理器
                _characterLifecycleManager?.Dispose();
                _characterLifecycleManager = null;

                // 清理本地玩家开枪事件桥接器
                _localPlayerShootBridge?.Dispose();
                _localPlayerShootBridge = null;

                // 清理伤害修改监听器

                // 注意：RPC 客户端会在 Disconnect 时自动清理事件订阅

                // 清理游戏上下文（会自动清理所有服务）
                GameContext.Cleanup();

                Debug.Log("[ModBehaviour] Mod 已卸载");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ModBehaviour] 卸载失败: {ex.Message}");
                Debug.LogException(ex);
            }
        }
    }
}
