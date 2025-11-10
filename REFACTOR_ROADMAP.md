# 🚧 DuckyNet Server 重构施工进度

> **重构目标：** 将臃肿的单体架构重构为「底层框架 + 模块 + 插件」的分层架构
> 
> **开始时间：** 2025-11-10  
> **预计工期：** 2-3 周

---

## 📋 总体架构

```
┌─────────────────────────────────────────┐
│         Plugins (热插拔扩展)              │
│  └─ ChatPlugin                          │
│  └─ UnitySyncPlugin (位置同步)           │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────┴───────────────────────┐
│         Modules (可选业务模块)            │
│  └─ PlayerModule (玩家档案)              │
│  └─ RoomModule (房间系统) ✓核心          │
│  └─ SceneModule (场景管理)               │
│  └─ CharacterModule (角色外观)           │
│  └─ EquipmentModule (装备武器)           │
└─────────────────┬───────────────────────┘
                  │
┌─────────────────┴───────────────────────┐
│      Core Framework (底层框架)           │
│  └─ RpcServer (网络层)                   │
│  └─ IdentityManager (身份层)             │
│  └─ EventBus (事件总线)                  │
│  └─ DI Container (依赖注入)              │
│  └─ ServerHost (生命周期)                │
└─────────────────────────────────────────┘
```

---

## 🎯 核心设计决策

### 1. PlayerInfo 处理策略 ⚠️ 重要约束

#### **约束条件**
```
PlayerInfo 定义在 Shared 项目中，是客户端-服务端通信协议
❌ 不能拆分 PlayerInfo（会破坏客户端兼容性）
✅ 保持 PlayerInfo 作为完整的玩家数据容器
✅ 服务端内部优化架构，不改动 Shared 层
```

#### **调整后的方案**

**Shared 层：PlayerInfo（保持不变）**
```csharp
// Shared/Services/IPlayerService.cs
public class PlayerInfo  // 通信协议，不改动
{
    public string SteamId { get; set; }
    public string SteamName { get; set; }
    public string AvatarUrl { get; set; }
    public ScenelData CurrentScenelData { get; set; }
    public bool HasCharacter { get; set; }
    public byte[]? AppearanceData { get; set; }
    public PlayerEquipmentData EquipmentData { get; set; }
    public PlayerWeaponData? WeaponData { get; set; }
}
```

**Server Core 层：PlayerIdentity（轻量级映射）**
```csharp
// 只用于 ClientId ↔ SteamId 映射，不替代 PlayerInfo
public readonly struct PlayerIdentity
{
    public string ClientId { get; }   // 连接标识（短暂）
    public string SteamId { get; }    // 玩家标识（持久）
}

// 使用场景：快速查找映射关系，不存储业务数据
// PlayerInfo 依然是玩家数据的主要容器
```

**重构重点：架构优化，而非数据拆分**
```
✅ 引入依赖注入 → 解决初始化混乱
✅ 模块化服务 → 职责清晰
✅ 插件化非核心功能 → 热插拔扩展
❌ 不拆分 PlayerInfo → 保持兼容性
```

---

### 2. 降级为插件的功能

| 功能 | 原位置 | 降级原因 | 状态 |
|------|--------|---------|------|
| **聊天系统** | `PlayerServiceImpl.SendChatMessage` | 非核心功能，可热插拔 | ⏳ 待迁移 |
| **位置同步** | `PlayerUnitySyncServiceImpl` | 业务逻辑，可替换同步方案 | ⏳ 待迁移 |

---

## 📅 施工阶段

### **阶段 0：准备工作** ✅

**目标：** 设计架构、制定计划、不改动代码

- [x] 分析现有架构问题
- [x] 设计分层方案
- [x] 确定数据分层策略
- [x] 创建施工文档

---

### **阶段 1：引入依赖注入（1-2天）** ⏳

**目标：** 消除手动 `new`，解决初始化依赖地狱

