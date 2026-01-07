using DuckyNet.Server.Managers;
using DuckyNet.RPC;
using DuckyNet.RPC.Core;
using DuckyNet.Server.Events;
using System;

namespace DuckyNet.Server.Plugin
{
    /// <summary>
    /// 插件上下文实现
    /// 提供插件访问服务器资源的能力
    /// </summary>
    public class PluginContext : IPluginContext
    {
        public PlayerManager PlayerManager { get; }
        public RoomManager RoomManager { get; }
        public RpcServer RpcServer { get; }
        public IServiceProvider ServiceProvider { get; }
        public IEventBus EventBus { get; }
        public IPluginLogger Logger { get; }

        public PluginContext(
            PlayerManager playerManager,
            RoomManager roomManager,
            RpcServer rpcServer,
            IServiceProvider serviceProvider,
            IEventBus eventBus,
            IPluginLogger logger)
        {
            PlayerManager = playerManager;
            RoomManager = roomManager;
            RpcServer = rpcServer;
            ServiceProvider = serviceProvider;
            EventBus = eventBus;
            Logger = logger;
        }
    }
}
