using DuckyNet.RPC.Core;
using DuckyNet.Server.Managers;
using DuckyNet.Server.Plugin;
using DuckyNet.Server.Services;
using DuckyNet.Shared.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DuckyNet.Server.Plugins.Modules
{
    [DependsOn("CorePlugin")]
    public class SyncModulePlugin : IPlugin
    {
        public string Name => "SyncModule";
        public string Version => "1.0.0";
        public string Author => "DuckyNet";
        public string Description => "同步模块";

        private IPluginContext _context = null!;

        public void OnLoad(IPluginContext context)
        {
            _context = context;
            _context.Logger.Info("SyncModule loaded.");
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<BroadcastManager>();

            services.AddSingleton<PlayerUnitySyncServiceImpl>();
            services.AddSingleton<IPlayerUnitySyncService>(sp => sp.GetRequiredService<PlayerUnitySyncServiceImpl>());

            services.AddSingleton<HealthSyncServiceImpl>();
            services.AddSingleton<IHealthSyncService>(sp => sp.GetRequiredService<HealthSyncServiceImpl>());

            services.AddSingleton<AnimatorSyncServiceImpl>();
            services.AddSingleton<IAnimatorSyncService>(sp => sp.GetRequiredService<AnimatorSyncServiceImpl>());

            services.AddSingleton<ItemSyncServiceImpl>();
            services.AddSingleton<IItemSyncService>(sp => sp.GetRequiredService<ItemSyncServiceImpl>());

            services.AddSingleton<EquipmentServerServiceImpl>();
            services.AddSingleton<IEquipmentService>(sp => sp.GetRequiredService<EquipmentServerServiceImpl>());

            services.AddSingleton<WeaponSyncServerServiceImpl>();
            services.AddSingleton<IWeaponSyncService>(sp => sp.GetRequiredService<WeaponSyncServerServiceImpl>());
        }

        public void ConfigureRpc(RpcServer server)
        {
            var provider = _context.ServiceProvider;
            server.RegisterServerService<IPlayerUnitySyncService>(provider.GetRequiredService<IPlayerUnitySyncService>());
            server.RegisterServerService<IHealthSyncService>(provider.GetRequiredService<IHealthSyncService>());
            server.RegisterServerService<IAnimatorSyncService>(provider.GetRequiredService<IAnimatorSyncService>());
            server.RegisterServerService<IItemSyncService>(provider.GetRequiredService<IItemSyncService>());
            server.RegisterServerService<IEquipmentService>(provider.GetRequiredService<IEquipmentService>());
            server.RegisterServerService<IWeaponSyncService>(provider.GetRequiredService<IWeaponSyncService>());
        }

        public void ConfigureWeb(IEndpointRouteBuilder endpoints)
        {
        }

        public void OnUnload()
        {
        }

        public void OnUpdate()
        {
        }
    }
}
