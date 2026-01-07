using System;
using UnityEngine;
using DuckyNet.RPC;
using DuckyNet.RPC.Core;
using DuckyNet.Client.Core.Players;
using EventBusCore = DuckyNet.Client.Core.EventBus.EventBus;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 游戏上下文 - 全局服务容器
    /// 使用 Service Locator 模式管理所有核心服务
    /// </summary>
    public class GameContext
    {
        private static GameContext? _instance;
        
        /// <summary>
        /// 全局实例
        /// </summary>
        public static GameContext Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new InvalidOperationException("GameContext 未初始化！请先调用 Initialize()");
                }
                return _instance;
            }
        }

        /// <summary>
        /// 玩家服务
        /// </summary>
        public PlayerManager PlayerManager { get; private set; }

        /// <summary>
        /// RPC 客户端服务
        /// </summary>
        public RpcClient RpcClient { get; private set; }

        /// <summary>
        /// UI 管理器
        /// </summary>
        public UIManager UIManager { get; private set; }

        /// <summary>
        /// 输入管理器
        /// </summary>
        public InputManager InputManager { get; private set; }

        /// <summary>
        /// 头像管理器
        /// </summary>
        public AvatarManager AvatarManager { get; private set; }


        /// <summary>
        /// 场景客户端管理器
        /// </summary>
        public SceneClientManager SceneClientManager { get; private set; }

        /// <summary>
        /// 房间客户端管理器
        /// </summary>
        public RoomManager RoomManager { get; private set; }

        /// <summary>
        /// 角色自定义管理器
        /// </summary>
        public CharacterCustomizationManager CharacterCustomizationManager { get; private set; }

        /// <summary>
        /// 动画同步管理器
        /// </summary>
        public AnimatorSyncManager AnimatorSyncManager { get; private set; }

        /// <summary>
        /// 动画同步客户端服务
        /// </summary>
        public Services.AnimatorSyncClientServiceImpl? AnimatorSyncClientService { get; set; }

        /// <summary>
        /// 物品网络协调器
        /// </summary>
        public Services.ItemNetworkCoordinator? ItemNetworkCoordinator { get; set; }

        /// <summary>
        /// NPC 管理器
        /// </summary>
        public NpcManager NpcManager { get; private set; }

        /// <summary>
        /// 全局事件总线
        /// </summary>
        public EventBusCore EventBus { get; private set; }

        /// <summary>
        /// 是否已初始化
        /// </summary>
        public static bool IsInitialized => _instance != null;

        private GameContext()
        {
            PlayerManager = null!;
            RpcClient = null!;
            UIManager = null!;
            InputManager = null!;
            AvatarManager = null!;
            CharacterCustomizationManager = null!;
            SceneClientManager = null!;
            RoomManager = null!;
            AnimatorSyncManager = null!;
            NpcManager = null!;
            EventBus = EventBusCore.Instance;
        }

        /// <summary>
        /// 初始化游戏上下文
        /// </summary>
        public static void Initialize()
        {
            if (_instance != null)
            {
                UnityEngine.Debug.LogWarning("[GameContext] 已经初始化，跳过重复初始化");
                return;
            }

            _instance = new GameContext();
            UnityEngine.Debug.Log("[GameContext] 游戏上下文已创建");
        }

        // RegisterService 方法已移除，改为在每个注册方法中直接实现

        /// <summary>
        /// 注册本地玩家服务
        /// </summary>
        public void RegisterPlayerManager(PlayerManager playerManager)
        {
            PlayerManager = playerManager ?? throw new ArgumentNullException(nameof(playerManager));
            UnityEngine.Debug.Log("[GameContext] 本地玩家服务已注册");
        }

        /// <summary>
        /// 注册 RPC 客户端服务
        /// </summary>
        public void RegisterRpcClient(RpcClient rpcClient)
        {
            RpcClient = rpcClient ?? throw new ArgumentNullException(nameof(rpcClient));
            UnityEngine.Debug.Log("[GameContext] RPC 客户端服务已注册");
        }

        /// <summary>
        /// 注册 UI 管理器
        /// </summary>
        public void RegisterUIManager(UIManager uiManager)
        {
            UIManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            UnityEngine.Debug.Log("[GameContext] UI 管理器已注册");
        }

        /// <summary>
        /// 注册输入管理器
        /// </summary>
        public void RegisterInputManager(InputManager inputManager)
        {
            InputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
            UnityEngine.Debug.Log("[GameContext] 输入管理器已注册");
        }

        /// <summary>
        /// 注册头像管理器
        /// </summary>
        public void RegisterAvatarManager(AvatarManager avatarManager)
        {
            AvatarManager = avatarManager ?? throw new ArgumentNullException(nameof(avatarManager));
            UnityEngine.Debug.Log("[GameContext] 头像管理器已注册");
        }



        /// <summary>
        /// 注册角色自定义管理器
        /// </summary>
        public void RegisterCharacterCustomizationManager(CharacterCustomizationManager customizationManager)
        {
            CharacterCustomizationManager = customizationManager ?? throw new ArgumentNullException(nameof(customizationManager));
            UnityEngine.Debug.Log("[GameContext] 角色自定义管理器已注册");
        }

        /// <summary>
        /// 注册动画同步管理器
        /// </summary>
        public void RegisterAnimatorSyncManager(AnimatorSyncManager animatorSyncManager)
        {
            AnimatorSyncManager = animatorSyncManager ?? throw new ArgumentNullException(nameof(animatorSyncManager));
            UnityEngine.Debug.Log("[GameContext] 动画同步管理器已注册");
        }

        /// <summary>
        /// 注册场景客户端管理器
        /// </summary>
        public void RegisterSceneClientManager(SceneClientManager sceneClientManager)
        {
            SceneClientManager = sceneClientManager ?? throw new ArgumentNullException(nameof(sceneClientManager));
            UnityEngine.Debug.Log("[GameContext] 场景客户端管理器已注册");
        }

        /// <summary>
        /// 注册房间客户端管理器
        /// </summary>
        public void RegisterRoomManager(RoomManager roomManager)
        {
            RoomManager = roomManager ?? throw new ArgumentNullException(nameof(roomManager));
            UnityEngine.Debug.Log("[GameContext] 房间管理器已注册");
        }

        /// <summary>
        /// 注册物品网络协调器
        /// </summary>
        public void RegisterItemNetworkCoordinator(Services.ItemNetworkCoordinator itemNetworkCoordinator)
        {
            ItemNetworkCoordinator = itemNetworkCoordinator ?? throw new ArgumentNullException(nameof(itemNetworkCoordinator));
            UnityEngine.Debug.Log("[GameContext] 物品网络协调器已注册");
        }

        /// <summary>
        /// 注册 NPC 管理器
        /// </summary>
        public void RegisterNpcManager(NpcManager npcManager)
        {
            NpcManager = npcManager ?? throw new ArgumentNullException(nameof(npcManager));
            UnityEngine.Debug.Log("[GameContext] NPC 管理器已注册");
        }

        /// <summary>
        /// 清理游戏上下文
        /// </summary>
        public static void Cleanup()
        {
            if (_instance == null) return;

            try
            {
                _instance.NpcManager?.Dispose();
                _instance.CharacterCustomizationManager?.Dispose();
                _instance.InputManager?.Dispose();
                _instance.UIManager?.Dispose();
                _instance.AvatarManager?.Dispose();
                _instance.AnimatorSyncManager?.Dispose();
                _instance.ItemNetworkCoordinator?.Dispose();
                _instance.RpcClient?.Disconnect();
                _instance.PlayerManager?.Dispose();
                _instance.SceneClientManager?.Dispose();
                _instance.RoomManager?.Dispose();
                _instance.EventBus?.Dispose();
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[GameContext] 清理失败: {ex.Message}");
            }
            finally
            {
                _instance = null;
            }
        }

        /// <summary>
        /// 更新所有服务（每帧调用）
        /// </summary>
        public void Update()
        {
            RpcClient?.Update();
            InputManager?.Update();
            UIManager?.Update();
            PlayerManager?.Update();
            NpcManager?.Update(); // 同步 NPC 位置
            AnimatorSyncManager?.Update();
        }

        /// <summary>
        /// LateUpdate - 动画后处理（每帧调用）
        /// </summary>
        public void LateUpdate()
        {
            // 🎯 更新远程玩家动画（在 LateUpdate 中统一提交到 Animator）
            PlayerManager?.LateUpdate();
        }

        /// <summary>
        /// 渲染所有 GUI（每帧调用）
        /// </summary>
        public void OnGUI()
        {
            UIManager?.OnGUI();
        }
    }
}
