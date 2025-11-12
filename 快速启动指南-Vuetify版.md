# 🚀 DuckyNet Web管理后台 - 快速启动指南

## 📋 重要更新

### ✨ 新版本特性
- ✅ **Vuetify Material Design** - 无需手写CSS
- ✅ **NPC位置2D可视化** - Canvas实时绘制
- ✅ **强健的监控API** - 性能/分布/统计
- ✅ **实时WebSocket推送** - 2秒自动更新

## 🎯 第一次使用

### Step 1: 安装前端依赖 ⭐

```bash
cd WebAdmin
npm install
```

**预计时间**: 2-3分钟
**包大小**: 约150MB

### Step 2: 启动系统

#### 方式A：一键启动（推荐）
```bash
双击: 启动完整系统.bat
```

#### 方式B：手动启动
```bash
# 终端1 - 启动后端
cd Server
dotnet run

# 终端2 - 启动前端
cd WebAdmin
npm run dev
```

### Step 3: 访问界面

打开浏览器访问：**http://localhost:3000**

## 🎨 界面功能

### 1. 总览面板
- 实时统计卡片
- 在线玩家/房间/NPC数量

### 2. 房间管理
- 数据表格（可排序）
- 点击查看详情

### 3. 玩家列表
- 所有在线玩家
- 位置信息

### 4. 场景监控⭐
- 左侧：场景列表
- 右侧：**2D可视化地图**
  - 🔵 玩家（三角形）
  - 🟠 NPC（圆圈+血量条）
  - 可缩放/拖拽
  - 点击实体聚焦

### 5. NPC管理
- NPC数据表格
- 血量进度条

### 6. 性能监控
- 服务器统计
- WebSocket连接数

## 🎮 地图控制

### NPC位置可视化地图
- **滚轮** - 缩放
- **拖拽** - 平移
- **+/-按钮** - 缩放
- **刷新按钮** - 重置
- **点击实体** - 聚焦

## 🔌 实时功能

### WebSocket状态
- 🟢 **实时连接** - 数据自动更新
- 🔴 **离线** - 降级到手动刷新

### 自动推送（每2秒）
- ✅ 概览数据
- ✅ 房间列表
- ✅ 玩家列表
- ✅ WebSocket客户端数

## 📡 访问地址

| 服务 | 地址 |
|------|------|
| 前端界面 | http://localhost:3000 |
| 后端API | http://localhost:5000 |
| Swagger文档 | http://localhost:5000/swagger |
| WebSocket | ws://localhost:5000/ws |

## 🔧 后续启动

### 第二次及以后

```bash
# 直接双击
启动完整系统.bat
```

或者

```bash
cd WebAdmin
npm run dev
```

不需要再次 `npm install`！

## ❓ 常见问题

### Q: 前端启动失败
```bash
# 删除重装
cd WebAdmin
rm -rf node_modules
npm install
```

### Q: 端口被占用
**前端**: 修改 `vite.config.js` 中的 `port: 3000`
**后端**: 修改 `Program.cs` 中的端口

### Q: NPC地图不显示
- 确保场景中有NPC
- 检查控制台错误

### Q: WebSocket断开
- 确保后端正在运行
- 检查CORS配置

## 📊 监控API

### 新增强大API

```bash
# 性能统计
GET /api/monitor/performance

# 玩家分布
GET /api/monitor/player-distribution

# NPC统计
GET /api/monitor/npc-stats

# 热门场景
GET /api/monitor/hot-scenes

# 健康检查
GET /api/monitor/health
```

## 🎨 Vuetify特性

### UI组件
- 数据表格（带分页/排序）
- 卡片布局
- 导航抽屉
- 图标系统（MDI）
- 进度条
- 标签
- 选项卡

### 主题
- 深色主题
- Material Design
- 响应式布局

## 🔥 性能特点

- ⚡ Vite极速热重载
- 🌊 WebSocket实时推送
- 🎨 Canvas硬件加速
- 📊 虚拟滚动（大数据）
- 🔄 自动重连机制

## 📝 开发说明

### 修改前端
```bash
cd WebAdmin
npm run dev
# 修改 src/ 下的文件，自动热重载
```

### 修改后端
```bash
cd Server
# 修改 Web/ 下的文件
dotnet run
```

## 🎯 测试清单

- [ ] 前端启动成功（http://localhost:3000）
- [ ] WebSocket连接成功（🟢 实时连接）
- [ ] 总览数据显示正常
- [ ] 房间列表可以点击查看
- [ ] 玩家列表显示位置
- [ ] NPC地图可以缩放拖拽
- [ ] Swagger文档可访问

## 📚 文档

- `使用Vuetify重构完成.md` - 完整技术文档
- `README_WEB_ADMIN.md` - 系统说明
- `WebAdmin/README.md` - 前端文档

## 🎉 就这么简单！

1. `cd WebAdmin && npm install` （首次）
2. 双击 `启动完整系统.bat`
3. 访问 http://localhost:3000
4. 开始监控你的服务器！

---

🦆 **DuckyNet - 强大的游戏服务器管理后台**

