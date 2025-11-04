using System;
using System.Collections.Generic;
using UnityEngine;
using DuckyNet.Shared.Data;
using DuckyNet.Shared.Services;
using DuckyNet.Client.Core.EventBus.Events;

namespace DuckyNet.Client.Services
{
    /// <summary>
    /// 动画同步客户端服务实现
    /// 接收远程玩家动画状态并应用到对应角色
    /// </summary>
    public class AnimatorSyncClientServiceImpl : IAnimatorSyncClientService
    {
        // 缓存已处理的远程玩家，避免重复禁用组件
        private HashSet<string> _processedPlayers = new HashSet<string>();
        
        // 为每个远程玩家缓存平滑组件
        private Dictionary<string, RemoteAnimatorSmoother> _smoothers = new Dictionary<string, RemoteAnimatorSmoother>();

        /// <summary>
        /// 每帧更新 - 平滑插值所有远程玩家的动画参数
        /// </summary>
        public void Update()
        {
            foreach (var smoother in _smoothers.Values)
            {
                smoother.Update();
            }
        }

        public void OnAnimatorStateUpdated(string steamId, AnimatorSyncData animatorData)
        {
            try
            {
                if (!Core.GameContext.IsInitialized)
                {
                    Debug.LogWarning($"[AnimatorSyncClientService] GameContext 未初始化，跳过玩家 {steamId} 的动画");
                    return;
                }

                // Debug.Log($"[AnimatorSyncClientService] 📥 收到远程动画 - PlayerId:{steamId}, State:{animatorData.StateHash}, Speed:{animatorData.GetFloatParam(0):F2}");

                // 🎯 新架构：直接发布事件到 EventBus，由 RemoteAnimatorSyncManager 处理
                if (Core.GameContext.Instance.EventBus != null)
                {
                    Core.GameContext.Instance.EventBus.Publish(
                        new RemoteAnimatorUpdateEvent(steamId, animatorData)
                    );
                    // Debug.Log($"[AnimatorSyncClientService] ✅ 事件已发布到 EventBus");
                }
                else
                {
                    Debug.LogError($"[AnimatorSyncClientService] ❌ EventBus 为空！");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[AnimatorSyncClientService] 发布动画事件失败: {ex.Message}\n{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 禁用动画控制脚本（防止本地逻辑覆盖同步的动画参数）
        /// </summary>
        private void DisableAnimationControl(object characterObject)
        {
            try
            {
                if (!(characterObject is Component comp)) return;

                // 禁用 CharacterAnimationControl
                var animControlType = HarmonyLib.AccessTools.TypeByName("CharacterAnimationControl");
                if (animControlType != null)
                {
                    var animControl = comp.GetComponentInChildren(animControlType) as MonoBehaviour;
                    if (animControl != null)
                    {
                        animControl.enabled = false;
                        Debug.Log($"[AnimatorSyncClientService] ✅ 已禁用远程玩家的 CharacterAnimationControl");
                    }
                }

                // 禁用 CharacterAnimationControl_MagicBlend（如果存在）
                var magicBlendType = HarmonyLib.AccessTools.TypeByName("CharacterAnimationControl_MagicBlend");
                if (magicBlendType != null)
                {
                    var magicBlend = comp.GetComponentInChildren(magicBlendType) as MonoBehaviour;
                    if (magicBlend != null)
                    {
                        magicBlend.enabled = false;
                        Debug.Log($"[AnimatorSyncClientService] ✅ 已禁用远程玩家的 CharacterAnimationControl_MagicBlend");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AnimatorSyncClientService] 禁用动画控制脚本失败: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 远程玩家动画平滑器 - 每帧持续应用动画参数
    /// </summary>
    internal class RemoteAnimatorSmoother
    {
        private readonly Animator _animator;
        private AnimatorSyncData? _targetData;
        
        // 参数哈希缓存
        private readonly int _moveSpeedHash;
        private readonly int _moveDirXHash;
        private readonly int _moveDirYHash;
        private readonly int _handStateHash;
        private readonly int _dashingHash;
        private readonly int _rightHandOutHash;
        private readonly int _attackHash;
        private readonly int _gunReadyHash;
        
        public RemoteAnimatorSmoother(Animator animator)
        {
            _animator = animator;
            
            // 预计算参数哈希
            _moveSpeedHash = Animator.StringToHash("MoveSpeed");
            _moveDirXHash = Animator.StringToHash("MoveDirX");
            _moveDirYHash = Animator.StringToHash("MoveDirY");
            _handStateHash = Animator.StringToHash("HandState");
            _dashingHash = Animator.StringToHash("Dashing");
            _rightHandOutHash = Animator.StringToHash("RightHandOut");
            _attackHash = Animator.StringToHash("Attack");
            _gunReadyHash = Animator.StringToHash("GunReady");
        }
        
        /// <summary>
        /// 接收新的动画状态
        /// </summary>
        public void OnReceiveAnimatorState(AnimatorSyncData syncData)
        {
            _targetData = syncData;
            
            // 调试日志
            Debug.Log($"[RemoteAnimatorSmoother] 收到动画状态 - StateHash:{syncData.StateHash}, " +
                      $"MoveSpeed:{syncData.GetFloatParam(0):F2}, MoveDirX:{syncData.GetFloatParam(1):F2}, MoveDirY:{syncData.GetFloatParam(2):F2}, " +
                      $"Dashing:{syncData.GetBoolParam(0)}, RightHandOut:{syncData.GetBoolParam(1)}");
            
            // 状态切换立即应用
            var currentState = _animator.GetCurrentAnimatorStateInfo(0);
            if (currentState.fullPathHash != syncData.StateHash)
            {
                // 使用 CrossFade 平滑过渡
                _animator.CrossFade(syncData.StateHash, 0.2f, 0, syncData.GetNormalizedTime());
                Debug.Log($"[RemoteAnimatorSmoother] 切换状态: {currentState.fullPathHash} → {syncData.StateHash}");
            }
            
            // 注意：所有参数在 Update() 中每帧持续设置
        }
        
        /// <summary>
        /// 每帧更新 - 持续设置所有动画参数
        /// </summary>
        public void Update()
        {
            if (_targetData == null || _animator == null) return;
            
            try
            {
                // Float 参数 - 每帧直接设置（移除 dampTime）
                float moveSpeed = _targetData.GetFloatParam(0);
                float moveDirX = _targetData.GetFloatParam(1);
                float moveDirY = _targetData.GetFloatParam(2);
                
                _animator.SetFloat(_moveSpeedHash, moveSpeed);
                _animator.SetFloat(_moveDirXHash, moveDirX);
                _animator.SetFloat(_moveDirYHash, moveDirY);
                
                // Bool 参数每帧持续设置（关键！）
                _animator.SetBool(_dashingHash, _targetData.GetBoolParam(0));
                _animator.SetBool(_rightHandOutHash, _targetData.GetBoolParam(1));
                _animator.SetBool(_attackHash, _targetData.GetBoolParam(2));
                _animator.SetBool(_gunReadyHash, _targetData.GetBoolParam(3));
                
                // 每 60 帧记录一次（约 1 秒）
                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[RemoteAnimatorSmoother] Update - MoveSpeed:{moveSpeed:F2}, MoveDirX:{moveDirX:F2}, MoveDirY:{moveDirY:F2}, " +
                              $"Dashing:{_targetData.GetBoolParam(0)}, RightHandOut:{_targetData.GetBoolParam(1)}");
                }
                
                // Integer 参数直接设置
                _animator.SetInteger(_handStateHash, (int)_targetData.GetFloatParam(3));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RemoteAnimatorSmoother] Update 失败: {ex.Message}");
            }
        }
    }
}
