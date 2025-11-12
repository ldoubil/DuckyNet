# DuckyNet Web Admin - 前端项目

基于 **Vue 3** + **Vite** 的 DuckyNet 服务器管理后台界面（Steam风格）

## 特性

- 🎨 Steam 风格UI设计
- ⚡️ Vite 快速开发体验
- 🔄 WebSocket 实时数据推送
- 📊 实时服务器状态监控
- 🏠 房间管理
- 👥 玩家管理
- 🗺️ 场景管理
- 🤖 NPC 监控

## 技术栈

- Vue 3 (Composition API)
- Vite
- Axios (HTTP请求)
- WebSocket (实时通信)

## 快速开始

### 1. 安装依赖

```bash
npm install
```

### 2. 启动开发服务器

```bash
npm run dev
```

浏览器访问：http://localhost:3000

### 3. 构建生产版本

```bash
npm run build
```

构建产物在 `dist/` 目录

### 4. 预览生产构建

```bash
npm run preview
```

## 配置

### 环境变量

在 `.env` 文件中配置 API 地址：

```env
VITE_API_BASE_URL=http://localhost:5000
VITE_WS_BASE_URL=ws://localhost:5000
```

生产环境在 `.env.production` 中配置

## 目录结构

```
WebAdmin/
├── src/
│   ├── App.vue              # 主应用组件
│   ├── main.js              # 入口文件
│   ├── assets/              # 静态资源
│   │   └── steam-style.css  # Steam样式
│   └── services/            # 服务层
│       └── api.js           # API和WebSocket
├── index.html               # HTML模板
├── vite.config.js           # Vite配置
├── package.json             # 依赖配置
└── README.md                # 本文档
```

## API端点

### REST API
- `GET /api/dashboard/overview` - 服务器概览
- `GET /api/rooms` - 房间列表
- `GET /api/rooms/{roomId}` - 房间详情
- `GET /api/players` - 玩家列表
- `GET /api/scenes` - 场景列表
- `GET /api/scenes/{sceneName}/{subSceneName}` - 场景详情

### WebSocket
- `ws://localhost:5000/ws` - 实时数据推送

## 开发说明

### 修改端口

在 `vite.config.js` 中修改：

```javascript
server: {
  port: 3000  // 修改为你需要的端口
}
```

### 修改API地址

在 `.env` 文件中修改

### 添加新功能

1. 在 `src/App.vue` 的 `data()` 中添加响应式数据
2. 在 `methods` 中添加方法
3. 在 `template` 中添加UI

## 部署

### 部署到静态服务器

```bash
npm run build
# 将 dist/ 目录部署到 Nginx/Apache 等
```

### Nginx 配置示例

```nginx
server {
    listen 80;
    server_name your-domain.com;
    
    location / {
        root /path/to/dist;
        try_files $uri $uri/ /index.html;
    }
    
    location /api {
        proxy_pass http://localhost:5000;
    }
    
    location /ws {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }
}
```

## 注意事项

1. 确保后端Server正在运行（端口5000）
2. WebSocket需要后端支持
3. 开发模式下Vite会自动代理API请求

## 许可证

与 DuckyNet 项目保持一致

