using System;
using UnityEngine;
using static UnityEngine.Debug;
using Steamworks;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.Helpers;
using DuckyNet.Client.Core.Utils;

namespace DuckyNet.Client.Core.Players
{
    /// <summary>
    /// 远程玩家 - 表示网络中的其他玩家
    /// 🔥 正确架构：双层生命周期
    /// 
    /// RemotePlayer 生命周期（房间层）：
    /// - PlayerJoinedRoomEvent → 创建 RemotePlayer（订阅位置同步事件）
    /// - PlayerLeftRoomEvent → 销毁 RemotePlayer
    /// 
    /// Character 生命周期（场景层）：
    /// - PlayerEnteredSceneEvent → 标记玩家进入场景
    /// - 收到位置同步数据 → 创建角色（如果在同一场景）
    /// - PlayerLeftSceneEvent → 销毁角色（保留 RemotePlayer）
    /// 
    /// 性能优化：
    /// - 缓存 Transform 引用，减少 GetComponent 调用
    /// </summary>
    public class RemotePlayer : BasePlayer
    {
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();
        private SmoothSyncManager? _smoothSyncManager;
        private Transform? _characterTransform; // 缓存 Transform 引用
        
        public RemotePlayer(PlayerInfo info) : base(info)
        {
            Log($"[RemotePlayer] 远程玩家创建（房间层）: {info.SteamName} ({info.SteamId})");
            _eventSubscriber.EnsureInitializedAndSubscribe();
            
            // 🔥 订阅位置同步事件
            _eventSubscriber.Subscribe<PlayerUnitySyncEvent>(OnPlayerUnitySyncReceived);
            
            // 🔥 订阅场景事件（远程玩家离开场景时销毁角色）
            _eventSubscriber.Subscribe<PlayerLeftSceneEvent>(OnPlayerLeftScene);
            
            // 🔥 订阅本地场景切换事件（清理已销毁的角色引用）
            _eventSubscriber.Subscribe<SceneLoadedDetailEvent>(OnLocalSceneLoaded);
        }

        /// <summary>
        /// 玩家离开场景 - 销毁角色
        /// </summary>
        private void OnPlayerLeftScene(PlayerLeftSceneEvent @event)
        {
            // 只处理自己的场景事件
            if (@event.PlayerInfo.SteamId != Info.SteamId) return;

            Log($"[RemotePlayer] 玩家 {Info.SteamName} 离开场景，销毁角色");
            DestroyCharacter(); // 销毁角色，但保留 RemotePlayer
        }

        /// <summary>
        /// 本地玩家场景加载完成 - 清理已销毁的角色引用
        /// 🔥 简化逻辑：主场景切换时 Unity 会销毁所有对象，我们只需要清空引用
        /// 服务器会根据场景匹配来发送位置同步，收到同步后会自动重建角色
        /// </summary>
        private void OnLocalSceneLoaded(SceneLoadedDetailEvent @event)
        {
            // 🔥 主场景切换时，Unity 会自动销毁场景中的所有对象
            // 清空角色引用，避免访问已销毁的对象
            if (CharacterObject != null && CharacterObject == null) // Unity 特殊的 null 检查
            {
                Log($"[RemotePlayer] 检测到角色对象已被场景切换销毁，清空引用: {Info.SteamName}");
                CharacterObject = null;
                _characterTransform = null;
            }
            
            Log($"[RemotePlayer] 本地场景加载完成: {Info.SteamName}, 等待位置同步重建角色");
        }

