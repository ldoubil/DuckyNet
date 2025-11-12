using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DuckyNet.Server.Managers;

namespace DuckyNet.Server.Web
{
    /// <summary>
    /// Web 服务器启动配置
    /// </summary>
    public static class WebServerStartup
    {
        /// <summary>
        /// 配置并启动 Web 服务器
        /// </summary>
        public static WebApplication CreateAndConfigureWebApp(
            IServiceProvider gameServiceProvider,
            string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // 配置 Web 服务
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            
            // 启用 CORS（允许Vue前端访问）
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins("http://localhost:3000", "http://localhost:3001") // Vue开发服务器（支持多端口）
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials(); // 允许WebSocket
                });
            });

            // 从游戏服务提供者中获取单例服务并注入到 Web 服务中
            builder.Services.AddSingleton(gameServiceProvider.GetRequiredService<PlayerManager>());
            builder.Services.AddSingleton(gameServiceProvider.GetRequiredService<RoomManager>());
            builder.Services.AddSingleton(gameServiceProvider.GetRequiredService<SceneManager>());
            builder.Services.AddSingleton(gameServiceProvider.GetRequiredService<PlayerNpcManager>());

            var app = builder.Build();

            // 配置 HTTP 请求管道
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors();
            app.UseRouting();
            app.MapControllers();
            
            // 根路径提示信息
            app.MapGet("/", async context =>
            {
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(@"{
    ""name"": ""DuckyNet Server API"",
    ""version"": ""1.0.0"",
    ""swagger"": ""/swagger"",
    ""frontend"": ""http://localhost:3001 (Vue3 Dev Server)"",
    ""refresh"": ""HTTP Polling (3 seconds)""
}");
            });

            return app;
        }
    }
}

