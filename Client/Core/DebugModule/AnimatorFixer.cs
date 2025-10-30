using UnityEngine;
using HarmonyLib;

namespace DuckyNet.Client.Core.DebugModule
{
    /// <summary>
    /// Animator修复工具 - 用于禁用/启用动画控制，防止脚本覆盖网络同步的动画参数
    /// </summary>
    public static class AnimatorFixer
    {
        /// <summary>
        /// 获取 CharacterAnimationControl 类型
        /// </summary>
        private static System.Type? GetAnimationControlType()
        {
            // 使用 HarmonyLib 的 AccessTools 查找类型（支持多个可能的类型名）
            var typeNames = new[]
            {
                "CharacterAnimationControl_MagicBlend",
                "CharacterAnimationControl",
                "AnimationControl"
            };

            foreach (var typeName in typeNames)
            {
                var type = AccessTools.TypeByName(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        /// <summary>
        /// 禁用动画控制（用于远程同步的角色）
        /// </summary>
        public static bool DisableAnimationControl(GameObject character)
        {
            if (character == null) return false;

            try
            {
                var animControlType = GetAnimationControlType();
                if (animControlType == null)
                {
                    UnityEngine.Debug.LogWarning($"[AnimatorFixer] 未找到 CharacterAnimationControl 类型");
                    return false;
                }

                // 查找并禁用 CharacterAnimationControl 组件
                var animControl = character.GetComponentInChildren(animControlType, true);
                
                if (animControl != null && animControl is MonoBehaviour mb)
                {
                    mb.enabled = false;
                    UnityEngine.Debug.Log($"[AnimatorFixer] 已禁用动画控制: {character.name}");
                    return true;
                }
                
                UnityEngine.Debug.LogWarning($"[AnimatorFixer] 未找到 CharacterAnimationControl 组件: {character.name}");
                return false;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[AnimatorFixer] 禁用动画控制失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 启用动画控制（恢复本地控制）
        /// </summary>
        public static bool EnableAnimationControl(GameObject character)
        {
            if (character == null) return false;

            try
            {
                var animControlType = GetAnimationControlType();
                if (animControlType == null)
                {
                    UnityEngine.Debug.LogWarning($"[AnimatorFixer] 未找到 CharacterAnimationControl 类型");
                    return false;
                }

                var animControl = character.GetComponentInChildren(animControlType, true);
                
                if (animControl != null && animControl is MonoBehaviour mb)
                {
                    mb.enabled = true;
                    UnityEngine.Debug.Log($"[AnimatorFixer] 已启用动画控制: {character.name}");
                    return true;
                }
                
                return false;
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[AnimatorFixer] 启用动画控制失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查动画控制是否已启用
        /// </summary>
        public static bool IsAnimationControlEnabled(GameObject character)
        {
            if (character == null) return false;

            try
            {
                var animControlType = GetAnimationControlType();
                if (animControlType == null)
                {
                    return false;
                }

                var animControl = character.GetComponentInChildren(animControlType, true);
                
                if (animControl != null && animControl is MonoBehaviour mb)
                {
                    return mb.enabled;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 诊断并修复Animator问题
        /// </summary>
        public static void DiagnoseAndFix(GameObject character)
        {
            if (character == null)
            {
                UnityEngine.Debug.LogWarning("[AnimatorFixer] 角色对象为空");
                return;
            }

            var animator = character.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                UnityEngine.Debug.LogWarning($"[AnimatorFixer] 角色 {character.name} 未找到Animator");
                return;
            }

            UnityEngine.Debug.Log($"[AnimatorFixer] Animator诊断完成: {character.name}");
            UnityEngine.Debug.Log($"  - Controller: {(animator.runtimeAnimatorController != null ? animator.runtimeAnimatorController.name : "None")}");
            UnityEngine.Debug.Log($"  - Enabled: {animator.enabled}");
            UnityEngine.Debug.Log($"  - Parameters: {animator.parameterCount}");
        }
    }
}