#### 任务清单
- [ ] 1.1 安装 `Microsoft.Extensions.DependencyInjection` NuGet 包
- [ ] 1.2 创建 `Core/ServiceCollectionExtensions.cs`
  ```csharp
  public static class ServiceCollectionExtensions
  {
      public static IServiceCollection AddDuckyNetCore(this IServiceCollection services)
      {
          // 注册底层服务
          services.AddSingleton<RpcServer>();
          services.AddSingleton<EventBus>();
          return services;
      }
      
      public static IServiceCollection AddDuckyNetModules(this IServiceCollection services)
      {
          // 注册现有服务（暂时保持原样）
          services.AddSingleton<PlayerManager>();
          services.AddSingleton<RoomManager>();
          services.AddSingleton<IPlayerService, PlayerServiceImpl>();
          services.AddSingleton<IRoomService, RoomServiceImpl>();
          // ... 其他服务
          return services;
      }
  }
  ```
- [ ] 1.3 重构 `Program.cs`：使用 DI 容器
  ```csharp
  var services = new ServiceCollection();
  services.AddDuckyNetCore();
  services.AddDuckyNetModules();
  var provider = services.BuildServiceProvider();
  
  var server = provider.GetRequiredService<RpcServer>();
  server.Start(9050);
  ```
- [ ] 1.4 移除所有手动依赖注入的 hack 代码
  - 删除 `roomService.SetEquipmentService()` 延迟注入
  - 删除 `RoomBroadcastHelper.Initialize()` 静态初始化

#### 验证标准
- ✅ 服务器启动成功
- ✅ 玩家登录/登出正常
- ✅ 房间创建/加入正常
- ✅ 代码行数减少 30%（Program.cs: 182 → ~120 行）

---

### **阶段 2：创建底层框架（2-3天）** ⏳

**目标：** 提取稳定的底层抽象

#### 任务清单
- [ ] 2.1 创建目录结构
  ```
  Server/Core/
  ├── Network/
  │   └── (已有 RpcServer，保持不变)
  ├── Identity/
  │   ├── PlayerIdentity.cs        [新建]
  │   └── IdentityManager.cs       [新建]
  ├── Lifecycle/
  │   ├── IServerLifecycle.cs      [新建]
  │   └── ServerHost.cs            [新建]
  └── ServiceCollectionExtensions.cs
  ```

- [ ] 2.2 实现 `PlayerIdentity`（只读结构体）
  ```csharp
  public readonly struct PlayerIdentity
  {
      public string ClientId { get; }
      public string SteamId { get; }
      
      public PlayerIdentity(string clientId, string steamId)
      {
          ClientId = clientId;
          SteamId = steamId;
      }
  }
  ```

- [ ] 2.3 实现 `IdentityManager`（底层映射管理）
  ```csharp
  public class IdentityManager
  {
      private readonly Dictionary<string, string> _clientIdToSteamId = new();
      private readonly Dictionary<string, string> _steamIdToClientId = new();
      private readonly object _lock = new();
      
      public bool Bind(string clientId, string steamId) { /* ... */ }
      public bool Unbind(string clientId) { /* ... */ }
      public string? GetSteamId(string clientId) { /* ... */ }
      public string? GetClientId(string steamId) { /* ... */ }
      public PlayerIdentity? GetIdentity(string clientId) { /* ... */ }
  }
  ```

- [ ] 2.4 实现 `ServerHost`（生命周期管理）
  ```csharp
  public class ServerHost
  {
      private readonly IServiceProvider _services;
      private readonly RpcServer _server;
      private readonly PluginManager _pluginManager;
      private CancellationTokenSource _cts;
      
      public async Task RunAsync()
      {
          // 1. 启动网络层
          // 2. 加载插件
          // 3. 启动后台任务
          // 4. 等待停止信号
          // 5. 优雅关闭
      }
      
      private async Task UpdateLoopAsync(CancellationToken token) { /* ... */ }
      private async Task TimeoutCheckLoopAsync(CancellationToken token) { /* ... */ }
  }
  ```

- [ ] 2.5 重构 `PlayerManager` 配合 `IdentityManager`
  - 保持 PlayerInfo 作为完整数据容器
  - IdentityManager 提供快速映射查询接口
  - PlayerManager 继续管理 PlayerInfo 业务数据
  - 两者协作而非替代关系

