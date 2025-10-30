using System;
using UnityEngine;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// 动画调试器 - 用于调试和测试单位动画
    /// </summary>
    public class AnimationDebugger
    {
        public AnimationDebugger()
        {
        }

        /// <summary>
        /// 记录动画信息到控制台
        /// </summary>
        public void LogAnimationInfo(GameObject unit)
        {
            if (unit == null)
            {
                UnityEngine.Debug.LogWarning("[AnimationDebugger] 单位对象为空");
                return;
            }

            var animator = unit.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                UnityEngine.Debug.LogWarning($"[AnimationDebugger] 单位 {unit.name} 未找到Animator");
                return;
            }

            UnityEngine.Debug.Log($"=== {unit.name} 动画信息 ===");
            UnityEngine.Debug.Log($"Controller: {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "None")}");
            UnityEngine.Debug.Log($"Enabled: {animator.enabled}");
            UnityEngine.Debug.Log($"Parameters: {animator.parameterCount}");

            for (int i = 0; i < animator.parameterCount; i++)
            {
                var param = animator.parameters[i];
                object value = param.type switch
                {
                    AnimatorControllerParameterType.Float => animator.GetFloat(param.name),
                    AnimatorControllerParameterType.Int => animator.GetInteger(param.name),
                    AnimatorControllerParameterType.Bool => animator.GetBool(param.name),
                    AnimatorControllerParameterType.Trigger => "Trigger",
                    _ => "Unknown"
                };
                UnityEngine.Debug.Log($"  {param.name} ({param.type}): {value}");
            }
        }

        /// <summary>
        /// 设置Animator浮点数参数
        /// </summary>
        public void SetAnimatorFloat(GameObject unit, string paramName, float value)
        {
            var animator = unit?.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.SetFloat(paramName, value);
            }
        }

        /// <summary>
        /// 设置Animator整数参数
        /// </summary>
        public void SetAnimatorInt(GameObject unit, string paramName, int value)
        {
            var animator = unit?.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.SetInteger(paramName, value);
            }
        }

        /// <summary>
        /// 设置Animator布尔参数
        /// </summary>
        public void SetAnimatorBool(GameObject unit, string paramName, bool value)
        {
            var animator = unit?.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.SetBool(paramName, value);
            }
        }

        /// <summary>
        /// 触发Animator Trigger
        /// </summary>
        public void TriggerAnimation(GameObject unit, string triggerName)
        {
            var animator = unit?.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.SetTrigger(triggerName);
            }
        }

        /// <summary>
        /// 设置动画层权重
        /// </summary>
        public void SetLayerWeight(GameObject unit, string layerName, float weight)
        {
            var animator = unit?.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                for (int i = 0; i < animator.layerCount; i++)
                {
                    var layerNameFromAnimator = animator.GetLayerName(i);
                    if (layerNameFromAnimator == layerName)
                    {
                        animator.SetLayerWeight(i, weight);
                        return;
                    }
                }
                UnityEngine.Debug.LogWarning($"[AnimationDebugger] 未找到层: {layerName}");
            }
        }

        /// <summary>
        /// 测试移动动画
        /// </summary>
        public void TestMovementAnimation(GameObject unit, float speed, Vector2 direction)
        {
            var animator = unit?.GetComponentInChildren<Animator>(true);
            if (animator != null)
            {
                animator.SetFloat("MoveSpeed", speed);
                animator.SetFloat("MoveDirX", direction.x);
                animator.SetFloat("MoveDirY", direction.y);
            }
        }

        /// <summary>
        /// 诊断本地玩家角色
        /// </summary>
        public void DiagnoseLocalPlayerCharacter()
        {
            try
            {
                if (Core.GameContext.IsInitialized)
                {
                    var localPlayer = Core.GameContext.Instance.LocalPlayer;
                    UnityEngine.Debug.Log("[AnimationDebugger] 开始诊断本地玩家角色...");
                    // 这里可以添加更多诊断逻辑
                }
                else
                {
                    UnityEngine.Debug.LogWarning("[AnimationDebugger] GameContext未初始化");
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[AnimationDebugger] 诊断失败: {ex.Message}");
            }
        }
    }
}

