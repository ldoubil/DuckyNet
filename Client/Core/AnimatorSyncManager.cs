using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DuckyNet.Shared.Data;
using DuckyNet.Client.Core.Helpers;

namespace DuckyNet.Client.Core
{
    /// <summary>
    /// 动画同步管理器
    /// 负责采集本地玩家动画状态并上传到服务器
    /// </summary>
    public class AnimatorSyncManager : IDisposable
    {
        private readonly EventSubscriberHelper _eventSubscriber = new EventSubscriberHelper();
        private Animator? _localPlayerAnimator;
        
        // 同步配置
        private float _syncCheckInterval = 0.05f; // 每 50ms 检查一次（20Hz）
        private float _lastCheckTime = 0f;
        private float _forceSyncInterval = 1.0f; // 强制同步间隔（1秒）
        private float _lastForceSyncTime = 0f;
        
        // 缓存上次发送的数据
        private AnimatorSyncData? _lastSentData = null;
        
        // 变化检测阈值
        private const float FLOAT_CHANGE_THRESHOLD = 0.02f;  // Float 参数变化阈值（2%）
        private const float STATE_TIME_THRESHOLD = 0.15f;     // 动画状态时间阈值（15%）
        
        // 参数映射配置（使用游戏实际的参数名）
        private readonly string[] _floatParamNames = new string[]
        {
            "MoveSpeed",       // 0: 移动速度
            "MoveDirX",        // 1: 移动方向 X
            "MoveDirY",        // 2: 移动方向 Y
            "",                // 3: 预留给 HandState (Integer)
            "",                // 4: 预留
            "",                // 5: 预留
            "",                // 6: 预留
            ""                 // 7: 预留
        };
        
        private readonly string[] _boolParamNames = new string[]
        {
            "Dashing",         // 0: 翻滚/冲刺
            "RightHandOut",    // 1: 右手是否伸出
            "Attack",          // 2: 攻击状态 (MagicBlend)
            "GunReady",        // 3: 枪械准备 (MagicBlend)
            "",                // 4-31: 预留
        };
        
        // 参数哈希缓存
        private Dictionary<int, int> _floatParamHashes = new Dictionary<int, int>();
        private Dictionary<int, int> _boolParamHashes = new Dictionary<int, int>();
        
        public AnimatorSyncManager()
        {
            _eventSubscriber.EnsureInitializedAndSubscribe();
            
            // 预计算参数哈希
            for (int i = 0; i < _floatParamNames.Length; i++)
            {
                if (string.IsNullOrEmpty(_floatParamNames[i])) continue;
                _floatParamHashes[i] = Animator.StringToHash(_floatParamNames[i]);
            }
            for (int i = 0; i < _boolParamNames.Length; i++)
            {
                if (string.IsNullOrEmpty(_boolParamNames[i])) continue;
                _boolParamHashes[i] = Animator.StringToHash(_boolParamNames[i]);
            }
        }
        
