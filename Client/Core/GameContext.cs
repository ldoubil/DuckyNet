using System;
using UnityEngine;
using DuckyNet.Client.RPC;

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
        /// 本地玩家服务
        /// </summary>
        public LocalPlayer LocalPlayer { get; private set; }

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
        /// 是否已初始化
        /// </summary>
        public static bool IsInitialized => _instance != null;

        private GameContext()
        {
            LocalPlayer = null!;
            RpcClient = null!;
            UIManager = null!;
            InputManager = null!;
            AvatarManager = null!;
        }

        /// <summary>
        /// 初始化游戏上下文
        /// </summary>
        public static void Initialize()
        {
            if (_instance != null)
            {
                Debug.LogWarning("[GameContext] 已经初始化，跳过重复初始化");
                return;
            }

            _instance = new GameContext();
            Debug.Log("[GameContext] 游戏上下文已创建");
        }

        /// <summary>
        /// 注册本地玩家服务
        /// </summary>
        public void RegisterLocalPlayer(LocalPlayer localPlayer)
        {
            LocalPlayer = localPlayer ?? throw new ArgumentNullException(nameof(localPlayer));
            Debug.Log("[GameContext] 本地玩家服务已注册");
        }

        /// <summary>
        /// 注册 RPC 客户端服务
        /// </summary>
        public void RegisterRpcClient(RpcClient rpcClient)
        {
            RpcClient = rpcClient ?? throw new ArgumentNullException(nameof(rpcClient));
            Debug.Log("[GameContext] RPC 客户端服务已注册");
        }

        /// <summary>
        /// 注册 UI 管理器
        /// </summary>
        public void RegisterUIManager(UIManager uiManager)
        {
            UIManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            Debug.Log("[GameContext] UI 管理器已注册");
        }

        /// <summary>
        /// 注册输入管理器
        /// </summary>
        public void RegisterInputManager(InputManager inputManager)
        {
            InputManager = inputManager ?? throw new ArgumentNullException(nameof(inputManager));
            Debug.Log("[GameContext] 输入管理器已注册");
        }

        /// <summary>
        /// 注册头像管理器
        /// </summary>
        public void RegisterAvatarManager(AvatarManager avatarManager)
        {
            AvatarManager = avatarManager ?? throw new ArgumentNullException(nameof(avatarManager));
            Debug.Log("[GameContext] 头像管理器已注册");
        }

        /// <summary>
        /// 清理游戏上下文
        /// </summary>
        public static void Cleanup()
        {
            if (_instance == null) return;

            try
            {
                _instance.InputManager?.Dispose();
                _instance.UIManager?.Dispose();
                _instance.AvatarManager?.Dispose();
                _instance.RpcClient?.Disconnect();
                _instance.LocalPlayer?.Dispose();

                Debug.Log("[GameContext] 游戏上下文已清理");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GameContext] 清理失败: {ex.Message}");
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