        /// <summary>
        /// 收到位置同步数据 - 创建或更新角色
        /// 🔥 简化逻辑：服务器已经过滤了场景匹配，客户端收到就是同场景的数据
        /// </summary>
        private void OnPlayerUnitySyncReceived(PlayerUnitySyncEvent @event)
        {
            // 快速过滤：检查同步数据是否是当前玩家的
            if (@event.SteamID != Info.SteamId) return;

            // 如果平滑管理器不存在，创建它
            if (_smoothSyncManager == null)
            {
                var (posX, posY, posZ) = @event.SyncData.GetPosition();
                var (rotX, rotY, rotZ, rotW) = @event.SyncData.GetRotation();
                
                _smoothSyncManager = new SmoothSyncManager(
                    new Vector3(posX, posY, posZ),
                    new Quaternion(rotX, rotY, rotZ, rotW)
                );
                
                Log($"[RemotePlayer] 初始化平滑同步管理器: {Info.SteamName}");
            }
            
            // 接收新的同步数据
            _smoothSyncManager.ReceiveSyncData(@event.SyncData);
            
            // 🔥 核心简化：收到位置同步就创建角色
            // 服务器保证只发送同场景玩家的数据，客户端完全信任服务器
            if (CharacterObject == null)
            {
                var spawnPosition = _smoothSyncManager.GetPosition();
                Log($"[RemotePlayer] 🔥 收到位置同步，创建角色: {Info.SteamName} 位置: {spawnPosition}");
                CreateCharacter(spawnPosition, Info.SteamName);
            }
        }
        
        /// <summary>
        /// 更新远程玩家位置（每帧调用）
        /// 性能优化：缓存 Transform 引用，避免每帧 GetComponent
        /// </summary>
        public void UpdatePosition()
        {
            if (_smoothSyncManager == null || CharacterObject == null) return;
            
            // 缓存 Transform 引用
            if (_characterTransform == null)
            {
                _characterTransform = CharacterObject.transform;
            }
            
            if (_characterTransform == null) return;
            
            // 更新平滑值
            _smoothSyncManager.Update();
            
            // 应用到角色对象（使用缓存的 Transform）
            _smoothSyncManager.ApplyToTransform(_characterTransform);
        }

        /// <summary>
        /// 获取生成位置 - 可以从玩家信息中获取，或使用默认位置
        /// </summary>
        private Vector3 GetSpawnPosition()
        {
            // TODO: 从服务器同步的位置信息获取
            // 暂时使用默认位置
            return Vector3.zero;
        }

