using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Client.Core.Utils;
using DuckyNet.Shared.Data;

namespace DuckyNet.Client.Core.Players
{
    /// <summary>
    /// 远程玩家动画同步管理器
    /// 整合批量写入、延迟播放、趋势外推三大系统
    /// 为每个远程玩家提供平滑的动画同步
    /// </summary>
    public class RemoteAnimatorSyncManager : IDisposable
    {
        // 远程玩家状态映射表
        private readonly Dictionary<string, RemotePlayerAnimationState> _playerStates = new Dictionary<string, RemotePlayerAnimationState>();
        
        // 配置参数
        public int PlaybackDelayMs { get; set; } = 120; // 播放延迟（毫秒）
        public bool EnableExtrapolation { get; set; } = true; // 启用趋势外推
        public float SmoothTime { get; set; } = 0.12f; // 平滑时间
        
        /// <summary>
        /// 远程玩家动画状态
        /// </summary>
        private class RemotePlayerAnimationState : IDisposable
        {
            public string PlayerId { get; }
            public GameObject? GameObject { get; private set; } // 可变,支持场景切换时更新
            public Animator? Animator { get; private set; }
            
            // 核心系统组件
            public AnimationFrameRingBuffer FrameBuffer { get; }
            public AnimationBatchWriter BatchWriter { get; }
            public KalmanMotionPredictor KalmanPredictor { get; private set; } // 卡尔曼滤波预测器
            
            // 状态标志
            public bool IsActive { get; private set; }
            public double LastFrameTime { get; private set; }
            
            // 参数哈希缓存
            private readonly Dictionary<int, int> _floatParamHashes;
            private readonly Dictionary<int, int> _boolParamHashes;
            
            // 参数名称映射（与 AnimatorSyncManager 保持一致）
            private static readonly string[] FloatParamNames = new string[]
            {
                "MoveSpeed",   // 0
                "MoveDirX",    // 1
                "MoveDirY",    // 2
                "HandState",   // 3 (作为 float 传输的 int)
                "",            // 4-7 预留
                "",
                "",
                ""
            };
            
            private static readonly string[] BoolParamNames = new string[]
            {
                "Dashing",      // 0
                "RightHandOut", // 1
                "Attack",       // 2
                "GunReady",     // 3
            };
            
            public RemotePlayerAnimationState(string playerId, GameObject gameObject)
            {
                PlayerId = playerId;
                GameObject = gameObject;
                
                // 初始化核心组件
                FrameBuffer = new AnimationFrameRingBuffer(32);
                BatchWriter = new AnimationBatchWriter();
                KalmanPredictor = new KalmanMotionPredictor();
                
                // 初始化参数哈希缓存
                _floatParamHashes = new Dictionary<int, int>();
                _boolParamHashes = new Dictionary<int, int>();
                
                // 缓存参数哈希
                for (int i = 0; i < FloatParamNames.Length; i++)
                {
                    if (!string.IsNullOrEmpty(FloatParamNames[i]))
                    {
                        _floatParamHashes[i] = Animator.StringToHash(FloatParamNames[i]);
                    }
                }
                
                for (int i = 0; i < BoolParamNames.Length; i++)
                {
                    if (!string.IsNullOrEmpty(BoolParamNames[i]))
                    {
                        _boolParamHashes[i] = Animator.StringToHash(BoolParamNames[i]);
                    }
                }
                
                // 查找 Animator
                TryLinkAnimator();
            }
            
            /// <summary>
            /// 更新 GameObject 引用(场景切换后角色重新创建时调用)
            /// </summary>
            public void UpdateGameObject(GameObject newGameObject)
            {
                GameObject = newGameObject;
                Animator = null; // 清空旧的 Animator
                IsActive = false;
                
                UnityEngine.Debug.Log($"[RemoteAnimatorSync] 🔄 更新 GameObject 引用: {PlayerId}");
                
                // 重新绑定 Animator
                TryLinkAnimator();
            }
            
