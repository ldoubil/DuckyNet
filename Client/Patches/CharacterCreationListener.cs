using System;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using System.Threading.Tasks;

namespace DuckyNet.Client.Patches
{
    /// <summary>
    /// 角色创建监听器 - 使用 Harmony Patch 拦截主角色创建方法
    /// </summary>
    [HarmonyPatch]
    public class CharacterCreationListener
    {
        /// <summary>
        /// 主角色创建完成事件
        /// 参数：主角色对象（CharacterMainControl，通过反射获取）
        /// </summary>
        public static event Action<object>? OnMainCharacterCreated;

        // 防抖机制：避免重复触发
        private static DateTime _lastTriggerTime = DateTime.MinValue;
        private static readonly TimeSpan _debounceInterval = TimeSpan.FromSeconds(2); // 2秒内只触发一次

        private static Type? _levelManagerType;
        private static Type? _uniTaskType;


        /// <summary>
        /// 动态查找目标方法：LevelManager.CreateMainCharacterAsync
        /// </summary>
        static MethodBase? TargetMethod()
        {
            try
            {
                Debug.Log("[CharacterCreationListener] ========== 开始查找目标方法 ==========");

                _levelManagerType = AccessTools.TypeByName("LevelManager");
                if (_levelManagerType == null)
                {
                    Debug.LogError("[CharacterCreationListener] ❌ 无法找到 LevelManager 类型");
                    return null;
                }
                Debug.Log($"[CharacterCreationListener] ✅ 找到 LevelManager 类型: {_levelManagerType.FullName}");

                // 查找 UniTask 类型（用于方法签名）
                _uniTaskType = AccessTools.TypeByName("Cysharp.Threading.Tasks.UniTask");
                if (_uniTaskType == null)
                {
                    Debug.LogError("[CharacterCreationListener] ❌ 无法找到 UniTask 类型");
                    return null;
                }

                // 查找 CreateMainCharacterAsync 方法
                // 方法签名：private async UniTask CreateMainCharacterAsync(Vector3 position, Quaternion rotation)
                var method = AccessTools.Method(_levelManagerType, "CreateMainCharacterAsync", 
                    new[] { typeof(Vector3), typeof(Quaternion) });

                if (method != null)
                {
                    Debug.Log($"[CharacterCreationListener] ✅✅✅ 成功找到目标方法！");
                    Debug.Log($"[CharacterCreationListener] 方法: {method.DeclaringType?.FullName}.{method.Name}");
                    Debug.Log($"[CharacterCreationListener] 返回类型: {method.ReturnType.FullName}");
                    Debug.Log("[CharacterCreationListener] ========================================");
                    return method;
                }

                Debug.LogError("[CharacterCreationListener] ❌❌❌ 无法找到 CreateMainCharacterAsync 方法");
                Debug.LogError("[CharacterCreationListener] 可能原因:");
                Debug.LogError("  1. 方法名不匹配");
                Debug.LogError("  2. 参数类型不匹配");
                Debug.LogError("  3. 方法访问修饰符不是预期的（应该是 private）");
                Debug.Log("[CharacterCreationListener] ========================================");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterCreationListener] TargetMethod 异常: {ex.Message}");
                Debug.LogException(ex);
            }

            return null;
        }

