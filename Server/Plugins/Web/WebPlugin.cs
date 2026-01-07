using System;
using DuckyNet.RPC.Core;
using DuckyNet.Server.Managers;
using DuckyNet.Server.Plugin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DuckyNet.Server.Plugins.Web
{
    public class WebPlugin : IPlugin
    {
        public string Name => "WebPlugin";
        public string Version => "1.0.0";
        public string Author => "DuckyNet";
        public string Description => "后台管理 Web 模块";

        private IPluginContext _context = null!;

        public void OnLoad(IPluginContext context)
        {
            _context = context;
            _context.Logger.Info("WebPlugin loaded.");
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen();

            services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins("http://localhost:3000", "http://localhost:3001")
                        .AllowAnyMethod()
                        .AllowAnyHeader()
                        .AllowCredentials();
                });
            });

            var provider = _context.ServiceProvider;
            services.AddSingleton(provider.GetRequiredService<PlayerManager>());
            services.AddSingleton(provider.GetRequiredService<RoomManager>());
            services.AddSingleton(provider.GetRequiredService<SceneManager>());
            services.AddSingleton(provider.GetRequiredService<PlayerNpcManager>());
        }

        public void ConfigureRpc(RpcServer server)
        {
        }

        public void ConfigureWeb(IEndpointRouteBuilder endpoints)
        {
            if (endpoints is not WebApplication app)
            {
                return;
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors();
            app.UseRouting();
            app.MapControllers();

            app.MapGet("/", async context =>
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(@"{
    \"name\": \"DuckyNet Server API\",
    \"version\": \"1.0.0\",
    \"swagger\": \"/swagger\",
    \"frontend\": \"http://localhost:3001 (Vue3 Dev Server)\",
    \"refresh\": \"HTTP Polling (3 seconds)\"
}");
            });
        }

        public void OnUnload()
        {
        }

        public void OnUpdate()
        {
        }
    }
}
