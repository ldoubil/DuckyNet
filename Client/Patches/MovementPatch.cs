using HarmonyLib;
using UnityEngine;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// Movement 补丁 - 防止远程玩家移动
    /// 通过检查 GameObject 名称跳过远程玩家的移动更新
    /// </summary>
    [HarmonyPatch]
    public static class MovementPatch
    {
        /// <summary>
        /// 远程玩家名称标记
        /// </summary>
        public const string REMOTE_PLAYER_MARKER = "[RemotePlayer]";

        /// <summary>
        /// 获取 Movement 类型
        /// </summary>
        private static System.Type? GetMovementType()
        {
            return AccessTools.TypeByName("Movement");
        }

        /// <summary>
        /// 动态指定要补丁的方法
        /// </summary>
        [HarmonyTargetMethod]
        static System.Reflection.MethodBase? TargetMethod()
        {
            var type = GetMovementType();
            if (type == null)
            {
                Debug.LogWarning("[MovementPatch] 找不到 Movement 类型");
                return null;
            }

            var updateMethod = AccessTools.Method(type, "UpdateMovement");
            if (updateMethod == null)
            {
                Debug.LogWarning("[MovementPatch] 找不到 Movement.UpdateMovement 方法");
                return null;
            }

            Debug.Log("[MovementPatch] ✅ 成功定位 Movement.UpdateMovement 方法");
            return updateMethod;
        }

        /// <summary>
        /// 前置补丁 - 检查是否为远程玩家
        /// </summary>
        [HarmonyPrefix]
        static bool Prefix(object __instance)
        {
            try
            {
                // 将 __instance 转换为 Component
                if (__instance is Component component)
                {
                    // 使用名称检查（避免 Tag 未定义的问题）
                    if (component.gameObject.name.Contains(REMOTE_PLAYER_MARKER))
                    {
                        // 跳过远程玩家的移动更新
                        return false;
                    }
                }

                // 对于本地玩家和其他角色，继续执行原方法
                return true;
            }
            catch (System.Exception)
            {
                // 静默处理异常，避免每帧都输出错误日志
                return true; // 出错时继续执行原方法
            }
        }
    }
}
