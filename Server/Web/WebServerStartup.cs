using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using DuckyNet.Server.Managers;
using DuckyNet.Server.Web.Services;

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
                    policy.WithOrigins("http://localhost:3000") // Vue开发服务器
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
            
            // 注册 WebSocket 处理器
            builder.Services.AddSingleton<WebSocketHandler>();

            var app = builder.Build();

            // 配置 HTTP 请求管道
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseCors();
            
            // 启用 WebSocket
            var webSocketOptions = new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromMinutes(2)
            };
            app.UseWebSockets(webSocketOptions);
            
            // WebSocket 端点
            var wsHandler = app.Services.GetRequiredService<WebSocketHandler>();
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/ws")
                {
                    await wsHandler.HandleAsync(context);
                }
                else
                {
                    await next();
                }
            });
            
            // 启动 WebSocket 广播服务
            wsHandler.Start();

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
    ""websocket"": ""ws://localhost:5000/ws"",
    ""frontend"": ""http://localhost:3000 (Vue3 Dev Server)""
}");
            });

            return app;
        }
    }
}

