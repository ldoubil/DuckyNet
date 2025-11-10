using Microsoft.Extensions.DependencyInjection;
using DuckyNet.Server.RPC;
using DuckyNet.Server.Events;
using DuckyNet.Server.Managers;
using DuckyNet.Server.Services;
using DuckyNet.Server.Plugin;
using DuckyNet.Shared.Services;
using DuckyNet.Shared.RPC;

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
            // 管理器
            services.AddSingleton<RoomManager>();
            services.AddSingleton<PlayerManager>();

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
        /// 注册所有 RPC 服务到 RpcServer
        /// </summary>
        public static void RegisterRpcServices(IServiceProvider serviceProvider)
        {
            var server = serviceProvider.GetRequiredService<RpcServer>();

            // 注册所有服务到 RPC 服务器
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
        }
    }
}

