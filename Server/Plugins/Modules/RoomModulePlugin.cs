using DuckyNet.RPC.Core;
using DuckyNet.Server.Managers;
using DuckyNet.Server.Plugin;
using DuckyNet.Server.Services;
using DuckyNet.Shared.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DuckyNet.Server.Plugins.Modules
{
    public class RoomModulePlugin : IPlugin
    {
        public string Name => "RoomModule";
        public string Version => "1.0.0";
        public string Author => "DuckyNet";
        public string Description => "房间模块";

        private IPluginContext _context = null!;

        public void OnLoad(IPluginContext context)
        {
            _context = context;
            _context.Logger.Info("RoomModule loaded.");
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<RoomManager>();

            services.AddSingleton<RoomServiceImpl>();
            services.AddSingleton<IRoomService>(sp => sp.GetRequiredService<RoomServiceImpl>());
        }

        public void ConfigureRpc(RpcServer server)
        {
            var provider = _context.ServiceProvider;
            server.RegisterServerService<IRoomService>(provider.GetRequiredService<IRoomService>());
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
