using System;
using System.Reflection;
using UnityEngine;

namespace DuckyNet.Client.Core.Utils
{
    /// <summary>
    /// UniTask 辅助工具 - 处理 UniTask 异步任务的等待
    /// </summary>
    public static class UniTaskHelper
    {
        /// <summary>
        /// 等待 UniTask 完成
        /// </summary>
        public static async System.Threading.Tasks.Task WaitForUniTask(object uniTask)
        {
            try
            {
                var resultType = uniTask.GetType();

                // 获取 GetAwaiter() 方法
                var getAwaiterMethod = resultType.GetMethod("GetAwaiter", BindingFlags.Public | BindingFlags.Instance);
                if (getAwaiterMethod == null)
                {
                    UnityEngine.Debug.LogError("[UniTaskHelper] 找不到 GetAwaiter() 方法");
                    return;
                }

                var awaiter = getAwaiterMethod.Invoke(uniTask, null);
                if (awaiter == null)
                {
                    UnityEngine.Debug.LogError("[UniTaskHelper] GetAwaiter() 返回 null");
                    return;
                }

                var awaiterType = awaiter.GetType();

                // 获取 IsCompleted 属性和 GetResult 方法
                var isCompletedProp = awaiterType.GetProperty("IsCompleted");
                var getResultMethod = awaiterType.GetMethod("GetResult");

                if (isCompletedProp == null || getResultMethod == null)
                {
                    UnityEngine.Debug.LogError("[UniTaskHelper] 找不到 IsCompleted 或 GetResult");
                    return;
                }

                // 轮询等待完成
                int maxWaitMs = 30000; // 最多等待30秒
                int elapsedMs = 0;
                int pollIntervalMs = 50;

                while (!(bool)isCompletedProp.GetValue(awaiter))
                {
                    await System.Threading.Tasks.Task.Delay(pollIntervalMs);
                    elapsedMs += pollIntervalMs;

                    if (elapsedMs >= maxWaitMs)
                    {
                        UnityEngine.Debug.LogError("[UniTaskHelper] 等待 UniTask 完成超时");
                        return;
                    }
                }

                // 调用 GetResult() 确保任务完成（即使无返回值）
                getResultMethod.Invoke(awaiter, null);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[UniTaskHelper] WaitForUniTask 异常: {ex.Message}");
                UnityEngine.Debug.LogException(ex);
            }
        }

        /// <summary>
        /// 同步等待 UniTask（阻塞调用）
        /// </summary>
        public static object? WaitForUniTaskSync(object uniTask)
        {
            try
            {
                var resultType = uniTask.GetType();
                var getAwaiterMethod = resultType.GetMethod("GetAwaiter", BindingFlags.Public | BindingFlags.Instance);
                if (getAwaiterMethod == null) return null;

                var awaiter = getAwaiterMethod.Invoke(uniTask, null);
                if (awaiter == null) return null;

                var awaiterType = awaiter.GetType();
                var isCompletedProp = awaiterType.GetProperty("IsCompleted");
                var getResultMethod = awaiterType.GetMethod("GetResult");

                if (isCompletedProp == null || getResultMethod == null) return null;

                // 如果已完成，直接返回结果
                if ((bool)isCompletedProp.GetValue(awaiter))
                {
                    return getResultMethod.Invoke(awaiter, null);
                }

                // 如果未完成，轮询等待（阻塞）
                while (!(bool)isCompletedProp.GetValue(awaiter))
                {
                    System.Threading.Thread.Sleep(50);
                }

                return getResultMethod.Invoke(awaiter, null);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[UniTaskHelper] WaitForUniTaskSync 异常: {ex.Message}");
                return null;
            }
        }
    }
}

