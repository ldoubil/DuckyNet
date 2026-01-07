using DuckyNet.RPC.Core;
using DuckyNet.Server.Events.Handlers;
using DuckyNet.Server.Managers;
using DuckyNet.Server.Plugin;
using DuckyNet.Server.Services;
using DuckyNet.Shared.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DuckyNet.Server.Plugins.Modules
{
    public class PlayerModulePlugin : IPlugin
    {
        public string Name => "PlayerModule";
        public string Version => "1.0.0";
        public string Author => "DuckyNet";
        public string Description => "玩家与角色模块";

        private IPluginContext _context = null!;

        public void OnLoad(IPluginContext context)
        {
            _context = context;
            _context.Logger.Info("PlayerModule loaded.");
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<PlayerManager>();
            services.AddSingleton<PlayerCleanupHandler>();

            services.AddSingleton<CharacterServiceImpl>();
            services.AddSingleton<ICharacterService>(sp => sp.GetRequiredService<CharacterServiceImpl>());

            services.AddSingleton<PlayerServiceImpl>();
            services.AddSingleton<IPlayerService>(sp => sp.GetRequiredService<PlayerServiceImpl>());

            services.AddSingleton<CharacterAppearanceServiceImpl>();
            services.AddSingleton<ICharacterAppearanceService>(sp => sp.GetRequiredService<CharacterAppearanceServiceImpl>());
        }

        public void ConfigureRpc(RpcServer server)
        {
            var provider = _context.ServiceProvider;
            server.RegisterServerService<IPlayerService>(provider.GetRequiredService<IPlayerService>());
            server.RegisterServerService<ICharacterService>(provider.GetRequiredService<ICharacterService>());
            server.RegisterServerService<ICharacterAppearanceService>(provider.GetRequiredService<ICharacterAppearanceService>());
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