#### 验证标准
- ✅ 身份映射逻辑正确（断线重连测试）
- ✅ 生命周期管理正常（启动/停止/Ctrl+C）
- ✅ 原有功能不受影响

---

### **阶段 3：模块化重构（3-4天）** ⏳

**目标：** 将 Services 按职责重组为模块（保持 PlayerInfo 不变）

#### 任务清单

##### 3.1 PlayerModule（玩家管理模块）
- [ ] 创建 `Modules/Player/`
  ```
  Modules/Player/
  ├── PlayerServiceImpl.cs          [迁移]
  ├── PlayerManager.cs              [迁移]
  └── PlayerModuleExtensions.cs     [新建]
  ```
- [ ] 调整 PlayerManager
  - 保持 PlayerInfo 作为数据容器
  - 配合 IdentityManager 做映射查询
  - 移除聊天相关逻辑 → ChatPlugin
- [ ] 注册模块
  ```csharp
  services.AddPlayerModule();
  ```

##### 3.2 RoomModule（房间模块）✓核心
- [ ] 创建 `Modules/Room/`
  ```
  Modules/Room/
  ├── Room.cs                       [迁移]
  ├── RoomManager.cs                [迁移]
  ├── RoomServiceImpl.cs            [迁移]
  └── RoomModuleExtensions.cs       [新建]
  ```
- [ ] 保持现有逻辑
  - 继续使用 PlayerInfo
  - 通过 DI 自动解析依赖（消除延迟注入）
- [ ] 注册模块
  ```csharp
  services.AddRoomModule();
  ```

##### 3.3 SceneModule（场景管理模块）
- [ ] 创建 `Modules/Scene/`
  ```
  Modules/Scene/
  ├── SceneServiceImpl.cs           [迁移]
  └── SceneModuleExtensions.cs      [新建]
  ```
- [ ] 保持使用 PlayerInfo.CurrentScenelData
- [ ] 实现场景切换事件
  ```csharp
  public class PlayerSceneChangedEvent : IServerEvent
  {
      public PlayerInfo Player { get; set; }
      public ScenelData OldScene { get; set; }
      public ScenelData NewScene { get; set; }
  }
  ```

##### 3.4 CharacterModule（角色外观模块）
- [ ] 创建 `Modules/Character/`
  ```
  Modules/Character/
  ├── CharacterServiceImpl.cs       [迁移]
  ├── CharacterAppearanceServiceImpl.cs [迁移]
  └── CharacterModuleExtensions.cs  [新建]
  ```
- [ ] 保持使用 PlayerInfo.HasCharacter 和 AppearanceData
- [ ] 保持同步逻辑不变

##### 3.5 EquipmentModule（装备武器模块）
- [ ] 创建 `Modules/Equipment/`
  ```
  Modules/Equipment/
  ├── EquipmentServiceImpl.cs       [迁移]
  ├── WeaponSync/
  │   └── WeaponSyncServiceImpl.cs  [迁移]
  └── EquipmentModuleExtensions.cs  [新建]
  ```
- [ ] 保持使用 PlayerInfo.EquipmentData 和 WeaponData
- [ ] 通过事件解耦房间依赖

##### 3.6 SyncModule（同步服务模块，临时保留）
- [ ] 创建 `Modules/Sync/`
  ```
  Modules/Sync/
  ├── HealthSyncServiceImpl.cs      [迁移]
  ├── AnimatorSyncServiceImpl.cs    [迁移]
  ├── ItemSyncServiceImpl.cs        [迁移]
  └── SyncModuleExtensions.cs       [新建]
  ```
- [ ] 注：PlayerUnitySyncService 会在阶段4迁移到插件

#### 验证标准
- ✅ 模块可独立禁用（注释 `services.AddXXXModule()`）
- ✅ 模块间通过接口/事件通信
- ✅ 原有功能完全正常

---

### **阶段 4：插件化迁移（2-3天）** ⏳

**目标：** 将非核心功能降级为插件