            private void TryLinkAnimator()
            {
                // 检查 GameObject 是否有效
                if (GameObject == null || !GameObject)
                {
                    UnityEngine.Debug.LogWarning($"[RemoteAnimatorSync] GameObject 无效或已销毁: {PlayerId}");
                    IsActive = false;
                    return;
                }
                
                try
                {
                    // 方式1: 通过 CharacterMainControl
                    var charControlType = HarmonyLib.AccessTools.TypeByName("CharacterMainControl");
                    if (charControlType != null)
                    {
                        var charControl = GameObject.GetComponent(charControlType);
                        if (charControl != null)
                        {
                            var modelField = HarmonyLib.AccessTools.Field(charControlType, "characterModel");
                            var model = modelField?.GetValue(charControl) as GameObject;
                            if (model != null)
                            {
                                Animator = model.GetComponentInChildren<Animator>(true);
                            }
                        }
                    }
                    
                    // 方式2: 直接查找
                    if (Animator == null)
                    {
                        Animator = GameObject.GetComponentInChildren<Animator>(true);
                    }
                    
                    if (Animator != null)
                    {
                        Animator.applyRootMotion = false;
                        IsActive = true;
                        UnityEngine.Debug.Log($"[RemoteAnimatorSync] ✅ Animator 绑定成功: {PlayerId}");
                        
                        // 🔥 禁用动画控制脚本,防止本地逻辑覆盖网络同步的动画参数
                        DisableAnimationControl();
                    }
                    else
                    {
                        UnityEngine.Debug.LogWarning($"[RemoteAnimatorSync] ⚠️ 未找到 Animator: {PlayerId}");
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[RemoteAnimatorSync] 链接 Animator 失败: {ex.Message}");
                }
            }
            
            /// <summary>
            /// 禁用游戏原本的动画控制脚本,防止覆盖网络同步的参数
            /// </summary>
            private void DisableAnimationControl()
            {
                try
                {
                    if (GameObject == null) return;
                    
                    // 禁用 CharacterAnimationControl
                    var animControlType = HarmonyLib.AccessTools.TypeByName("CharacterAnimationControl");
                    if (animControlType != null)
                    {
                        var animControl = GameObject.GetComponentInChildren(animControlType) as MonoBehaviour;
                        if (animControl != null)
                        {
                            animControl.enabled = false;
                            UnityEngine.Debug.Log($"[RemoteAnimatorSync] ✅ 已禁用 CharacterAnimationControl: {PlayerId}");
                        }
                    }
                    
                    // 禁用 CharacterAnimationControl_MagicBlend
                    var magicBlendType = HarmonyLib.AccessTools.TypeByName("CharacterAnimationControl_MagicBlend");
                    if (magicBlendType != null)
                    {
                        var magicBlend = GameObject.GetComponentInChildren(magicBlendType) as MonoBehaviour;
                        if (magicBlend != null)
                        {
                            magicBlend.enabled = false;
                            UnityEngine.Debug.Log($"[RemoteAnimatorSync] ✅ 已禁用 CharacterAnimationControl_MagicBlend: {PlayerId}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogWarning($"[RemoteAnimatorSync] 禁用动画控制脚本失败: {ex.Message}");
                }
            }
            
            /// <summary>
            /// 接收远程动画数据
            /// </summary>
            public void ReceiveAnimatorData(AnimatorSyncData syncData)
            {
                // 转换为 AnimationFrame
                var frame = new AnimationFrame
                {
                    Timestamp = Time.unscaledTimeAsDouble,
                    MoveSpeed = syncData.GetFloatParam(0),
                    MoveDirX = syncData.GetFloatParam(1),
                    MoveDirY = syncData.GetFloatParam(2),
                    IsDashing = syncData.GetBoolParam(0),
                    IsGunReady = syncData.GetBoolParam(3),
                    IsReloading = false, // 如果需要，可以扩展
                    IsDead = false,
                    HandState = (int)syncData.GetFloatParam(3), // HandState 作为 int
                    AttackIndex = 0,
                    StateHash = syncData.StateHash,
                    NormalizedTime = syncData.GetNormalizedTime()
                };
                
                // 检查时间合法性
                if (FrameBuffer.Count > 0)
                {
                    var lastFrame = FrameBuffer.GetLatest();
                    double deltaTime = frame.Timestamp - lastFrame.Timestamp;
                    
                    // 时间异常，清空缓冲
                    if (deltaTime < -0.05 || deltaTime > 2.0)
                    {
                        UnityEngine.Debug.LogWarning($"[RemoteAnimatorSync] 时间异常，清空缓冲: {PlayerId}, dt={deltaTime:F3}s");
                        FrameBuffer.Clear();
                        KalmanPredictor.Reset();
                    }
                }
                
                // 推入帧
                FrameBuffer.Push(frame);
                LastFrameTime = frame.Timestamp;
                
                // 更新卡尔曼滤波器
                KalmanPredictor.Update(frame.MoveSpeed, frame.MoveDirX, frame.MoveDirY, frame.Timestamp);
            }
            
            /// <summary>
            /// 更新动画（在 LateUpdate 中调用）
            /// </summary>
            public void UpdateAnimation(float deltaTime, int playbackDelayMs, bool enableExtrapolation)
            {
                // 检查 GameObject 是否被销毁
                if (GameObject == null || !GameObject)
                {
                    IsActive = false;
                    return;
                }
                
                if (!IsActive)
                {
                    // 静默返回,不输出日志(避免刷屏)
                    return;
                }
                
                if (Animator == null)
                {
                    // 静默返回,等待 UpdateGameObject 调用
                    return;
                }
                
                if (FrameBuffer.Count == 0)
                {
                    // UnityEngine.Debug.LogWarning($"[RemoteAnimatorSync] 帧缓冲为空: {PlayerId}");
                    return;
                }
                
                double now = Time.unscaledTimeAsDouble;
                double targetTime = now - (playbackDelayMs / 1000.0);
                
                // 获取目标帧
                AnimationFrame targetFrame;
                
                // 检查数据是否过旧,需要预测
                double timeSinceLastData = now - LastFrameTime;
                if (enableExtrapolation && timeSinceLastData > 0.1 && timeSinceLastData < KalmanPredictor.MaxPredictionTime)
                {
                    // 数据过旧,使用卡尔曼滤波预测
                    var lastFrame = FrameBuffer.GetLatest();
                    float predictionDelta = (float)timeSinceLastData;
                    targetFrame = KalmanPredictor.Predict(lastFrame, predictionDelta);
                    
                    // UnityEngine.Debug.Log($"[RemoteAnimatorSync] 使用卡尔曼预测: {PlayerId}, 延迟={timeSinceLastData:F3}s, 置信度={KalmanPredictor.GetConfidence():F2}");
                }
                else
                {
                    // 从缓冲区获取插值帧
                    targetFrame = FrameBuffer.FindFrameAtTime(targetTime);
                }
                
                // 应用到批写入器
                ApplyFrameToBatchWriter(targetFrame);
                
                // 提交到 Animator
                UnityEngine.Debug.Log($"[RemoteAnimatorSync] 正在提交动画到 Animator: {PlayerId}, 参数数量: {BatchWriter.GetCachedParamCount()}");
                BatchWriter.Commit(Animator, deltaTime);
            }
            
            private void ApplyFrameToBatchWriter(AnimationFrame frame)
            {
                UnityEngine.Debug.Log($"[RemoteAnimatorSync] 应用动画帧: {PlayerId}, MoveSpeed={frame.MoveSpeed:F2}, MoveDirX={frame.MoveDirX:F2}, MoveDirY={frame.MoveDirY:F2}");
                
                // Float 参数
                if (_floatParamHashes.TryGetValue(0, out int moveSpeedHash))
                {
                    BatchWriter.SetFloat(moveSpeedHash, frame.MoveSpeed);
                    UnityEngine.Debug.Log($"[RemoteAnimatorSync] 设置 MoveSpeed: {frame.MoveSpeed:F2} (Hash: {moveSpeedHash})");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[RemoteAnimatorSync] 未找到 MoveSpeed 参数哈希");
                }
                
                if (_floatParamHashes.TryGetValue(1, out int moveDirXHash))
                {
                    BatchWriter.SetFloat(moveDirXHash, frame.MoveDirX);
                    UnityEngine.Debug.Log($"[RemoteAnimatorSync] 设置 MoveDirX: {frame.MoveDirX:F2} (Hash: {moveDirXHash})");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[RemoteAnimatorSync] 未找到 MoveDirX 参数哈希");
                }
                
                if (_floatParamHashes.TryGetValue(2, out int moveDirYHash))
                {
                    BatchWriter.SetFloat(moveDirYHash, frame.MoveDirY);
                    UnityEngine.Debug.Log($"[RemoteAnimatorSync] 设置 MoveDirY: {frame.MoveDirY:F2} (Hash: {moveDirYHash})");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"[RemoteAnimatorSync] 未找到 MoveDirY 参数哈希");
                }
                
                if (_floatParamHashes.TryGetValue(3, out int handStateHash))
                {
                    BatchWriter.SetInt(handStateHash, frame.HandState);
                    UnityEngine.Debug.Log($"[RemoteAnimatorSync] 设置 HandState: {frame.HandState} (Hash: {handStateHash})");
                }
                
                // Bool 参数
                if (_boolParamHashes.TryGetValue(0, out int dashingHash))
                {
                    BatchWriter.SetBool(dashingHash, frame.IsDashing);
                    UnityEngine.Debug.Log($"[RemoteAnimatorSync] 设置 Dashing: {frame.IsDashing} (Hash: {dashingHash})");
                }
                
                if (_boolParamHashes.TryGetValue(3, out int gunReadyHash))
                {
                    BatchWriter.SetBool(gunReadyHash, frame.IsGunReady);
                    UnityEngine.Debug.Log($"[RemoteAnimatorSync] 设置 GunReady: {frame.IsGunReady} (Hash: {gunReadyHash})");
                }
            }
            
            public void Dispose()
            {
                BatchWriter?.Clear();
                FrameBuffer?.Clear();
                KalmanPredictor?.Reset();
            }
        }
        
        /// <summary>
        /// 注册远程玩家(支持幂等性,如果已存在则跳过)
        /// </summary>
        public void RegisterRemotePlayer(string playerId, GameObject playerObject)
        {
            if (_playerStates.ContainsKey(playerId))
            {
                // 已存在,跳过(不输出警告,因为可能是场景切换后更新)
                return;
            }
            
            var state = new RemotePlayerAnimationState(playerId, playerObject);
            state.BatchWriter.SetSmoothTime(SmoothTime);
            
            _playerStates[playerId] = state;
            UnityEngine.Debug.Log($"[RemoteAnimatorSync] 注册远程玩家: {playerId}");
        }
        
        /// <summary>
        /// 注销远程玩家
        /// </summary>
        public void UnregisterRemotePlayer(string playerId)
        {
            if (_playerStates.TryGetValue(playerId, out var state))
            {
                state.Dispose();
                _playerStates.Remove(playerId);
                UnityEngine.Debug.Log($"[RemoteAnimatorSync] 注销远程玩家: {playerId}");
            }
        }
        
        /// <summary>
        /// 更新远程玩家的 GameObject (场景切换后角色重新创建时调用)
        /// </summary>
        public void UpdatePlayerGameObject(string playerId, GameObject newGameObject)
        {
            if (_playerStates.TryGetValue(playerId, out var state))
            {
                state.UpdateGameObject(newGameObject);
                UnityEngine.Debug.Log($"[RemoteAnimatorSync] 🔄 更新玩家 GameObject: {playerId}");
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[RemoteAnimatorSync] 未找到玩家状态,无法更新 GameObject: {playerId}");
            }
        }
        
        /// <summary>
        /// 接收远程玩家的动画数据
        /// </summary>
        public void ReceiveAnimatorUpdate(string playerId, AnimatorSyncData syncData)
        {
            UnityEngine.Debug.Log($"[RemoteAnimatorSync] 🎬 接收动画 - PlayerId:{playerId}, State:{syncData.StateHash}, 已注册玩家数:{_playerStates.Count}");
            
            if (_playerStates.TryGetValue(playerId, out var state))
            {
                UnityEngine.Debug.Log($"[RemoteAnimatorSync] ✅ 找到玩家状态: {playerId}");
                state.ReceiveAnimatorData(syncData);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[RemoteAnimatorSync] ⚠️ 未找到玩家状态: {playerId}，已注册玩家: {string.Join(", ", _playerStates.Keys)}");
            }
        }
        
        /// <summary>
        /// 更新所有远程玩家动画（在 LateUpdate 中调用）
        /// </summary>
        public void UpdateAll()
        {
            float deltaTime = Time.unscaledDeltaTime;
            
            foreach (var state in _playerStates.Values)
            {
                state.UpdateAnimation(deltaTime, PlaybackDelayMs, EnableExtrapolation);
            }
        }
        
        /// <summary>
        /// 获取统计信息
        /// </summary>
        public string GetStats()
        {
            return $"远程玩家: {_playerStates.Count}, " +
                   $"延迟: {PlaybackDelayMs}ms, " +
                   $"外推: {(EnableExtrapolation ? "启用" : "禁用")}";
        }
        
        public void Dispose()
        {
            foreach (var state in _playerStates.Values)
            {
                state.Dispose();
            }
            _playerStates.Clear();
        }
    }
}
