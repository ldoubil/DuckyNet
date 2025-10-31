using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace DuckyNet.Server.RPC
{
    /// <summary>
    /// RpcServer 扩展方法 - 提供强类型的广播调用
    /// </summary>
    public static class RpcServerExtensions
    {
        private static readonly ConcurrentDictionary<Type, object> _broadcastProxyCache = new ConcurrentDictionary<Type, object>();
        private static readonly ConcurrentDictionary<Type, object> _clientsBroadcastProxyCache = new ConcurrentDictionary<Type, object>();
        private static readonly ConcurrentDictionary<Type, object> _whereBroadcastProxyCache = new ConcurrentDictionary<Type, object>();

        /// <summary>
        /// 获取强类型的广播代理（广播到所有客户端）
        /// </summary>
        /// <typeparam name="TService">服务接口类型</typeparam>
        /// <param name="server">RPC服务器</param>
        /// <returns>广播代理实例</returns>
        public static TService Broadcast<TService>(this RpcServer server) where TService : class
        {
            var serviceType = typeof(TService);
            
            return (TService)_broadcastProxyCache.GetOrAdd(serviceType, t =>
            {
                // 根据接口类型创建对应的广播代理
                var proxyTypeName = $"{t.Namespace}.Generated.{t.Name.TrimStart('I')}BroadcastProxy";
                var proxyType = t.Assembly.GetType(proxyTypeName);
                
                if (proxyType == null)
                {
                    throw new InvalidOperationException(
                        $"找不到广播代理类型: {proxyTypeName}。请确保运行了代码生成器。");
                }
                
                return Activator.CreateInstance(proxyType, server)!;
            });
        }

        /// <summary>
        /// 获取强类型的广播代理（广播到指定客户端列表）
        /// </summary>
        /// <typeparam name="TService">服务接口类型</typeparam>
        /// <param name="server">RPC服务器</param>
        /// <param name="clientIds">客户端ID列表</param>
        /// <returns>广播代理实例</returns>
        public static TService BroadcastToClients<TService>(this RpcServer server, IEnumerable<string> clientIds) where TService : class
        {
            var serviceType = typeof(TService);
            
            var proxyBase = _clientsBroadcastProxyCache.GetOrAdd(serviceType, t =>
            {
                // 根据接口类型创建对应的广播代理
                var proxyTypeName = $"{t.Namespace}.Generated.{t.Name.TrimStart('I')}ClientsBroadcastProxy";
                var proxyType = t.Assembly.GetType(proxyTypeName);
                
                if (proxyType == null)
                {
                    throw new InvalidOperationException(
                        $"找不到广播代理类型: {proxyTypeName}。请确保运行了代码生成器。");
                }
                
                return proxyType;
            });

            return (TService)Activator.CreateInstance((Type)proxyBase, server, clientIds)!;
        }

        /// <summary>
        /// 获取强类型的广播代理（使用过滤器）
        /// </summary>
        /// <typeparam name="TService">服务接口类型</typeparam>
        /// <param name="server">RPC服务器</param>
        /// <param name="predicate">客户端过滤器</param>
        /// <returns>广播代理实例</returns>
        public static TService BroadcastWhere<TService>(this RpcServer server, Func<string, bool> predicate) where TService : class
        {
            var serviceType = typeof(TService);
            
            var proxyBase = _whereBroadcastProxyCache.GetOrAdd(serviceType, t =>
            {
                // 根据接口类型创建对应的广播代理
                var proxyTypeName = $"{t.Namespace}.Generated.{t.Name.TrimStart('I')}WhereBroadcastProxy";
                var proxyType = t.Assembly.GetType(proxyTypeName);
                
                if (proxyType == null)
                {
                    throw new InvalidOperationException(
                        $"找不到广播代理类型: {proxyTypeName}。请确保运行了代码生成器。");
                }
                
                return proxyType;
            });

            return (TService)Activator.CreateInstance((Type)proxyBase, server, predicate)!;
        }
    }
}