#### 任务清单

##### 4.1 ChatPlugin（聊天插件）
- [ ] 创建 `Plugins/ChatPlugin/`
  ```
  Plugins/ChatPlugin/
  ├── ChatPlugin.cs                 [新建]
  ├── ChatService.cs                [新建]
  ├── IChatService.cs               [新建]
  └── README.md                     [说明文档]
  ```
- [ ] 实现插件接口
  ```csharp
  public class ChatPlugin : IPlugin
  {
      public void OnLoad(IPluginContext context)
      {
          // 注册 RPC 服务
          context.RegisterService<IChatService>(new ChatService());
          
          // 订阅事件
          context.Events.Subscribe<PlayerJoinedRoomEvent>(OnPlayerJoinedRoom);
      }
  }
  ```
- [ ] 迁移功能
  - `SendChatMessage` → `ChatService.SendMessage`
  - 全局聊天/房间聊天逻辑
- [ ] 从 `PlayerServiceImpl` 移除聊天方法
- [ ] 更新客户端接口（兼容性）

##### 4.2 UnitySyncPlugin（位置同步插件）
- [ ] 创建 `Plugins/UnitySyncPlugin/`
  ```
  Plugins/UnitySyncPlugin/
  ├── UnitySyncPlugin.cs            [新建]
  ├── UnitySyncService.cs           [迁移]
  ├── IUnitySyncService.cs          [新建]
  ├── PositionCache.cs              [新建]
  └── README.md                     [说明文档]
  ```
- [ ] 实现插件
  ```csharp
  public class UnitySyncPlugin : IPlugin
  {
      private PositionCache _cache;
      
      public void OnLoad(IPluginContext context)
      {
          _cache = new PositionCache();
          context.RegisterService<IPlayerUnitySyncService>(
              new UnitySyncService(context.Identity, context.Events, _cache)
          );
      }
      
      public void OnUpdate(float deltaTime)
      {
          // 定时清理过期位置数据
          _cache.CleanupStale();
      }
  }
  ```
- [ ] 迁移 `PlayerUnitySyncServiceImpl` 的所有逻辑
- [ ] 保持 RPC 接口不变（客户端兼容）

#### 验证标准
- ✅ 插件可独立编译为 DLL
- ✅ 插件可动态加载/卸载
- ✅ 禁用插件后对应功能不可用，其他功能正常
- ✅ 插件日志独立（`[ChatPlugin]`, `[UnitySyncPlugin]`）

---

### **阶段 5：优化与清理（1-2天）** ⏳

**目标：** 删除冗余代码，优化性能，完善文档

#### 任务清单
- [ ] 5.1 清理 Legacy 目录
  - 评估 `Server/Legacy/` 中代码是否还在使用
  - 删除无用文件，保留的迁移到对应模块
- [ ] 5.2 统一日志格式
  ```csharp
  [Core] RpcServer started on port 9050
  [PlayerModule] Player logged in: TestUser (76561198012345678)
  [RoomModule] Room created: TestRoom (room_123456)
  [ChatPlugin] Message sent: Hello World
  ```
- [ ] 5.3 性能优化
  - 使用 `ConcurrentDictionary` 替代 `Dictionary + lock`（高并发场景）
  - 位置同步缓存过期策略优化
  - 事件总线批量处理
- [ ] 5.4 完善文档
  - 更新 `README.md`（新架构说明）
  - 编写模块开发指南 `docs/MODULE_GUIDE.md`
  - 编写插件开发指南 `docs/PLUGIN_GUIDE.md`
- [ ] 5.5 单元测试
  - `IdentityManager` 单元测试（映射逻辑）
  - `PluginManager` 单元测试（加载/卸载）
  - 房间逻辑单元测试（创建/加入/离开）

#### 验证标准
- ✅ 无编译警告
- ✅ 核心测试覆盖率 > 60%
- ✅ 文档完整（能指导新人开发）

---

## 📊 进度跟踪