        /// <summary>
        /// Harmony Postfix - 在 CreateMainCharacterAsync 执行后触发
        /// </summary>
        [HarmonyPostfix]
        static async void CreateMainCharacterAsync_Postfix(object __result)
        {
            try
            {
                if (__result == null)
                {
                    Debug.LogWarning("[CharacterCreationListener] ⚠️ CreateMainCharacterAsync 返回 null");
                    return;
                }

                Debug.Log($"[CharacterCreationListener] 检测到 CreateMainCharacterAsync 调用，等待 UniTask 完成...");

                // 等待 UniTask 完成
                await WaitForUniTask(__result);

                // 等待一小段时间确保主角色已完全初始化
                await Task.Delay(100);

                // 获取主角色引用
                var mainCharacter = GetMainCharacter();

                if (mainCharacter != null)
                {
                    // 防抖：检查距离上次触发的时间间隔
                    var now = DateTime.Now;
                    if (now - _lastTriggerTime < _debounceInterval)
                    {
                        Debug.Log($"[CharacterCreationListener] 忽略重复触发（距离上次 {((now - _lastTriggerTime).TotalSeconds):F1}秒）");
                        return;
                    }

                    _lastTriggerTime = now;

                    // 获取角色信息用于日志
                    var characterType = mainCharacter.GetType();
                    var nameProp = AccessTools.Property(characterType, "name") ?? AccessTools.Property(typeof(GameObject), "name");
                    var transformProp = AccessTools.Property(characterType, "transform") ?? AccessTools.Property(typeof(GameObject), "transform");
                    
                    string? characterName = null;
                    Vector3? position = null;

                    if (nameProp != null)
                    {
                        characterName = nameProp.GetValue(mainCharacter) as string;
                    }

                    if (transformProp != null)
                    {
                        var transform = transformProp.GetValue(mainCharacter) as Transform;
                        if (transform != null)
                        {
                            position = transform.position;
                        }
                    }

                    Debug.Log($"[CharacterCreationListener] ✅ 主角色创建完成");
                    if (characterName != null)
                    {
                        Debug.Log($"  - 名称: {characterName}");
                    }
                    if (position.HasValue)
                    {
                        Debug.Log($"  - 位置: {position.Value}");
                    }

                    // 触发事件
                    OnMainCharacterCreated?.Invoke(mainCharacter);

                    Debug.Log("[CharacterCreationListener] ✅ OnMainCharacterCreated 事件已触发");
                }
                else
                {
                    Debug.LogWarning("[CharacterCreationListener] ⚠️ 主角色为 null（可能还未完全初始化）");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterCreationListener] Postfix 异常: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 等待 UniTask 完成
        /// </summary>
        private static async Task WaitForUniTask(object uniTask)
        {
            try
            {
                var resultType = uniTask.GetType();

                // 获取 GetAwaiter() 方法
                var getAwaiterMethod = resultType.GetMethod("GetAwaiter", BindingFlags.Public | BindingFlags.Instance);
                if (getAwaiterMethod == null)
                {
                    Debug.LogError("[CharacterCreationListener] 找不到 GetAwaiter() 方法");
                    return;
                }

                var awaiter = getAwaiterMethod.Invoke(uniTask, null);
                if (awaiter == null)
                {
                    Debug.LogError("[CharacterCreationListener] GetAwaiter() 返回 null");
                    return;
                }

                var awaiterType = awaiter.GetType();

                // 获取 IsCompleted 属性和 GetResult 方法
                var isCompletedProp = awaiterType.GetProperty("IsCompleted");
                var getResultMethod = awaiterType.GetMethod("GetResult");

                if (isCompletedProp == null || getResultMethod == null)
                {
                    Debug.LogError("[CharacterCreationListener] 找不到 IsCompleted 或 GetResult");
                    return;
                }

                // 轮询等待完成
                int maxWaitMs = 30000; // 最多等待30秒
                int elapsedMs = 0;
                int pollIntervalMs = 50;

                while (!(bool)isCompletedProp.GetValue(awaiter))
                {
                    await Task.Delay(pollIntervalMs);
                    elapsedMs += pollIntervalMs;

                    if (elapsedMs >= maxWaitMs)
                    {
                        Debug.LogError("[CharacterCreationListener] 等待 UniTask 完成超时");
                        return;
                    }
                }

                // 调用 GetResult() 确保任务完成（即使无返回值）
                getResultMethod.Invoke(awaiter, null);

                Debug.Log("[CharacterCreationListener] ✅ UniTask 已完成");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterCreationListener] WaitForUniTask 异常: {ex.Message}");
                Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 获取主角色对象
        /// </summary>
        private static object? GetMainCharacter()
        {
            try
            {
                if (_levelManagerType == null)
                {
                    _levelManagerType = AccessTools.TypeByName("LevelManager");
                    if (_levelManagerType == null) return null;
                }

                var instanceProp = AccessTools.Property(_levelManagerType, "Instance");
                var levelManager = instanceProp?.GetValue(null);
                if (levelManager == null) return null;

                // 检查 MainCharacter 属性
                var mainCharacterProp = AccessTools.Property(_levelManagerType, "MainCharacter");
                if (mainCharacterProp != null)
                {
                    var mainCharacter = mainCharacterProp.GetValue(levelManager);
                    return mainCharacter;
                }

                Debug.LogWarning("[CharacterCreationListener] MainCharacter 属性不存在");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CharacterCreationListener] 获取主角色时异常: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 验证 Patch 是否成功应用
        /// </summary>
        public static bool VerifySubscription()
        {
            try
            {
                if (_levelManagerType == null)
                {
                    _levelManagerType = AccessTools.TypeByName("LevelManager");
                    if (_levelManagerType == null)
                    {
                        Debug.LogError("[CharacterCreationListener] 验证失败: 找不到 LevelManager 类型");
                        return false;
                    }
                }

                var method = AccessTools.Method(_levelManagerType, "CreateMainCharacterAsync",
                    new[] { typeof(Vector3), typeof(Quaternion) });

                if (method == null)
                {
                    Debug.LogError("[CharacterCreationListener] 验证失败: 找不到 CreateMainCharacterAsync 方法");
                    return false;
                }

                var patches = Harmony.GetPatchInfo(method);
                if (patches == null || patches.Postfixes.Count == 0)
                {
                    Debug.LogError("[CharacterCreationListener] 验证失败: Patch 未应用");
                    return false;
                }

                Debug.Log($"[CharacterCreationListener] ✅ 验证成功: Patch 已应用 ({patches.Postfixes.Count} 个 Postfix)");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterCreationListener] 验证异常: {ex.Message}");
                return false;
            }
        }
    }
}
