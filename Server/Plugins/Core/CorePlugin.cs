using DuckyNet.RPC.Core;
using DuckyNet.Server.Core;
using DuckyNet.Server.Plugin;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DuckyNet.Server.Plugins.Core
{
    public class CorePlugin : IPlugin
    {
        public string Name => "CorePlugin";
        public string Version => "1.0.0";
        public string Author => "DuckyNet";
        public string Description => "核心基础服务插件";

        public void OnLoad(IPluginContext context)
        {
            context.Logger.Info("CorePlugin loaded.");
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDuckyNetCore();
        }

        public void ConfigureRpc(RpcServer server)
        {
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