| 阶段 | 任务数 | 完成 | 进度 | 状态 |
|------|--------|------|------|------|
| 阶段 0：准备工作 | 4 | 4 | 100% | ✅ |
| 阶段 1：依赖注入 | 4 | 4 | 100% | ✅ **完成** |
| 阶段 2：底层框架 | 5 | 0 | 0% | ⏳ |
| 阶段 3：模块化 | 16 | 0 | 0% | ⏳ |
| 阶段 4：插件化 | 6 | 0 | 0% | ⏳ |
| 阶段 5：优化清理 | 5 | 0 | 0% | ⏳ |
| **总计** | **40** | **8** | **20%** | **🚧 施工中** |

**最新调整（2025-11-10）：**
- ⚠️ PlayerInfo 不可改动（Shared 层协议，影响客户端）
- ✅ 重构重点调整为：DI + 模块化 + 插件化
- ✅ IdentityManager 作为辅助映射，不替代 PlayerInfo

---

## 🎯 关键里程碑

- [ ] **M1 (阶段 1 完成)：** DI 容器集成，Program.cs 简化  
  _预计：2天后_
  
- [ ] **M2 (阶段 2 完成)：** 底层框架稳定，身份系统独立  
  _预计：5天后_
  
- [ ] **M3 (阶段 3 完成)：** 所有模块化完成，可独立禁用  
  _预计：10天后_
  
- [ ] **M4 (阶段 4 完成)：** 插件系统完成，聊天/同步降级  
  _预计：13天后_
  
- [ ] **M5 (阶段 5 完成)：** 重构完成，代码质量优化  
  _预计：15天后_

---

## ⚠️ 风险与应对

| 风险 | 影响 | 概率 | 应对措施 |
|------|------|------|---------|
| 客户端兼容性问题 | 高 | 中 | 保持 RPC 接口不变，逐步废弃旧接口 |
| 性能回退 | 中 | 低 | 每阶段做性能基准测试 |
| 模块依赖循环 | 中 | 中 | 严格使用事件解耦，禁止直接引用 |
| 插件加载失败 | 低 | 低 | 插件沙盒化，异常不影响主服务器 |
| 数据迁移丢失 | 高 | 极低 | 无状态设计，不涉及持久化迁移 |

---

## 📝 代码规范

### 命名约定
```csharp
// 底层框架
namespace DuckyNet.Server.Core
namespace DuckyNet.Server.Core.Network
namespace DuckyNet.Server.Core.Identity

// 模块
namespace DuckyNet.Server.Modules.Player
namespace DuckyNet.Server.Modules.Room

// 插件
namespace DuckyNet.Server.Plugins.Chat
namespace DuckyNet.Server.Plugins.UnitySync
```

### 文件组织
```
每个模块/插件必须包含：
├── XXXModule.cs / XXXPlugin.cs    (入口)
├── README.md                      (说明文档)
└── Tests/                         (单元测试，可选)
```

### 依赖规则
```
✅ 允许：Plugin → Module → Core
✅ 允许：Module → Core
✅ 允许：Module → Module (通过事件)
❌ 禁止：Core → Module
❌ 禁止：Core → Plugin
❌ 禁止：Module 直接引用 Module（必须通过接口/事件）
```

---

## 🔧 开发工具

### 推荐 NuGet 包
```xml
<!-- DI 容器 -->
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />

<!-- 日志 -->
<PackageReference Include="Serilog" Version="3.1.1" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />

<!-- 测试 -->
<PackageReference Include="xUnit" Version="2.6.2" />
<PackageReference Include="Moq" Version="4.20.70" />
```

---

## 📞 联系与反馈

如有问题或建议，请通过以下方式反馈：
- 代码审查标记 `// TODO(REFACTOR):`
- Git 提交信息格式：`[REFACTOR] 阶段X.Y: 任务描述`

---

**最后更新：** 2025-11-10 16:00  
**负责人：** AI Assistant + 用户  
**状态：** ✅ 阶段 1 完成！编译通过，0 错误 0 警告  
**进度：** 20% (8/40 任务完成)

查看详情：[REFACTOR_PHASE1_COMPLETE.md](REFACTOR_PHASE1_COMPLETE.md)

