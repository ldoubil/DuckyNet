using Microsoft.Extensions.DependencyInjection;
using DuckyNet.RPC;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Utils;
using DuckyNet.Server.Events;
using DuckyNet.Server.Managers;

namespace DuckyNet.Server.Core
{
    /// <summary>
    /// 服务集合扩展方法 - 配置依赖注入容器
    /// [REFACTOR] 阶段1：引入 DI 容器
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加 DuckyNet 核心服务（底层框架）
        /// </summary>
        public static IServiceCollection AddDuckyNetCore(this IServiceCollection services)
        {
            // 核心网络层
            services.AddSingleton<RpcServer>(sp =>
            {
                var config = RpcConfig.Development;
                return new RpcServer(config);
            });

            // 事件总线
            services.AddSingleton<EventBus>();
            services.AddSingleton<IEventBus>(sp => sp.GetRequiredService<EventBus>());

            return services;
        }

        /// <summary>
        /// 初始化服务器上下文并注册所有 RPC 服务
        /// </summary>
        public static void InitializeServer(IServiceProvider serviceProvider)
        {
            // 1. 初始化全局上下文
            var broadcastManager = serviceProvider.GetRequiredService<BroadcastManager>();
            
            // 初始化事件处理器
            var playerCleanupHandler = serviceProvider.GetRequiredService<Server.Events.Handlers.PlayerCleanupHandler>();
            
            ServerContext.Initialize(
                serviceProvider.GetRequiredService<RpcServer>(),
                serviceProvider.GetRequiredService<PlayerManager>(),
                serviceProvider.GetRequiredService<RoomManager>(),
                serviceProvider.GetRequiredService<SceneManager>(),
                broadcastManager,
                serviceProvider.GetRequiredService<EventBus>()
            );

        }
        
        /// <summary>
        /// [已过时] 使用 InitializeServer 代替
        /// </summary>
        [Obsolete("使用 InitializeServer 代替")]
        public static void RegisterRpcServices(IServiceProvider serviceProvider)
        {
            InitializeServer(serviceProvider);
        }
    }
}
