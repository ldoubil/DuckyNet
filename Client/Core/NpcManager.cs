using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using HarmonyLib;
using DuckyNet.Client.Core.EventBus;
using DuckyNet.Client.Core.EventBus.Events;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// NPC 管理器 - 统一管理本地和远程 NPC
    /// 本地 NPC：由游戏原生生成，带 AI
    /// 远程 NPC：由服务器同步创建，无 AI（影子模式）
    /// </summary>
    public class NpcManager : IDisposable
    {
        // 本地 NPC（带 AI）
        private readonly Dictionary<string, NpcInfo> _localNpcs = new Dictionary<string, NpcInfo>();
        
        // 远程 NPC（影子模式，无 AI）
        private readonly Dictionary<string, NpcInfo> _remoteNpcs = new Dictionary<string, NpcInfo>();
        
        private readonly EventSubscriberHelper _eventSub = new EventSubscriberHelper();

        // 反射缓存
        private static Type? _healthType;
        private static Type? _characterMainControlType;
        private static System.Reflection.PropertyInfo? _currentHealthProperty;
        private static System.Reflection.PropertyInfo? _maxHealthProperty;
        private static System.Reflection.FieldInfo? _healthField;

        // 位置同步
        private float _lastSyncTime;
        private const float SyncInterval = 0.1f; // 每 100ms 同步一次

        // 可见性管理器
        private readonly NpcVisibilityManager _visibilityManager;

        // 对象池
        private readonly ShadowNpcPool _npcPool;

        public NpcManager()
        {
            _visibilityManager = new NpcVisibilityManager
            {
                SyncRange = 100f, // 同步范围 100 米
                PositionThreshold = 0.1f, // 位置变化阈值 0.1 米
                RotationThreshold = 5f // 旋转变化阈值 5 度
            };

            _npcPool = new ShadowNpcPool
            {
                DefaultPoolSize = 10,
                MaxPoolSize = 50,
                AutoRecycleTime = 60f
            };

            // 预热常用 NPC 类型
            _npcPool.WarmUp("Character(Clone)", 5);

            InitializeReflection();
            
            _eventSub.EnsureInitializedAndSubscribe();
            _eventSub.Subscribe<CharacterSpawnedEvent>(OnNpcSpawned);
            _eventSub.Subscribe<CharacterDestroyedEvent>(OnNpcDestroyed);
            _eventSub.Subscribe<CharacterDeathEvent>(OnNpcDeath);
            
            // 订阅场景进入事件（中途加入时请求场景 NPC）
            _eventSub.Subscribe<SceneLoadedDetailEvent>(OnSceneLoaded);

            Debug.Log("[NpcManager] NPC 管理器已初始化");
        }

        /// <summary>
        /// 初始化反射
        /// </summary>
        private void InitializeReflection()
        {
            if (_healthType != null) return;

            _healthType = AccessTools.TypeByName("Health");
            _characterMainControlType = AccessTools.TypeByName("CharacterMainControl");

            if (_healthType != null)
            {
                _currentHealthProperty = AccessTools.Property(_healthType, "CurrentHealth");
                _maxHealthProperty = AccessTools.Property(_healthType, "MaxHealth");
            }

            if (_characterMainControlType != null)
            {
                _healthField = AccessTools.Field(_characterMainControlType, "health");
            }
        }

        /// <summary>
        /// 本地 NPC 创建事件（游戏原生生成）
        /// </summary>
        private void OnNpcSpawned(CharacterSpawnedEvent evt)
        {
            try
            {
                // 过滤掉本地玩家
                if (IsLocalPlayer(evt.CharacterMainControl)) return;

                // 获取当前场景信息
                var sceneData = GameContext.Instance.PlayerManager?.LocalPlayer?.Info?.CurrentScenelData;

                var npcInfo = new NpcInfo
                {
                    Id = evt.CharacterId,
                    CharacterMainControl = evt.CharacterMainControl,
                    GameObject = evt.GameObject,
                    Name = evt.GameObject?.name ?? "Unknown",
                    SpawnTime = Time.time,
                    IsAlive = true,
                    IsLocal = true, // 标记为本地 NPC
                    SceneName = sceneData?.SceneName ?? "",
                    SubSceneName = sceneData?.SubSceneName ?? ""
                };

                // 获取初始位置和旋转
                if (evt.GameObject != null)
                {
                    npcInfo.Position = evt.GameObject.transform.position;
                    npcInfo.Rotation = evt.GameObject.transform.rotation;
                }

                // 获取血量信息
                UpdateHealth(npcInfo);

                _localNpcs[evt.CharacterId] = npcInfo;
                
                Debug.Log($"[NpcManager] 本地 NPC 已注册: {npcInfo.Name} (ID: {npcInfo.Id})");
                
                // 发送到服务器（让其他玩家看到）
                SendNpcSpawnToServer(npcInfo);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NpcManager] 处理 NPC 创建失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 本地 NPC 销毁事件
        /// </summary>
        private void OnNpcDestroyed(CharacterDestroyedEvent evt)
        {
            if (_localNpcs.TryGetValue(evt.CharacterId, out var npc))
            {
                Debug.Log($"[NpcManager] 本地 NPC 已移除: {npc.Name} (ID: {evt.CharacterId})");
                
                // 清理可见性状态
                _visibilityManager.RemoveNpcState(evt.CharacterId);
                
                // 发送到服务器
                SendNpcDestroyToServer(evt.CharacterId, npc.SceneName, npc.SubSceneName);
                
                _localNpcs.Remove(evt.CharacterId);
            }
        }

        /// <summary>
        /// 本地 NPC 死亡事件
        /// </summary>
        private void OnNpcDeath(CharacterDeathEvent evt)
        {
            if (_localNpcs.TryGetValue(evt.CharacterId, out var npcInfo))
            {
                npcInfo.IsAlive = false;
                npcInfo.DeathTime = Time.time;
            }
        }

        /// <summary>
        /// 更新 NPC 血量信息
        /// </summary>
        private void UpdateHealth(NpcInfo npcInfo)
        {
            try
            {
                if (npcInfo.CharacterMainControl == null || _healthField == null) return;

                object? health = _healthField.GetValue(npcInfo.CharacterMainControl);
                if (health == null) return;

                npcInfo.CurrentHealth = (float?)_currentHealthProperty?.GetValue(health) ?? 0f;
                npcInfo.MaxHealth = (float?)_maxHealthProperty?.GetValue(health) ?? 0f;
            }
            catch
            {
                // 静默失败
            }
        }

        private float _sceneLoadTime;
        private bool _hasPendingNpcRequest;

        /// <summary>
        /// 场景加载完成事件（中途加入时请求场景 NPC）
        /// </summary>
        private void OnSceneLoaded(SceneLoadedDetailEvent evt)
        {
            Debug.Log($"[NpcManager] 场景加载完成，延迟 1 秒后请求场景 NPC（等待位置同步）");
            
            // 🔥 标记需要延迟请求，在 Update 中处理
            _sceneLoadTime = Time.time;
            _hasPendingNpcRequest = true;
        }

        /// <summary>
        /// 检查是否是本地玩家
        /// </summary>
        private bool IsLocalPlayer(object? characterMainControl)
        {
            if (characterMainControl == null || _characterMainControlType == null) return false;

            try
            {
                var isMainCharacterProperty = AccessTools.Property(_characterMainControlType, "IsMainCharacter");
                return (bool)(isMainCharacterProperty?.GetValue(characterMainControl) ?? false);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取所有 NPC 列表（本地 + 远程）
        /// </summary>
        public IEnumerable<NpcInfo> GetAllNpcs()
        {
            // 更新本地 NPC 的实时信息
            foreach (var npc in _localNpcs.Values)
            {
                if (npc.IsAlive && npc.GameObject != null)
                {
                    var transform = npc.GameObject.transform;
                    npc.Position = transform.position;
                    npc.Rotation = transform.rotation;
                    UpdateHealth(npc);
                }
            }

            // 合并本地和远程 NPC
            return _localNpcs.Values.Concat(_remoteNpcs.Values).ToList();
        }

        /// <summary>
        /// 获取活着的 NPC
        /// </summary>
        public IEnumerable<NpcInfo> GetAliveNpcs()
        {
            return GetAllNpcs().Where(n => n.IsAlive);
        }

        /// <summary>
        /// 获取死亡的 NPC
        /// </summary>
        public IEnumerable<NpcInfo> GetDeadNpcs()
        {
            return GetAllNpcs().Where(n => !n.IsAlive);
        }

        /// <summary>
        /// 根据 ID 获取 NPC
        /// </summary>
        public NpcInfo? GetNpc(string id)
        {
            if (_localNpcs.TryGetValue(id, out var npc))
                return npc;
            
            if (_remoteNpcs.TryGetValue(id, out npc))
                return npc;
            
            return null;
        }

        /// <summary>
        /// 清理所有 NPC
        /// </summary>
        public void Clear()
        {
            _localNpcs.Clear();
            _remoteNpcs.Clear();
        }

        /// <summary>
        /// 添加远程 NPC（从对象池获取）
        /// </summary>
        public void AddRemoteNpc(string npcId, NpcSpawnData spawnData)
        {
            try
            {
                // 🔥 检查是否是本地 NPC（避免重复）
                if (_localNpcs.ContainsKey(npcId))
                {
                    Debug.Log($"[NpcManager] ⏭️ 跳过远程 NPC：{npcId} 是本地 NPC");
                    return;
                }

                // 检查是否已存在
                if (_remoteNpcs.ContainsKey(npcId))
                {
                    Debug.Log($"[NpcManager] ⏭️ 远程 NPC 已存在: {npcId}");
                    return;
                }

                // 从对象池获取
                var (characterMainControl, gameObject) = _npcPool.Get(spawnData);
                
                if (characterMainControl == null || gameObject == null)
                {
                    Debug.LogError($"[NpcManager] 从对象池获取 NPC 失败: {spawnData.NpcType}");
                    return;
                }

                var npcInfo = new NpcInfo
                {
                    Id = npcId,
                    CharacterMainControl = characterMainControl,
                    GameObject = gameObject,
                    Name = spawnData.NpcType,
                    SpawnTime = Time.time,
                    IsAlive = true,
                    IsLocal = false, // 远程 NPC
                    SceneName = spawnData.SceneName,
                    SubSceneName = spawnData.SubSceneName
                };

                npcInfo.Position = gameObject.transform.position;
                npcInfo.Rotation = gameObject.transform.rotation;
                // 初始化目标位置（防止从 (0,0,0) 插值）
                npcInfo.TargetPosition = gameObject.transform.position;
                npcInfo.TargetRotation = gameObject.transform.rotation;

                _remoteNpcs[npcId] = npcInfo;
                Debug.Log($"[NpcManager] ✅ 远程 NPC 已添加: {spawnData.NpcType} (ID: {npcId})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NpcManager] 添加远程 NPC 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 添加远程 NPC（旧方法，兼容性）
        /// </summary>
        public void AddRemoteNpc(string npcId, object characterMainControl, GameObject? gameObject, 
            string npcType, string sceneName, string subSceneName)
        {
            var spawnData = new NpcSpawnData
            {
                NpcId = npcId,
                NpcType = npcType,
                SceneName = sceneName,
                SubSceneName = subSceneName,
                PositionX = gameObject?.transform.position.x ?? 0,
                PositionY = gameObject?.transform.position.y ?? 0,
                PositionZ = gameObject?.transform.position.z ?? 0,
                RotationY = gameObject?.transform.rotation.eulerAngles.y ?? 0
            };
            
            AddRemoteNpc(npcId, spawnData);
        }

        /// <summary>
        /// 更新远程 NPC 位置（设置目标位置，不直接设置）
        /// </summary>
        public void UpdateRemoteNpcTransform(string npcId, Vector3 position, float rotationY)
        {
            if (_remoteNpcs.TryGetValue(npcId, out var npc))
            {
                // 设置目标位置和旋转（用于平滑插值）
                npc.TargetPosition = position;
                npc.TargetRotation = Quaternion.Euler(0, rotationY, 0);

                // 如果是第一次接收位置，直接设置
                if (npc.GameObject != null && Vector3.Distance(npc.Position, Vector3.zero) < 0.01f)
                {
                    npc.Position = position;
                    npc.Rotation = Quaternion.Euler(0, rotationY, 0);
                    npc.GameObject.transform.position = position;
                    npc.GameObject.transform.rotation = Quaternion.Euler(0, rotationY, 0);
                }
            }
        }

        /// <summary>
        /// 移除远程 NPC（回收到对象池）
        /// </summary>
        public void RemoveRemoteNpc(string npcId)
        {
            if (_remoteNpcs.TryGetValue(npcId, out var npc))
            {
                // 回收到对象池（而不是直接销毁）
                _npcPool.Recycle(npcId);

                _remoteNpcs.Remove(npcId);
                Debug.Log($"[NpcManager] 远程 NPC 已移除并回收: {npc.Name} (ID: {npcId})");
            }
        }

        /// <summary>
        /// 请求当前场景的所有远程 NPC（中途加入时）
        /// </summary>
        public async void RequestSceneNpcs()
        {
            try
            {
                if (!GameContext.IsInitialized) return;

                var sceneData = GameContext.Instance.PlayerManager?.LocalPlayer?.Info?.CurrentScenelData;
                if (sceneData == null) return;

                Debug.Log($"[NpcManager] 📥 请求场景 NPC: {sceneData.SceneName}/{sceneData.SubSceneName}");

                var serverContext = new RPC.ClientServerContext(GameContext.Instance.RpcClient);
                var npcService = new Shared.Services.Generated.NpcSyncServiceClientProxy(serverContext);
                var npcs = await npcService.RequestSceneNpcs(sceneData.SceneName, sceneData.SubSceneName);
                
                Debug.Log($"[NpcManager] ✅ 收到 {npcs.Length} 个场景 NPC");

                // 批量创建影子 NPC（使用对象池）
                foreach (var npcData in npcs)
                {
                    AddRemoteNpc(npcData.NpcId, npcData);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NpcManager] 请求场景 NPC 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送 NPC 生成到服务器
        /// </summary>
        private async void SendNpcSpawnToServer(NpcInfo npcInfo)
        {
            try
            {
                if (!GameContext.IsInitialized || GameContext.Instance.RpcClient == null) return;

                var spawnData = new NpcSpawnData
                {
                    NpcId = npcInfo.Id,
                    SceneName = npcInfo.SceneName,
                    SubSceneName = npcInfo.SubSceneName,
                    NpcType = npcInfo.Name,
                    PositionX = npcInfo.Position.x,
                    PositionY = npcInfo.Position.y,
                    PositionZ = npcInfo.Position.z,
                    RotationY = npcInfo.Rotation.eulerAngles.y,
                    MaxHealth = npcInfo.MaxHealth
                };

                var serverContext = new RPC.ClientServerContext(GameContext.Instance.RpcClient);
                var npcService = new Shared.Services.Generated.NpcSyncServiceClientProxy(serverContext);
                await npcService.NotifyNpcSpawned(spawnData);
                
                Debug.Log($"[NpcManager] ✅ NPC 生成已发送到服务器");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NpcManager] 发送 NPC 生成失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送 NPC 销毁到服务器
        /// </summary>
        private async void SendNpcDestroyToServer(string npcId, string sceneName, string subSceneName)
        {
            try
            {
                if (!GameContext.IsInitialized || GameContext.Instance.RpcClient == null) return;

                var destroyData = new NpcDestroyData
                {
                    NpcId = npcId,
                    Reason = 0 // 正常销毁
                };

                var serverContext = new RPC.ClientServerContext(GameContext.Instance.RpcClient);
                var npcService = new Shared.Services.Generated.NpcSyncServiceClientProxy(serverContext);
                await npcService.NotifyNpcDestroyed(destroyData);
                
                Debug.Log($"[NpcManager] ✅ NPC 销毁已通知服务器");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NpcManager] 发送 NPC 销毁失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 每帧更新 - 定期同步本地 NPC 位置 + 平滑远程 NPC
        /// </summary>
        public void Update()
        {
            // 处理延迟的 NPC 请求（等待位置同步）
            if (_hasPendingNpcRequest && Time.time - _sceneLoadTime >= 1f)
            {
                _hasPendingNpcRequest = false;
                Debug.Log($"[NpcManager] 📥 延迟请求完成，开始请求场景 NPC");
                RequestSceneNpcs();
            }

            // 平滑更新远程 NPC 位置
            UpdateRemoteNpcSmoothing();

            // 定期同步本地 NPC 位置
            if (Time.time - _lastSyncTime >= SyncInterval)
            {
                SendNpcTransformBatch();
                _lastSyncTime = Time.time;
            }
        }

        /// <summary>
        /// 平滑更新远程 NPC 的位置和旋转（每帧调用）
        /// </summary>
        private void UpdateRemoteNpcSmoothing()
        {
            foreach (var npc in _remoteNpcs.Values)
            {
                if (npc.GameObject == null || !npc.IsAlive) continue;

                // 平滑插值到目标位置
                float distance = Vector3.Distance(npc.Position, npc.TargetPosition);
                if (distance > 0.01f) // 只有距离足够大才插值
                {
                    npc.Position = Vector3.Lerp(
                        npc.Position,
                        npc.TargetPosition,
                        Time.deltaTime * npc.SmoothSpeed
                    );
                    npc.GameObject.transform.position = npc.Position;
                }

                // 平滑插值旋转
                if (Quaternion.Angle(npc.Rotation, npc.TargetRotation) > 0.1f)
                {
                    npc.Rotation = Quaternion.Slerp(
                        npc.Rotation,
                        npc.TargetRotation,
                        Time.deltaTime * npc.SmoothSpeed
                    );
                    npc.GameObject.transform.rotation = npc.Rotation;
                }
            }
        }

        /// <summary>
        /// 批量发送本地 NPC 位置到服务器（带优化）
        /// </summary>
        private async void SendNpcTransformBatch()
        {
            try
            {
                if (!GameContext.IsInitialized || GameContext.Instance.RpcClient == null) return;
                if (_localNpcs.Count == 0) return;

                // 获取本地玩家位置
                var localPlayer = GameContext.Instance.PlayerManager?.LocalPlayer;
                if (localPlayer?.CharacterObject == null) return;

                var playerPosition = localPlayer.CharacterObject.transform.position;

                // 使用可见性管理器过滤需要同步的 NPC
                var npcsToSync = _visibilityManager.GetNpcsToSync(_localNpcs, playerPosition, null);

                if (npcsToSync.Count == 0) return;

                var transforms = new List<NpcTransformData>();

                foreach (var npcId in npcsToSync)
                {
                    if (_localNpcs.TryGetValue(npcId, out var npc) && npc.GameObject != null)
                    {
                        var pos = npc.GameObject.transform.position;
                        var rot = npc.GameObject.transform.rotation.eulerAngles.y;

                        transforms.Add(new NpcTransformData
                        {
                            NpcId = npc.Id,
                            PositionX = pos.x,
                            PositionY = pos.y,
                            PositionZ = pos.z,
                            RotationY = rot
                        });
                    }
                }

                if (transforms.Count > 0)
                {
                    var batchData = new NpcBatchTransformData
                    {
                        Count = transforms.Count,
                        NpcIds = transforms.Select(t => t.NpcId).ToArray(),
                        PositionsX = transforms.Select(t => t.PositionX).ToArray(),
                        PositionsY = transforms.Select(t => t.PositionY).ToArray(),
                        PositionsZ = transforms.Select(t => t.PositionZ).ToArray(),
                        RotationsY = transforms.Select(t => t.RotationY).ToArray()
                    };

                    var serverContext = new RPC.ClientServerContext(GameContext.Instance.RpcClient);
                    var npcService = new Shared.Services.Generated.NpcSyncServiceClientProxy(serverContext);
                    await npcService.NotifyNpcBatchTransform(batchData);

                    // Debug.Log($"[NpcManager] 同步 {transforms.Count}/{_localNpcs.Count} 个 NPC");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NpcManager] 发送位置更新失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取可见性管理器（用于调试）
        /// </summary>
        public NpcVisibilityManager VisibilityManager => _visibilityManager;

        /// <summary>
        /// 获取对象池（用于调试）
        /// </summary>
        public ShadowNpcPool NpcPool => _npcPool;

        public void Dispose()
        {
            _eventSub.Dispose();
            _visibilityManager.Dispose();
            _npcPool.Dispose();
            _localNpcs.Clear();
            _remoteNpcs.Clear();
            Debug.Log("[NpcManager] NPC 管理器已释放");
        }
    }

    /// <summary>
    /// NPC 信息
    /// </summary>
    public class NpcInfo
    {
        public string Id { get; set; } = "";
        public object? CharacterMainControl { get; set; }
        public GameObject? GameObject { get; set; }
        public string Name { get; set; } = "Unknown";
        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }
        public float CurrentHealth { get; set; }
        public float MaxHealth { get; set; }
        public bool IsAlive { get; set; }
        public float SpawnTime { get; set; }
        public float? DeathTime { get; set; }
        
        /// <summary>
        /// 是否是本地 NPC（带 AI）
        /// </summary>
        public bool IsLocal { get; set; }
        
        /// <summary>
        /// 场景名称（创建时记录，不会变化）
        /// </summary>
        public string SceneName { get; set; } = "";
        
        /// <summary>
        /// 子场景名称
        /// </summary>
        public string SubSceneName { get; set; } = "";

        // 平滑同步（仅远程 NPC）
        internal Vector3 TargetPosition { get; set; }
        internal Quaternion TargetRotation { get; set; }
        internal float SmoothSpeed { get; set; } = 10f; // 平滑速度

        /// <summary>
        /// 血量百分比
        /// </summary>
        public float HealthPercent => MaxHealth > 0 ? (CurrentHealth / MaxHealth) * 100f : 0f;

        /// <summary>
        /// 存活时间（秒）
        /// </summary>
        public float AliveTime
        {
            get
            {
                if (!IsAlive && DeathTime.HasValue)
                {
                    return DeathTime.Value - SpawnTime;
                }
                return Time.time - SpawnTime;
            }
        }
    }
}

