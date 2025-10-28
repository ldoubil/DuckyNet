using System;
using System.Threading.Tasks;

namespace DuckyNet.Shared.RPC
{
    /// <summary>
    /// RPC 扩展方法
    /// </summary>
    public static class RpcExtensions
    {
        /// <summary>
        /// 安全调用（忽略异常）
        /// </summary>
        public static void SafeInvoke<TService>(this IClientContext context, string methodName, params object[] parameters) 
            where TService : class
        {
            try
            {
                context.Invoke<TService>(methodName, parameters);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RPC] SafeInvoke failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 安全异步调用（忽略异常）
        /// </summary>
        public static async Task<TResult> SafeInvokeAsync<TService, TResult>(this IClientContext context, string methodName, params object[] parameters) 
            where TService : class
        {
            try
            {
                return await context.InvokeAsync<TService, TResult>(methodName, parameters);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[RPC] SafeInvokeAsync failed: {ex.Message}");
                return default!;
            }
        }

        /// <summary>
        /// 带超时的异步调用
        /// </summary>
        public static async Task<TResult> InvokeAsyncWithTimeout<TService, TResult>(
            this IClientContext context, 
            int timeoutMs, 
            string methodName, 
            params object[] parameters) 
            where TService : class
        {
            var task = context.InvokeAsync<TService, TResult>(methodName, parameters);
            var timeoutTask = Task.Delay(timeoutMs);

            var completedTask = await Task.WhenAny(task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"RPC call '{methodName}' timed out after {timeoutMs}ms");
            }

            return await task;
        }
    }
}

