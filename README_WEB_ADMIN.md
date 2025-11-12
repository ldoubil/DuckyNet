# DuckyNet Web 后台管理系统

## 📁 项目结构（前后端分离）

```
DuckyNet/
├── Server/                    # 后端服务器
│   ├── Web/
│   │   ├── Controllers/      # REST API 控制器
│   │   └── Services/         # WebSocket 服务
│   └── Program.cs
├── WebAdmin/                  # 前端项目（Vue3 + Vite）
│   ├── src/
│   │   ├── App.vue
│   │   ├── main.js
│   │   ├── assets/
│   │   └── services/
│   ├── package.json
│   └── vite.config.js
└── 启动完整系统.bat           # 一键启动脚本
```

## 🚀 快速启动

### 方式1：一键启动（推荐）

双击 `启动完整系统.bat`，将自动启动：
- 后端 Server（RPC + Web API）
- 前端 Web Admin（Vue3）

### 方式2：分别启动

#### 1. 启动后端 Server

```bash
cd Server
dotnet run
```

后端服务：
- REST API: http://localhost:5000
- WebSocket: ws://localhost:5000/ws
- Swagger: http://localhost:5000/swagger
- RPC: 端口 9050

#### 2. 启动前端 WebAdmin

```bash
cd WebAdmin
npm install  # 首次运行
npm run dev
```

前端访问：http://localhost:3000

## ✨ 功能特性

### 前端（WebAdmin）
- ✅ Vue 3 + Vite（快速热重载）
- ✅ Steam 风格 UI
- ✅ WebSocket 实时数据推送
- ✅ HTTP REST API 备份
- ✅ 自动重连机制

### 后端（Server）
- ✅ REST API（房间、玩家、场景、NPC）
- ✅ WebSocket 实时广播
- ✅ CORS 跨域支持
- ✅ Swagger API 文档
- ✅ 与游戏 RPC 服务器并行运行

## 📡 通信方式

### 1. REST API

```javascript
// 获取服务器概览
GET /api/dashboard/overview

// 获取房间列表
GET /api/rooms

// 获取玩家列表
GET /api/players

// 获取场景列表
GET /api/scenes
```

### 2. WebSocket

```javascript
// 连接
ws://localhost:5000/ws

// 自动接收实时数据
{
  "type": "overview",
  "data": {
    "onlinePlayers": 5,
    "totalRooms": 2,
    "totalNpcs": 10
  }
}
```

## 🔧 配置

### 前端环境变量（WebAdmin）

创建 `WebAdmin/.env`：

```env
VITE_API_BASE_URL=http://localhost:5000
VITE_WS_BASE_URL=ws://localhost:5000
```

### 后端端口修改（Server）

在 `Server/Program.cs` 中修改：

```csharp
var webTask = _webApp.RunAsync("http://localhost:5000");
```

### 前端端口修改（WebAdmin）

在 `WebAdmin/vite.config.js` 中修改：

```javascript
server: {
  port: 3000
}
```

## 📦 生产部署

### 1. 构建前端

```bash
cd WebAdmin
npm run build
```

生成的 `dist/` 目录部署到 Nginx/Apache

### 2. Nginx 配置示例

```nginx
server {
    listen 80;
    server_name your-domain.com;
    
    # 前端静态文件
    location / {
        root /path/to/WebAdmin/dist;
        try_files $uri $uri/ /index.html;
    }
    
    # 后端 API 代理
    location /api {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
    }
    
    # WebSocket 代理
    location /ws {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
    }
}
```

### 3. 运行后端

```bash
cd Server
dotnet publish -c Release
cd bin/Release/net8.0/publish
./DuckyNet.Server
```

## 🎨 界面预览

- **总览面板**：实时统计（玩家、房间、NPC）
- **房间管理**：查看所有房间和房间内玩家
- **玩家管理**：查看在线玩家和位置信息
- **场景管理**：查看场景内玩家和 NPC

## 🔄 实时推送

WebSocket 每 2 秒自动推送服务器状态：
- 在线玩家数
- 活跃房间数
- NPC 总数
- 服务器时间

前端会显示实时连接状态：
- 🟢 实时 - WebSocket 已连接
- 🔴 离线 - WebSocket 断开（降级到轮询）

## 📝 开发说明

### 添加新的 API

#### 1. 后端（Server）

创建控制器 `Server/Web/Controllers/YourController.cs`：

```csharp
[ApiController]
[Route("api/[controller]")]
public class YourController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { message = "Hello" });
    }
}
```

#### 2. 前端（WebAdmin）

在 `WebAdmin/src/services/api.js` 添加：

```javascript
export const api = {
  // ... 现有方法
  
  getYourData: () => http.get('/api/your')
}
```

在 `App.vue` 中调用：

```javascript
const data = await api.getYourData()
```

## ❓ 常见问题

### Q: WebSocket 无法连接
A: 确保后端 Server 正在运行，检查 CORS 配置

### Q: 前端编译错误
A: 删除 `node_modules` 重新安装：`npm install`

### Q: 后端编译错误
A: 运行 `dotnet clean && dotnet restore && dotnet build`

### Q: 端口被占用
A: 修改配置文件中的端口号

## 📚 技术栈

### 前端
- Vue 3（Composition API）
- Vite 5
- Axios
- WebSocket API

### 后端
- ASP.NET Core 8.0
- Web API
- WebSocket
- Swagger

## 🎯 下一步

- [ ] 添加用户认证
- [ ] 添加操作权限
- [ ] 添加操作日志
- [ ] 添加图表统计
- [ ] 添加踢人功能
- [ ] 添加服务器配置管理

## 许可证

与 DuckyNet 项目保持一致

