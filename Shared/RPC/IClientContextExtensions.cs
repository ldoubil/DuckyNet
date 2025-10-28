using System;
using System.Collections.Concurrent;

namespace DuckyNet.Shared.RPC
{
    /// <summary>
    /// IClientContext 扩展方法 - 提供强类型的客户端调用
    /// </summary>
    public static class IClientContextExtensions
    {
        private static readonly ConcurrentDictionary<string, object> _clientProxyCache = new ConcurrentDictionary<string, object>();

        /// <summary>
        /// 获取强类型的客户端调用代理
        /// </summary>
        /// <typeparam name="TService">服务接口类型</typeparam>
        /// <param name="context">客户端上下文</param>
        /// <returns>客户端调用代理实例</returns>
        public static TService Call<TService>(this IClientContext context) where TService : class
        {
            var serviceType = typeof(TService);
            var cacheKey = $"{context.ClientId}_{serviceType.FullName}";
            
            return (TService)_clientProxyCache.GetOrAdd(cacheKey, key =>
            {
                // 根据接口类型创建对应的客户端调用代理
                var proxyTypeName = $"{serviceType.Namespace}.Generated.{serviceType.Name.TrimStart('I')}ClientCallProxy";
                var proxyType = serviceType.Assembly.GetType(proxyTypeName);
                
                if (proxyType == null)
                {
                    throw new InvalidOperationException(
                        $"找不到客户端调用代理类型: {proxyTypeName}。请确保运行了代码生成器。");
                }
                
                return Activator.CreateInstance(proxyType, context)!;
            });
        }
    }
}

