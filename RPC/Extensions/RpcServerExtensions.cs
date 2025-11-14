using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace DuckyNet.RPC.Extensions
{
    /// <summary>
    /// RpcServer 扩展方法 - 提供强类型的发送调用（使用过滤器）
    /// </summary>
    public static class RpcServerExtensions
    {
        private static readonly ConcurrentDictionary<Type, object> _sendProxyCache = new ConcurrentDictionary<Type, object>();

        /// <summary>
        /// 发送消息给满足条件的客户端（使用过滤器）
        /// 支持多种过滤器方式：
        /// - server.SendTo&lt;TService&gt;(clientId => clientId == "xxx") - 单个客户端
        /// - server.SendTo&lt;TService&gt;(clientId => clientIds.Contains(clientId)) - 客户端列表
        /// - server.SendTo&lt;TService&gt;(clientId => true) - 所有客户端
        /// </summary>
        /// <typeparam name="TService">服务接口类型</typeparam>
        /// <param name="server">RPC服务器</param>
        /// <param name="predicate">客户端过滤器函数</param>
        /// <returns>发送代理实例</returns>
        public static TService SendTo<TService>(this Core.RpcServer server, Func<string, bool> predicate) where TService : class
        {
            var serviceType = typeof(TService);
            
            var proxyBase = _sendProxyCache.GetOrAdd(serviceType, t =>
            {
                // 根据接口类型创建对应的发送代理
                var className = (t.Name.StartsWith("I") && t.Name.Length > 1 && char.IsUpper(t.Name[1])) 
                    ? t.Name.Substring(1) + "SendProxy"
                    : t.Name + "SendProxy";
                var proxyTypeName = $"{t.Namespace}.Generated.{className}";
                var proxyType = t.Assembly.GetType(proxyTypeName);
                
                if (proxyType == null)
                {
                    throw new InvalidOperationException(
                        $"找不到发送代理类型: {proxyTypeName}。请确保运行了代码生成器。");
                }
                
                return proxyType;
            });

            return (TService)Activator.CreateInstance((Type)proxyBase, server, predicate)!;
        }

        /// <summary>
        /// 发送消息给指定客户端列表（便捷方法，内部转换为过滤器）
        /// </summary>
        /// <typeparam name="TService">服务接口类型</typeparam>
        /// <param name="server">RPC服务器</param>
        /// <param name="clientIds">客户端ID列表</param>
        /// <returns>发送代理实例</returns>
        public static TService SendTo<TService>(this Core.RpcServer server, IEnumerable<string> clientIds) where TService : class
        {
            var clientIdSet = clientIds.ToHashSet();
            return server.SendTo<TService>(clientId => clientIdSet.Contains(clientId));
        }

        /// <summary>
        /// 发送消息给所有客户端（便捷方法）
        /// </summary>
        /// <typeparam name="TService">服务接口类型</typeparam>
        /// <param name="server">RPC服务器</param>
        /// <returns>发送代理实例</returns>
        public static TService Broadcast<TService>(this Core.RpcServer server) where TService : class
        {
            return server.SendTo<TService>(_ => true);
        }
    }
}

