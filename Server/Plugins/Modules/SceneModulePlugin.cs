using DuckyNet.RPC.Core;
using DuckyNet.Server.Managers;
using DuckyNet.Server.Plugin;
using DuckyNet.Server.Services;
using DuckyNet.Shared.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DuckyNet.Server.Plugins.Modules
{
    public class SceneModulePlugin : IPlugin
    {
        public string Name => "SceneModule";
        public string Version => "1.0.0";
        public string Author => "DuckyNet";
        public string Description => "场景模块";

        private IPluginContext _context = null!;

        public void OnLoad(IPluginContext context)
        {
            _context = context;
            _context.Logger.Info("SceneModule loaded.");
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<SceneManager>();

            services.AddSingleton<SceneServiceImpl>();
            services.AddSingleton<ISceneService>(sp => sp.GetRequiredService<SceneServiceImpl>());
        }

        public void ConfigureRpc(RpcServer server)
        {
            var provider = _context.ServiceProvider;
            server.RegisterServerService<ISceneService>(provider.GetRequiredService<ISceneService>());
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
