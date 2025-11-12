# DuckyNet 服务器管理后台

## 功能介绍

基于 **Vue3** 和 **ASP.NET Core** 的 Steam 风格服务器管理后台，提供实时监控和管理功能。

### 主要功能

1. **服务器概览**
   - 在线玩家数量
   - 活跃房间数量
   - NPC 总数
   - 服务器运行状态

2. **房间管理**
   - 查看所有房间列表
   - 查看房间详细信息
   - 查看房间内玩家
   - 房间状态监控（人数、密码、创建时间）

3. **玩家管理**
   - 查看所有在线玩家
   - 查看玩家位置信息
   - 查看玩家所在房间和场景
   - 玩家状态实时监控

4. **场景管理**
   - 查看所有活跃场景
   - 查看场景内的玩家和 NPC
   - NPC 位置和血量信息
   - 场景热度统计

## 技术栈

### 后端
- ASP.NET Core 8.0
- Web API
- Swagger API 文档

### 前端
- Vue 3 (通过 CDN)
- Axios (HTTP 请求)
- Steam 风格 CSS

## 启动服务器

### 1. 还原依赖包
```bash
cd Server
dotnet restore
```

### 2. 运行服务器
```bash
dotnet run
```

### 3. 访问后台
启动成功后，访问：
- **管理后台**: http://localhost:5000
- **API 文档**: http://localhost:5000/swagger (开发模式)
- **RPC 服务器**: 端口 9050

## API 端点

### Dashboard API
- `GET /api/dashboard/overview` - 获取服务器概览

### 房间 API
- `GET /api/rooms` - 获取所有房间
- `GET /api/rooms/{roomId}` - 获取房间详情

### 玩家 API
- `GET /api/players` - 获取所有在线玩家
- `GET /api/players/{steamId}` - 获取玩家详情

### 场景 API
- `GET /api/scenes` - 获取所有场景
- `GET /api/scenes/{sceneName}/{subSceneName}` - 获取场景详情

### NPC API
- `GET /api/npcs` - 获取 NPC 统计
- `GET /api/npcs/{npcId}` - 获取 NPC 详情

## 目录结构

```
Server/Web/
├── Controllers/           # Web API 控制器
│   ├── DashboardController.cs
│   ├── RoomsController.cs
│   ├── PlayersController.cs
│   ├── ScenesController.cs
│   └── NpcsController.cs
├── wwwroot/              # 静态资源
│   ├── index.html        # 主页面
│   ├── css/
│   │   └── steam-style.css
│   └── js/
│       └── app.js
├── WebServerStartup.cs   # Web 服务器配置
└── README.md             # 本文档
```

## 特性

### 自动刷新
- 数据每 5 秒自动刷新
- 服务器时间每秒更新

### Steam 风格界面
- 深色主题
- Steam 蓝色强调色
- 流畅的动画过渡
- 悬停效果

### 响应式设计
- 自适应卡片布局
- 网格系统
- 滚动条样式定制

## 开发说明

### 修改 Web 端口
在 `Program.cs` 中修改：
```csharp
var webTask = _webApp.RunAsync("http://localhost:5000");
```

### 添加新的 API
1. 在 `Controllers/` 目录创建新的控制器
2. 继承 `ControllerBase`
3. 添加 `[ApiController]` 和 `[Route("api/[controller]")]` 特性
4. 通过构造函数注入所需的 Manager

### 修改前端样式
编辑 `wwwroot/css/steam-style.css` 文件

### 添加新功能
1. 在 `app.js` 的 `data()` 中添加响应式数据
2. 在 `methods` 中添加新方法
3. 在 `index.html` 中添加对应的 UI

## 注意事项

1. Web 服务器和 RPC 服务器同时运行
2. 所有 Manager 通过依赖注入共享
3. 数据实时性取决于自动刷新间隔
4. CORS 已启用，允许跨域访问

## 故障排除

### 端口被占用
修改 `Program.cs` 中的 Web 端口号

### 无法访问静态文件
检查 `wwwroot` 目录是否存在且包含所有文件

### API 返回空数据
确保游戏服务器正在运行且有玩家连接

## 未来改进

- [ ] 添加实时 WebSocket 推送
- [ ] 添加玩家踢出功能
- [ ] 添加服务器配置管理
- [ ] 添加日志查看功能
- [ ] 添加性能监控图表
- [ ] 添加用户认证和权限管理

## 许可证

与 DuckyNet 项目保持一致

