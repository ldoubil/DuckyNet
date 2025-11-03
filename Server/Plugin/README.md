# DuckyNet 插件系统

## 🎯 设计理念

DuckyNet 服务器采用基于 **C# + 事件总线** 的插件架构：

- ✅ **原生性能**：C# 插件以 DLL 形式加载，性能接近原生代码
- ✅ **类型安全**：完整的 IntelliSense 支持和编译时检查
- ✅ **依赖注入**：通过 `IPluginContext` 访问服务器资源
- ✅ **事件驱动**：基于发布-订阅模式的松耦合架构
- ✅ **隔离性**：插件异常不会导致服务器崩溃

## 🏗️ 架构图

```
┌─────────────────────────────────────────┐
│         插件 (YourPlugin.dll)           │
│  implements IPlugin                     │
└─────────────────────────────────────────┘
              ↓ 依赖注入
┌─────────────────────────────────────────┐
│      IPluginContext (上下文)            │
│  - PlayerManager (玩家管理)             │
│  - RoomManager (房间管理)               │
│  - RpcServer (RPC 服务器)               │
│  - EventBus (事件总线) ←──────┐        │
│  - Logger (日志系统)           │        │
└─────────────────────────────────────────┘
              ↓ 订阅                      │
┌─────────────────────────────────────────┤
│         事件总线 (EventBus)             │
│  - PlayerLoginEvent          ←──────────┤
│  - RoomCreatedEvent                    │
│  - ...更多事件                          │
└─────────────────────────────────────────┘
              ↑ 发布
┌─────────────────────────────────────────┐
│      服务层 (Services)                  │
│  - PlayerServiceImpl                    │
│  - RoomServiceImpl                      │
└─────────────────────────────────────────┘
```

## 📦 核心组件

### 1. IPlugin（插件接口）

所有插件必须实现此接口：

```csharp
public interface IPlugin
{
    string Name { get; }          // 插件名称
    string Version { get; }       // 版本号
    string Author { get; }        // 作者
    string Description { get; }   // 描述
    
    void OnLoad(IPluginContext context);    // 加载时调用
    void OnUnload();                        // 卸载时调用
    void OnUpdate();                        // 每帧更新（约 60 FPS）
}
```

### 2. IPluginContext（上下文接口）

提供插件访问服务器资源的能力：

```csharp
public interface IPluginContext
{
    PlayerManager PlayerManager { get; }    // 玩家管理器
    RoomManager RoomManager { get; }        // 房间管理器
    RpcServer RpcServer { get; }            // RPC 服务器
    IEventBus EventBus { get; }             // 事件总线
    IPluginLogger Logger { get; }           // 日志系统
}
```

### 3. IEventBus（事件总线）

发布-订阅模式的事件系统：

```csharp
public interface IEventBus
{
    void Subscribe<TEvent>(Action<TEvent> handler);     // 订阅事件
    void Unsubscribe<TEvent>(Action<TEvent> handler);   // 取消订阅
    void Publish<TEvent>(TEvent eventData);             // 发布事件
}
```

## 🎪 可用事件

### 服务器事件

```csharp
// 服务器启动
public class ServerStartedEvent
{
    public int Port { get; set; }
}

// 服务器关闭
public class ServerStoppingEvent { }
```

### 玩家事件

```csharp
// 玩家连接（未登录）
public class PlayerConnectedEvent
{
    public string ClientId { get; set; }
}

// 玩家登录成功
public class PlayerLoginEvent
{
    public string ClientId { get; set; }
    public PlayerInfo Player { get; set; }
}

// 玩家登出
public class PlayerLogoutEvent
{
    public string ClientId { get; set; }
    public PlayerInfo Player { get; set; }
}

// 玩家断开连接
public class PlayerDisconnectedEvent
{
    public string ClientId { get; set; }
    public PlayerInfo? Player { get; set; }  // 可能未登录
}
```

### 房间事件

```csharp
// 房间创建
public class RoomCreatedEvent
{
    public RoomInfo Room { get; set; }
    public PlayerInfo Host { get; set; }
}

// 玩家加入房间
public class PlayerJoinedRoomEvent
{
    public RoomInfo Room { get; set; }
    public PlayerInfo Player { get; set; }
}

// 玩家离开房间
public class PlayerLeftRoomEvent
{
    public RoomInfo? Room { get; set; }  // 可能已删除
    public PlayerInfo Player { get; set; }
}

// 房间删除
public class RoomDeletedEvent
{
    public string RoomId { get; set; }
}
```

## 🚀 快速开始

### 1. 创建插件项目

```bash
dotnet new classlib -n MyPlugin -f net8.0
cd MyPlugin
dotnet add reference ../Server/DuckyNetServer.csproj
dotnet add reference ../Shared/DuckyNetShared.csproj
```

