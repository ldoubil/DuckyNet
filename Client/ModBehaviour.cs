using System;
using UnityEngine;
using DuckyNet.Client.Core;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Client.Core.EventBus;
using DuckyNet.RPC;
using DuckyNet.RPC.Core;
using DuckyNet.Shared.Services.Generated;
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
            GameContext.Initialize();
            var context = GameContext.Instance;

            RegisterCoreServices(context);
            RegisterClientServices(context);
            InitializeManagers(context);
            RegisterInputKeys();
            InitializeBridges();
            InitializeNetworkLifecycle(context);

            Debug.Log("[ModBehaviour] 游戏上下文初始化完成");
        }

        /// <summary>
        /// 注册核心服务
        /// </summary>
        private void RegisterCoreServices(GameContext context)
        {
            context.RegisterPlayerManager(new Core.Players.PlayerManager());
            context.RegisterRpcClient(new RpcClient());
            context.RegisterInputManager(new Core.InputManager());
            context.RegisterAvatarManager(new Core.AvatarManager());
            context.RegisterCharacterCustomizationManager(new Core.CharacterCustomizationManager());
            context.RegisterSceneClientManager(new Core.SceneClientManager());
            context.RegisterRoomManager(new Core.RoomManager());
            context.RegisterAnimatorSyncManager(new Core.AnimatorSyncManager());
            context.RegisterUIManager(new Core.UIManager(context.RpcClient));
            context.RegisterNpcManager(new Core.NpcManager());
            
            Debug.Log("[ModBehaviour] 核心服务已注册");
        }

        /// <summary>
        /// 注册客户端 RPC 服务（使用极简流畅 API）
        /// </summary>
        private void RegisterClientServices(GameContext context)
        {
            var rpcClient = context.RpcClient;
            var playerService = new Services.PlayerClientServiceImpl();
            var roomService = new Services.RoomClientServiceImpl();
            var sceneService = new Services.SceneClientServiceImpl();
            var characterService = new Services.CharacterClientServiceImpl();
            var characterAppearanceService = new Services.CharacterAppearanceClientServiceImpl();
            var animatorSyncClientService = new Services.AnimatorSyncClientServiceImpl();
            var itemSyncService = new Services.ItemSyncClientServiceImpl();
            var equipmentService = new Services.EquipmentClientServiceImpl();
            var weaponSyncService = new Services.WeaponSyncClientServiceImpl();
            var healthSyncService = new Services.HealthSyncClientServiceImpl();
            var npcSyncService = new Services.NpcSyncClientServiceImpl();

            // 使用极简流畅 API 注册服务
            rpcClient.Reg<Shared.Services.IPlayerClientService>()
                .OnChatMessage(playerService.OnChatMessage)
                .OnPlayerJoined(playerService.OnPlayerJoined)
                .OnPlayerLeft(playerService.OnPlayerLeft)
                .OnServerMessage(playerService.OnServerMessage)
                .OnPlayerUnitySyncReceived(playerService.OnPlayerUnitySyncReceived);

            rpcClient.Reg<Shared.Services.IRoomClientService>()
                .OnPlayerJoinedRoom(roomService.OnPlayerJoinedRoom)
                .OnPlayerLeftRoom(roomService.OnPlayerLeftRoom)
                .OnKickedFromRoom(roomService.OnKickedFromRoom);

            rpcClient.Reg<Shared.Services.ISceneClientService>()
                .OnPlayerEnteredScene(sceneService.OnPlayerEnteredScene)
                .OnPlayerLeftScene(sceneService.OnPlayerLeftScene);

            rpcClient.Reg<Shared.Services.ICharacterClientService>()
                .OnPlayerAppearanceUpdated(characterService.OnPlayerAppearanceUpdated);

            rpcClient.Reg<Shared.Services.ICharacterAppearanceClientService>()
                .OnAppearanceReceived(characterAppearanceService.OnAppearanceReceived);

            rpcClient.Reg<Shared.Services.IAnimatorSyncClientService>()
                .OnAnimatorStateUpdated(animatorSyncClientService.OnAnimatorStateUpdated);
            context.AnimatorSyncClientService = animatorSyncClientService;

            rpcClient.Reg<Shared.Services.IItemSyncClientService>()
                .OnRemoteItemDropped(itemSyncService.OnRemoteItemDropped)
                .OnRemoteItemPickedUp(itemSyncService.OnRemoteItemPickedUp);

            rpcClient.Reg<Shared.Services.IEquipmentClientService>()
                .OnEquipmentSlotUpdated(equipmentService.OnEquipmentSlotUpdated)
                .OnAllPlayersEquipmentReceived(equipmentService.OnAllPlayersEquipmentReceived);

            rpcClient.Reg<Shared.Services.IWeaponSyncClientService>()
                .OnWeaponSlotUpdated(weaponSyncService.OnWeaponSlotUpdated)
                .OnAllPlayersWeaponReceived(weaponSyncService.OnAllPlayersWeaponReceived)
                .OnWeaponSwitched(weaponSyncService.OnWeaponSwitched)
                .OnWeaponFired(weaponSyncService.OnWeaponFired);

            rpcClient.Reg<Shared.Services.IHealthSyncClientService>()
                .OnHealthSyncReceived(healthSyncService.OnHealthSyncReceived);

            rpcClient.Reg<Shared.Services.INpcSyncClientService>()
                .OnNpcSpawned(npcSyncService.OnNpcSpawned)
                .OnNpcBatchTransform(npcSyncService.OnNpcBatchTransform)
                .OnNpcDestroyed(npcSyncService.OnNpcDestroyed);

            // 创建并注册物品网络协调器
            var clientContext = new RPC.ClientServerContext(rpcClient);
            var itemSyncServiceProxy = new Shared.Services.Generated.ItemSyncServiceClientProxy(clientContext);
            var itemNetworkCoordinator = new Services.ItemNetworkCoordinator(itemSyncServiceProxy);
            context.RegisterItemNetworkCoordinator(itemNetworkCoordinator);

            Debug.Log("[ModBehaviour] 客户端服务已注册（使用极简流畅 API）");
        }

        /// <summary>
        /// 初始化各个管理器
        /// </summary>
        private void InitializeManagers(GameContext context)
        {
            context.AnimatorSyncManager.Initialize();
            context.UIManager.Initialize();

            Debug.Log("[ModBehaviour] 管理器已初始化");
        }

        /// <summary>
        /// 初始化各种桥接器
        /// </summary>
        private void InitializeBridges()
        {
            // 场景事件桥接器
            var sceneBridge = new Patches.SceneEventBridge();
            sceneBridge.Initialize();
            Core.Helpers.SceneInfoProvider.Initialize(sceneBridge);


            // 本地玩家开枪事件桥接器
            _localPlayerShootBridge = new Patches.LocalPlayerShootBridge();
            _localPlayerShootBridge.Initialize();

            // 武器特效系统预初始化
            Core.Utils.WeaponEffectsPlayer.Initialize();
            Services.WeaponFireEffectsPlayer.Initialize();
            
            // 影子 NPC 工厂预初始化
            Core.ShadowNpcFactory.Initialize();

            Debug.Log("[ModBehaviour] 桥接器已初始化");
        }

        /// <summary>
        /// 初始化网络生命周期
        /// </summary>
        private void InitializeNetworkLifecycle(GameContext context)
        {
            var lifecycleManager = new Core.NetworkLifecycleManager(context);

            context.RpcClient.Connected += () => lifecycleManager.HandleConnected();
            context.RpcClient.Disconnected += lifecycleManager.HandleDisconnected;

            // 启动角色外观自动上传
            CharacterAppearanceHelper.StartAutoUpload();

            Debug.Log("[ModBehaviour] 网络生命周期已初始化");
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
