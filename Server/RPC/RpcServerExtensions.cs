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
                // 修复：只移除第一个 'I' 字符
                var className = (t.Name.StartsWith("I") && t.Name.Length > 1 && char.IsUpper(t.Name[1])) 
                    ? t.Name.Substring(1) 
                    : t.Name;
                var proxyTypeName = $"{t.Namespace}.Generated.{className}BroadcastProxy";
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
                // 修复：只移除第一个 'I' 字符
                var className = (t.Name.StartsWith("I") && t.Name.Length > 1 && char.IsUpper(t.Name[1])) 
                    ? t.Name.Substring(1) 
                    : t.Name;
                var proxyTypeName = $"{t.Namespace}.Generated.{className}ClientsBroadcastProxy";
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
                // 修复：只移除第一个 'I' 字符
                var className = (t.Name.StartsWith("I") && t.Name.Length > 1 && char.IsUpper(t.Name[1])) 
                    ? t.Name.Substring(1) 
                    : t.Name;
                var proxyTypeName = $"{t.Namespace}.Generated.{className}WhereBroadcastProxy";
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

        /// <summary>
        /// 获取强类型的广播代理（广播到房间内所有客户端，可选排除某个客户端）
        /// </summary>
        /// <typeparam name="TService">服务接口类型</typeparam>
        /// <param name="server">RPC服务器</param>
        /// <param name="roomId">房间ID</param>
        /// <param name="exceptClientId">要排除的客户端ID（可选）</param>
        /// <returns>广播代理实例</returns>
        public static TService BroadcastToRoom<TService>(this RpcServer server, string roomId, string? exceptClientId = null) where TService : class
        {
            // 使用 BroadcastWhere 实现房间内广播
            return server.BroadcastWhere<TService>(clientId =>
            {
                // 这里需要通过某种方式获取客户端所在房间
                // 为了简化，我们使用一个静态辅助方法
                var inRoom = RoomBroadcastHelper.IsClientInRoom(clientId, roomId);
                var notExcluded = exceptClientId == null || clientId != exceptClientId;
                return inRoom && notExcluded;
            });
        }
    }

    /// <summary>
    /// 房间广播辅助类（用于 BroadcastToRoom）
    /// </summary>
    public static class RoomBroadcastHelper
    {
        private static Managers.RoomManager? _roomManager;
        private static Managers.PlayerManager? _playerManager;

        /// <summary>
        /// 初始化房间广播辅助类（在服务器启动时调用）
        /// </summary>
        public static void Initialize(Managers.RoomManager roomManager, Managers.PlayerManager playerManager)
        {
            _roomManager = roomManager;
            _playerManager = playerManager;
        }

        /// <summary>
        /// 检查客户端是否在指定房间中
        /// </summary>
        public static bool IsClientInRoom(string clientId, string roomId)
        {
            if (_playerManager == null || _roomManager == null)
            {
                return false;
            }

            var player = _playerManager.GetPlayer(clientId);
            if (player == null)
            {
                return false;
            }

            var playerRoom = _roomManager.GetPlayerRoom(player);
            return playerRoom != null && playerRoom.RoomId == roomId;
        }
    }
}

