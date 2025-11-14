using Microsoft.Extensions.DependencyInjection;
using DuckyNet.RPC;
using DuckyNet.RPC.Core;
using DuckyNet.RPC.Utils;
using DuckyNet.Server.Events;
using DuckyNet.Server.Managers;
using DuckyNet.Server.Services;
using DuckyNet.Server.Plugin;
using DuckyNet.Shared.Services;

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
        /// 添加 DuckyNet 业务模块（当前保持原有结构）
        /// TODO(REFACTOR): 阶段3会拆分为独立模块
        /// </summary>
        public static IServiceCollection AddDuckyNetModules(this IServiceCollection services)
        {
            // 核心管理器（按依赖顺序）
            services.AddSingleton<PlayerManager>();
            services.AddSingleton<RoomManager>();
            services.AddSingleton<SceneManager>();
            
            // 事件系统
            services.AddSingleton<Server.Events.EventBus>();
            services.AddSingleton<Server.Events.IEventBus>(sp => sp.GetRequiredService<Server.Events.EventBus>());
            
            // 玩家清理事件处理器
            services.AddSingleton<Server.Events.Handlers.PlayerCleanupHandler>();
            
            services.AddSingleton<BroadcastManager>();
            
            // 🔥 NPC 管理（改用 PlayerNpcManager）
            services.AddSingleton<PlayerNpcManager>();
            
            // NPC 可见性追踪器（单例，需要在 NpcSyncServiceImpl 之前）
            services.AddSingleton<NpcVisibilityTracker>(sp =>
            {
                return new NpcVisibilityTracker
                {
                    SyncRange = 100f
                };
            });

            // 服务实现（按依赖顺序注册）
            // 注意：SceneService 需要在 CharacterService 之前注册
            services.AddSingleton<PlayerUnitySyncServiceImpl>();
            services.AddSingleton<IPlayerUnitySyncService>(sp => sp.GetRequiredService<PlayerUnitySyncServiceImpl>());

            services.AddSingleton<SceneServiceImpl>();
            services.AddSingleton<ISceneService>(sp => sp.GetRequiredService<SceneServiceImpl>());

            services.AddSingleton<CharacterServiceImpl>();
            services.AddSingleton<ICharacterService>(sp => sp.GetRequiredService<CharacterServiceImpl>());

            services.AddSingleton<PlayerServiceImpl>();
            services.AddSingleton<IPlayerService>(sp => sp.GetRequiredService<PlayerServiceImpl>());

            services.AddSingleton<HealthSyncServiceImpl>();
            services.AddSingleton<IHealthSyncService>(sp => sp.GetRequiredService<HealthSyncServiceImpl>());

            services.AddSingleton<CharacterAppearanceServiceImpl>();
            services.AddSingleton<ICharacterAppearanceService>(sp => sp.GetRequiredService<CharacterAppearanceServiceImpl>());

            services.AddSingleton<AnimatorSyncServiceImpl>();
            services.AddSingleton<IAnimatorSyncService>(sp => sp.GetRequiredService<AnimatorSyncServiceImpl>());

            services.AddSingleton<ItemSyncServiceImpl>();
            services.AddSingleton<IItemSyncService>(sp => sp.GetRequiredService<ItemSyncServiceImpl>());

            services.AddSingleton<EquipmentServerServiceImpl>();
            services.AddSingleton<IEquipmentService>(sp => sp.GetRequiredService<EquipmentServerServiceImpl>());

            services.AddSingleton<WeaponSyncServerServiceImpl>();
            services.AddSingleton<IWeaponSyncService>(sp => sp.GetRequiredService<WeaponSyncServerServiceImpl>());

            // NPC 同步服务
            services.AddSingleton<NpcSyncServiceImpl>();
            services.AddSingleton<INpcSyncService>(sp => sp.GetRequiredService<NpcSyncServiceImpl>());

            // RoomService 最后注册（依赖装备和武器服务）
            services.AddSingleton<RoomServiceImpl>();
            services.AddSingleton<IRoomService>(sp => sp.GetRequiredService<RoomServiceImpl>());


            return services;
        }

        /// <summary>
        /// 添加插件系统
        /// </summary>
        public static IServiceCollection AddPluginSystem(this IServiceCollection services)
        {
            services.AddSingleton<PluginManager>(sp =>
            {
                var context = new PluginContext(
                    sp.GetRequiredService<PlayerManager>(),
                    sp.GetRequiredService<RoomManager>(),
                    sp.GetRequiredService<RpcServer>(),
                    sp.GetRequiredService<EventBus>(),
                    new PluginLogger("System")
                );
                return new PluginManager(context);
            });

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

            // 2. 注册所有服务到 RPC 服务器
            var server = ServerContext.Server;
            server.RegisterServerService<IPlayerService>(
                serviceProvider.GetRequiredService<IPlayerService>());
            server.RegisterServerService<IRoomService>(
                serviceProvider.GetRequiredService<IRoomService>());
            server.RegisterServerService<ISceneService>(
                serviceProvider.GetRequiredService<ISceneService>());
            server.RegisterServerService<ICharacterService>(
                serviceProvider.GetRequiredService<ICharacterService>());
            server.RegisterServerService<IPlayerUnitySyncService>(
                serviceProvider.GetRequiredService<IPlayerUnitySyncService>());
            server.RegisterServerService<IHealthSyncService>(
                serviceProvider.GetRequiredService<IHealthSyncService>());
            server.RegisterServerService<ICharacterAppearanceService>(
                serviceProvider.GetRequiredService<ICharacterAppearanceService>());
            server.RegisterServerService<IAnimatorSyncService>(
                serviceProvider.GetRequiredService<IAnimatorSyncService>());
            server.RegisterServerService<IItemSyncService>(
                serviceProvider.GetRequiredService<IItemSyncService>());
            server.RegisterServerService<IEquipmentService>(
                serviceProvider.GetRequiredService<IEquipmentService>());
            server.RegisterServerService<IWeaponSyncService>(
                serviceProvider.GetRequiredService<IWeaponSyncService>());
            server.RegisterServerService<INpcSyncService>(
                serviceProvider.GetRequiredService<INpcSyncService>());
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