### 2. 实现插件

```csharp
using DuckyNet.Server.Plugin;
using DuckyNet.Server.Plugin.Events;

public class MyPlugin : IPlugin
{
    public string Name => "我的插件";
    public string Version => "1.0.0";
    public string Author => "Your Name";
    public string Description => "这是一个示例插件";

    private IPluginContext _context;

    public void OnLoad(IPluginContext context)
    {
        _context = context;
        _context.Logger.Info("插件加载成功！");
        
        // 订阅事件
        _context.EventBus.Subscribe<PlayerLoginEvent>(OnPlayerLogin);
    }

    public void OnUnload()
    {
        // 取消订阅
        _context.EventBus.Unsubscribe<PlayerLoginEvent>(OnPlayerLogin);
        _context.Logger.Info("插件已卸载");
    }

    public void OnUpdate()
    {
        // 每帧更新逻辑（可选）
    }

    private void OnPlayerLogin(PlayerLoginEvent e)
    {
        _context.Logger.Info($"欢迎 {e.Player.SteamName}！");
    }
}
```

### 3. 编译和部署

```bash
# 编译
dotnet build -c Release

# 复制到服务器插件目录
copy bin\Release\net8.0\MyPlugin.dll <服务器目录>\Plugins\
```

### 4. 启动服务器

服务器启动时会自动加载 `Plugins` 目录下的所有 DLL：

```
[PluginManager] 发现 1 个 DLL 文件
[Plugin:System] INFO: 我的插件 v1.0.0 正在加载...
[Plugin:System] INFO: 插件加载成功！
[PluginManager] 已加载插件: 我的插件 v1.0.0 by Your Name
```

## 📚 示例插件

查看 `ExamplePlugin` 项目获取完整示例：

- **WelcomePlugin**：欢迎消息和统计
- **AntiFloodPlugin**：防刷屏检测

## 🔧 高级功能

### 访问玩家管理器

```csharp
// 获取所有在线玩家
var players = _context.PlayerManager.GetAllOnlinePlayers();

// 获取特定玩家
var player = _context.PlayerManager.GetPlayer(clientId);

// 检查玩家是否在线
bool isOnline = _context.PlayerManager.IsLoggedIn(steamId);
```

### 访问房间管理器

```csharp
// 获取所有房间
var rooms = _context.RoomManager.GetAllRooms();

// 获取房间玩家
var players = _context.RoomManager.GetRoomPlayers(roomId);

// 获取玩家所在房间
var room = _context.RoomManager.GetPlayerRoom(player);
```

### 访问 RPC 服务器

```csharp
// 断开客户端连接
_context.RpcServer.DisconnectClient(clientId, "违规操作");

// 获取客户端上下文
var clientContext = _context.RpcServer.GetClientContext(clientId);
```

### 发布自定义事件

```csharp
// 定义自定义事件
public class MyCustomEvent
{
    public string Message { get; set; }
}

// 发布事件
_context.EventBus.Publish(new MyCustomEvent 
{ 
    Message = "Hello from plugin!" 
});

// 其他插件可以订阅
_context.EventBus.Subscribe<MyCustomEvent>(e =>
{
    _context.Logger.Info($"收到消息: {e.Message}");
});
```

## ⚠️ 注意事项

1. **线程安全**：事件处理器在服务器主线程执行，访问共享资源时注意同步
2. **性能**：`OnUpdate()` 每秒调用约 60 次，避免执行重操作
3. **异常处理**：虽然有异常保护，但最好在插件内部处理异常
4. **资源清理**：务必在 `OnUnload()` 中取消所有事件订阅
5. **日志规范**：使用 `_context.Logger` 而不是 `Console.WriteLine`

## 🎯 为什么不用 Lua？

我们选择 C# 而非 Lua 的原因：

| 特性 | C# 插件 | Lua 脚本 |
|------|---------|----------|
| **性能** | ⭐⭐⭐⭐⭐ 原生 | ⭐⭐⭐ 需要解释 |
| **类型安全** | ✅ 编译时检查 | ❌ 运行时错误 |
| **IDE 支持** | ✅ 完整 IntelliSense | ⚠️ 有限 |
| **调试** | ✅ Visual Studio | ⚠️ 较困难 |
| **热重载** | ⚠️ 需要重启 | ✅ 支持 |
| **学习曲线** | ⚠️ 需要 C# 知识 | ✅ 语法简单 |

**结论**：对于需要稳定性和性能的服务器插件，C# 是更好的选择。如果需要频繁热更新，可以考虑混合方案（C# + Lua）。

## 📖 更多资源

- [示例插件源码](../../ExamplePlugin/)
- [服务器 API 文档](../README.md)
- [事件系统详解](./Events/README.md)