        /// <summary>
        /// 创建角色对象
        /// </summary>
        /// <param name="position">生成位置</param>
        /// <param name="displayName">显示名称（可选，默认使用 Info.SteamName）</param>
        /// <returns>创建成功返回true</returns>
        public bool CreateCharacter(Vector3 position, string? displayName = null)
        {
            // 🔥 如果未提供显示名称,使用 Info.SteamName
            displayName ??= Info.SteamName;
            
            // 如果已经有角色对象,先销毁
            if (CharacterObject != null)
            {
                DestroyCharacter();
            }

            try
            {
                // 创建角色数据项
                var characterItem = CharacterCreationUtils.CreateCharacterItem();
                if (characterItem == null)
                {
                    LogWarning($"[RemotePlayer] ⚠️ 创建角色数据项失败: {displayName}");
                    return false;
                }

                // 获取角色模型预制体
                var modelPrefab = CharacterCreationUtils.GetCharacterModelPrefab();
                if (modelPrefab == null)
                {
                    LogWarning($"[RemotePlayer] ⚠️ 获取角色模型预制体失败（可能是场景切换中 LevelManager 未就绪）: {displayName}");
                    return false;
                }

                // 实例化角色
                var newCharacter = CharacterCreationUtils.CreateCharacterInstance(
                    characterItem, modelPrefab, position, Quaternion.identity
                );
                if (newCharacter == null)
                {
                    LogWarning($"[RemotePlayer] ⚠️ 实例化角色失败: {displayName}");
                    return false;
                }

                // 配置角色基本属性
                CharacterCreationUtils.ConfigureCharacter(newCharacter, $"Character_{Info.SteamName}", position, team: 0);
                CharacterCreationUtils.ConfigureCharacterPreset(newCharacter, displayName, showName: true);
                
                // 禁用移动脚本 - 防止角色掉落和自动移动
                CharacterCreationUtils.DisableMovement(newCharacter);

                // 获取自定义图标并请求血条
                var customIcon = GetCustomIcon();
                CharacterCreationUtils.RequestHealthBar(newCharacter, displayName, customIcon);

                // 保存 GameObject 引用
                Component? characterComponent = newCharacter as Component;
                if (characterComponent != null)
                {
                    CharacterObject = characterComponent.gameObject;
                    _characterTransform = CharacterObject.transform; // 立即缓存 Transform
                
                    
                    // 初始化平滑同步管理器（如果还没有）
                    if (_smoothSyncManager == null)
                    {
                        _smoothSyncManager = new SmoothSyncManager(
                            _characterTransform.position,
                            _characterTransform.rotation
                        );
                    }
                    
                    // 打印角色位置信息
                    Vector3 characterPosition = _characterTransform.position;
                    Log($"[RemotePlayer] ✅ 角色创建成功: {displayName}, 位置: {characterPosition}");
                    
                    // 绘制调试射线 - 从相机/本地玩家指向远程玩家
                    DrawDebugRayToCharacter(characterPosition);
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogError($"[RemotePlayer] ❌ 创建角色时发生异常: {displayName}, 错误: {ex.Message}\n{ex.StackTrace}");
                return false;
            }

            return false;
        }

        /// <summary>
        /// 绘制调试射线 - 从屏幕中间（本地玩家/相机）指向远程玩家
        /// </summary>
        private void DrawDebugRayToCharacter(Vector3 targetPosition)
        {
            try
            {
                Vector3 startPosition;
                
                // 尝试获取主摄像机位置
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    startPosition = mainCamera.transform.position;
                    Log($"[RemotePlayer] 使用主摄像机位置作为起点: {startPosition}");
                }
                else
                {
                    // 如果没有主摄像机，尝试获取本地玩家位置
                    if (GameContext.IsInitialized && 
                        GameContext.Instance.PlayerManager?.LocalPlayer?.CharacterObject != null)
                    {
                        startPosition = GameContext.Instance.PlayerManager.LocalPlayer.CharacterObject.transform.position;
                        Log($"[RemotePlayer] 使用本地玩家位置作为起点: {startPosition}");
                    }
                    else
                    {
                        // 都没有，使用原点
                        startPosition = Vector3.zero;
                        LogWarning($"[RemotePlayer] 未找到相机和本地玩家，使用原点作为起点");
                    }
                }
                
                // 计算方向和距离
                Vector3 direction = targetPosition - startPosition;
                float distance = direction.magnitude;
                
                // 绘制调试射线（红色，持续10秒）
                Debug.DrawRay(startPosition, direction, Color.red, 10f);
                
                Log($"[RemotePlayer] 调试射线: 从 {startPosition} 指向 {targetPosition}, 距离: {distance:F2}");
            }
            catch (Exception ex)
            {
                LogError($"[RemotePlayer] 绘制调试射线失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取自定义图标 - 使用 Steam 头像
        /// </summary>
        private UnityEngine.Sprite? GetCustomIcon()
        {
            // 如果有 Steam 头像,将其转换为 Sprite
            if (AvatarTexture != null)
            {
                return UnityEngine.Sprite.Create(
                    AvatarTexture,
                    new UnityEngine.Rect(0, 0, AvatarTexture.width, AvatarTexture.height),
                    new UnityEngine.Vector2(0.5f, 0.5f)
                );
            }
            return null;
        }

        /// <summary>
        /// 设置 Steam 头像纹理
        /// </summary>
        public override void SetAvatarTexture(Texture2D texture)
        {
            AvatarTexture = texture;
            Log($"[RemotePlayer] Steam 头像已设置: {Info.SteamId}");

            // 如果角色已创建,可以更新血条图标
            // TODO: 实现运行时更新血条图标的逻辑
        }

        /// <summary>
        /// 释放资源（离开房间时调用）
        /// </summary>
        public override void Dispose()
        {
            Log($"[RemotePlayer] 远程玩家销毁（房间层）: {Info.SteamId}");
            _characterTransform = null; // 清除 Transform 缓存
            _smoothSyncManager = null;  // 清除同步管理器
            _eventSubscriber.Dispose();  // 取消事件订阅
            base.Dispose(); // 会自动销毁角色对象
        }
    }
}