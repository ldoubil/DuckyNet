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
    public class NpcModulePlugin : IPlugin
    {
        public string Name => "NpcModule";
        public string Version => "1.0.0";
        public string Author => "DuckyNet";
        public string Description => "NPC 模块";

        private IPluginContext _context = null!;

        public void OnLoad(IPluginContext context)
        {
            _context = context;
            _context.Logger.Info("NpcModule loaded.");
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<PlayerNpcManager>();

            services.AddSingleton<NpcVisibilityTracker>(sp =>
            {
                return new NpcVisibilityTracker
                {
                    SyncRange = 100f
                };
            });

            services.AddSingleton<NpcSyncServiceImpl>();
            services.AddSingleton<INpcSyncService>(sp => sp.GetRequiredService<NpcSyncServiceImpl>());
        }

        public void ConfigureRpc(RpcServer server)
        {
            var provider = _context.ServiceProvider;
            server.RegisterServerService<INpcSyncService>(provider.GetRequiredService<INpcSyncService>());
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