        /// <summary>
        /// 初始化（查找本地玩家 Animator）
        /// </summary>
        public void Initialize()
        {
            try
            {
                if (!GameContext.IsInitialized) return;
                
                var localPlayer = GameContext.Instance.PlayerManager?.LocalPlayer;
                if (localPlayer?.CharacterObject != null)
                {
                    _localPlayerAnimator = localPlayer.CharacterObject.GetComponentInChildren<Animator>();
                    if (_localPlayerAnimator != null)
                    {
                        Debug.Log("[AnimatorSyncManager] ✅ 找到本地玩家 Animator");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AnimatorSyncManager] 初始化失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 更新（定期检查并同步）
        /// </summary>
        public void Update()
        {
            if (_localPlayerAnimator == null)
            {
                // 尝试重新查找（每秒一次）
                if (Time.time - _lastCheckTime > 1f)
                {
                    Initialize();
                    _lastCheckTime = Time.time;
                }
                return;
            }
            
            // 定期检查动画变化
            if (Time.time - _lastCheckTime >= _syncCheckInterval)
            {
                CheckAndSyncAnimatorState();
                _lastCheckTime = Time.time;
            }
        }
        
        /// <summary>
        /// 检查动画状态并决定是否同步
        /// 策略：
        /// 1. 显著变化时立即发送（Float/Bool 参数改变）
        /// 2. 动画状态切换时立即发送
        /// 3. 无变化时，每 1 秒强制发送一次（保持连接活跃）
        /// </summary>
        private void CheckAndSyncAnimatorState()
        {
            if (_localPlayerAnimator == null || !GameContext.IsInitialized) return;
            
            try
            {
                var currentData = CaptureAnimatorState();
                
                // 策略1: 首次发送
                if (_lastSentData == null)
                {
                    SendAnimatorState(currentData);
                    return;
                }
                
                // 策略2: 检测显著变化
                if (HasSignificantChange(currentData, _lastSentData))
                {
                    SendAnimatorState(currentData);
                    return;
                }
                
                // 策略3: 强制同步（防止长时间无更新）
                if (Time.time - _lastForceSyncTime >= _forceSyncInterval)
                {
                    SendAnimatorState(currentData);
                    Debug.Log("[AnimatorSyncManager] 强制同步（保持活跃）");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AnimatorSyncManager] 检查动画状态失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 采集当前动画状态
        /// </summary>
        private AnimatorSyncData CaptureAnimatorState()
        {
            var syncData = new AnimatorSyncData();
            
            // 获取当前状态哈希（主层）
            var currentState = _localPlayerAnimator!.GetCurrentAnimatorStateInfo(0);
            syncData.StateHash = currentState.fullPathHash;
            syncData.SetNormalizedTime(currentState.normalizedTime);
            
            // 采集 Float 参数
            for (int i = 0; i < Math.Min(_floatParamNames.Length, 8); i++)
            {
                if (_floatParamHashes.TryGetValue(i, out int hash))
                {
                    try
                    {
                        float value = _localPlayerAnimator.GetFloat(hash);
                        syncData.SetFloatParam(i, value);
                    }
                    catch
                    {
                        // 参数不存在，跳过
                    }
                }
            }
            
            // 采集 Integer 参数 - HandState
            try
            {
                int handStateHash = Animator.StringToHash("HandState");
                int handStateValue = _localPlayerAnimator.GetInteger(handStateHash);
                syncData.SetFloatParam(3, handStateValue);
            }
            catch
            {
                // 参数不存在，跳过
            }
            
            // 采集 Bool 参数
            for (int i = 0; i < Math.Min(_boolParamNames.Length, 32); i++)
            {
                if (_boolParamHashes.TryGetValue(i, out int hash))
                {
                    try
                    {
                        bool value = _localPlayerAnimator.GetBool(hash);
                        syncData.SetBoolParam(i, value);
                    }
                    catch
                    {
                        // 参数不存在，跳过
                    }
                }
            }
            
            return syncData;
        }
        
        /// <summary>
        /// 发送动画状态到服务器
        /// </summary>
        private void SendAnimatorState(AnimatorSyncData syncData)
        {
            if (GameContext.Instance.RpcClient == null) return;
            
            try
            {
                GameContext.Instance.RpcClient.InvokeServer<Shared.Services.IAnimatorSyncService>(
                    nameof(Shared.Services.IAnimatorSyncService.UpdateAnimatorState),
                    syncData
                );
                
                // 更新缓存
                _lastSentData = syncData;
                _lastForceSyncTime = Time.time;
                
                // 调试日志
                Debug.Log($"[AnimatorSyncManager] 发送动画 - " +
                          $"State:{syncData.StateHash}, " +
                          $"Speed:{syncData.GetFloatParam(0):F2}, " +
                          $"Dir:({syncData.GetFloatParam(1):F2},{syncData.GetFloatParam(2):F2})");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AnimatorSyncManager] 发送动画状态失败: {ex.Message}");
            }
        }
        
        /// <summary>
        /// 检测是否有显著变化
        /// 算法：基于阈值的增量检测
        /// </summary>
        private bool HasSignificantChange(AnimatorSyncData current, AnimatorSyncData last)
        {
            // 1. 动画状态切换（最高优先级）
            if (current.StateHash != last.StateHash)
            {
                Debug.Log("[AnimatorSyncManager] 检测到状态切换");
                return true;
            }
            
            // 2. Bool 参数变化（高优先级）
            if (current.BoolParams != last.BoolParams)
            {
                Debug.Log("[AnimatorSyncManager] 检测到 Bool 参数变化");
                return true;
            }
            
            // 3. Float 参数显著变化（中优先级）
            for (int i = 0; i < 8; i++)
            {
                float currentVal = current.GetFloatParam(i);
                float lastVal = last.GetFloatParam(i);
                float delta = Mathf.Abs(currentVal - lastVal);
                
                // 对于移动参数 (0-2)，使用更敏感的阈值
                float threshold = (i <= 2) ? FLOAT_CHANGE_THRESHOLD : FLOAT_CHANGE_THRESHOLD * 2;
                
                if (delta > threshold)
                {
                    Debug.Log($"[AnimatorSyncManager] Float[{i}] 变化: {lastVal:F3} → {currentVal:F3} (Δ={delta:F3})");
                    return true;
                }
            }
            
            // 4. 动画时间跳跃（低优先级）
            float currentTime = current.GetNormalizedTime();
            float lastTime = last.GetNormalizedTime();
            float timeDelta = Mathf.Abs(currentTime - lastTime);
            
            // 如果时间倒退（循环）或跳跃过大
            if (timeDelta > STATE_TIME_THRESHOLD && timeDelta < 0.9f)
            {
                Debug.Log($"[AnimatorSyncManager] 动画时间跳跃: {lastTime:F2} → {currentTime:F2}");
                return true;
            }
            
            return false; // 无显著变化
        }
        
        public void Dispose()
        {
            _eventSubscriber.Dispose();
            _localPlayerAnimator = null;
        }
    }
}
