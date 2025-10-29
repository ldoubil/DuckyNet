# DuckyNet 客户端系统逻辑图

## 系统架构概览

```
┌─────────────────────────────────────────────────────────────────┐
│                        Unity 游戏引擎                              │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     ModBehaviour (入口)                           │
│  • Awake() - 初始化                                              │
│  • Update() - 每帧更新                                           │
│  • OnGUI() - GUI 渲染                                            │
│  • OnDestroy() - 清理                                            │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    GameContext (服务容器)                          │
│  使用 Service Locator 模式管理所有核心服务                         │
└─────────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
        ▼                     ▼                     ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ 核心管理器     │    │ RPC通信层    │    │ UI 系统       │
└──────────────┘    └──────────────┘    └──────────────┘
```

## 核心模块关系图

### 1. 初始化流程

```
ModBehaviour.Awake()
    │
    ├─► ConsoleModule.Initialize()
    ├─► InitializeHarmony() → 应用 Harmony Patch
    └─► InitializeGameContext()
            │
            ├─► GameContext.Initialize()
            │
            ├─► 注册核心服务
            │   ├─► LocalPlayer (本地玩家信息)
            │   ├─► RpcClient (网络通信)
            │   ├─► InputManager (输入管理)
            │   ├─► AvatarManager (头像管理)
            │   ├─► UnitManager (单位管理)
            │   ├─► CharacterCustomizationManager (角色自定义)
            │   ├─► SceneManager (场景管理)
            │   ├─► UIManager (UI管理)
            │   └─► SyncManager (同步管理)
            │
            ├─► 注册客户端 RPC 服务
            │   ├─► PlayerClientServiceImpl
            │   ├─► RoomClientServiceImpl
            │   ├─► SceneClientServiceImpl
            │   ├─► CharacterClientServiceImpl
            │   └─► CharacterSyncClientServiceImpl
            │
            └─► UIManager.Initialize()
                ├─► 创建 UI 窗口
                │   ├─► MainMenuWindow
                │   ├─► ChatWindow
                │   ├─► PlayerListWindow
                │   ├─► DebugWindow
                │   ├─► AnimationDebugWindow
                │   └─► AnimatorStateViewer
                └─► 注册输入按键
```

### 2. 网络通信层

```
┌─────────────────────────────────────────────────────────────┐
│                       RpcClient                               │
│  • LiteNetLib 网络库                                         │
│  • RPC 消息序列化/反序列化                                    │
│  • 同步/异步调用                                             │
│  • 连接管理                                                  │
└─────────────────────────────────────────────────────────────┘
         │                    │                    │
         │                    │                    │
         ▼                    ▼                    ▼
┌──────────────┐    ┌──────────────┐    ┌──────────────┐
│ 服务器调用     │    │ 客户端服务    │    │ 连接事件      │
│ (Call Server) │    │ (Client      │    │ (Events)     │
│               │    │  Service)     │    │              │
└──────────────┘    └──────────────┘    └──────────────┘
         │                    │                    │
         │                    │                    │
         ▼                    ▼                    ▼
┌─────────────────────────────────────────────────────────────┐
│                   服务器端 (Server)                          │
└─────────────────────────────────────────────────────────────┘
```

### 3. 场景管理系统

```
┌─────────────────────────────────────────────────────────────┐
│                      SceneManager                             │
│  • 追踪玩家场景位置                                            │
│  • 管理场景内玩家模型                                          │
│  • 监听 Unity 场景切换事件                                     │
└─────────────────────────────────────────────────────────────┘
         │
         ├─► LevelManager (游戏原生)
         │   ├─► OnLevelBeginInitializing (离开场景)
         │   └─► OnLevelInitialized (进入场景)
         │
         ├─► 服务器同步
         │   ├─► EnterSceneAsync() - 通知服务器进入场景
         │   ├─► LeaveSceneAsync() - 通知服务器离开场景
         │   └─► GetScenePlayersAsync() - 获取场景内玩家
         │
         └─► 模型管理
             ├─► CreatePlayerModel() - 创建玩家模型
             │   └─► UnitManager.CreateUnit()
             ├─► UpdatePlayerAppearance() - 更新外观
             │   └─► CharacterCustomizationManager
             └─► DestroyPlayerModel() - 销毁模型
```

### 4. 角色同步系统

```
┌─────────────────────────────────────────────────────────────┐
│                      SyncManager                               │
│  • 定期发送本地角色状态 (20次/秒)                              │
│  • 条件检查：角色存在 + 在地图中                                │
│  • 状态缓存优化                                               │
└─────────────────────────────────────────────────────────────┘
         │
         ├─► CharacterSyncHelper.FromUnity()
         │   └─► 提取角色状态数据
         │       ├─► Transform (位置、旋转)
         │       ├─► Animator (动画状态)
         │       └─► 其他同步数据
         │
         ├─► RpcClient.InvokeServer<ICharacterSyncService>()
         │   └─► SyncCharacterState()
         │
         └─► 同步循环 (SyncLoopAsync)
             └─► 每 0.05 秒执行一次
```

### 5. 角色外观系统

```
┌─────────────────────────────────────────────────────────────┐
│            CharacterCustomizationManager                       │
│  • 管理角色自定义数据                                          │
│  • 应用外观到角色模型                                          │
└─────────────────────────────────────────────────────────────┘
         │
         ├─► CharacterAppearanceConverter
         │   ├─► ConvertFromGameData() - 游戏数据 → 网络数据
         │   └─► ConvertToGameData() - 网络数据 → 游戏数据
         │
         ├─► CharacterAppearanceHelper
         │   ├─► StartAutoUpload() - 自动上传外观
         │   └─► UploadCurrentAppearanceAsync() - 上传外观
         │
         └─► CharacterCreationListener (Harmony Patch)
             └─► 监听角色创建事件，自动上传外观
```

### 6. UI 系统架构

```
┌─────────────────────────────────────────────────────────────┐
│                       UIManager                                │
│  • 管理所有 UI 窗口                                            │
│  • 注册输入按键                                                │
│  • 窗口显示/隐藏控制                                           │
└─────────────────────────────────────────────────────────────┘
         │
         ├─► MainMenuWindow
         │   ├─► 连接服务器
         │   ├─► 房间列表
         │   └─► LobbyPage / RoomPage
         │
         ├─► ChatWindow
         │   └─► 聊天消息显示
         │       └─► PlayerClientService.OnChatMessageReceived
         │
         ├─► PlayerListWindow
         │   └─► 玩家列表显示
         │
         ├─► DebugWindow
         │   └─► DebugModuleManager
         │       ├─► CharacterCustomizationModule
         │       └─► PlayerSpawnModule
         │
         ├─► AnimationDebugWindow
         │   └─► AnimationDebugger
         │
         └─► AnimatorStateViewer
             └─► 动画状态机可视化
```

### 7. 数据流向图

#### 7.1 角色创建与外观上传流程

```
游戏创建角色
    │
    ▼
CharacterCreationListener (Harmony Patch)
    │
    ▼
CharacterAppearanceHelper.UploadCurrentAppearanceAsync()
    │
    ├─► CharacterCustomizationManager.GetLocalPlayerCharacter()
    ├─► CharacterCustomizationManager.ExtractAppearanceData()
    ├─► CharacterAppearanceConverter.ConvertFromGameData()
    └─► RpcClient.InvokeServerAsync<ICharacterService>()
        │
        └─► UploadCharacterAppearanceAsync()
            │
            ▼
服务器接收并存储外观数据
```

#### 7.2 角色同步流程

```
本地角色移动/动作
    │
    ▼
SyncManager.SyncLoopAsync() (20次/秒)
    │
    ├─► ShouldSync() 检查
    │   ├─► 角色是否存在？
    │   └─► 是否在地图中？
    │
    ├─► CharacterSyncHelper.FromUnity()
    │   └─► 提取状态数据
    │
    └─► RpcClient.InvokeServer<ICharacterSyncService>()
        │
        └─► SyncCharacterState(syncData)
            │
            ▼
服务器广播给其他客户端
    │
    ▼
其他客户端接收同步数据
    │
    └─► CharacterSyncClientServiceImpl.OnCharacterSync()
        │
        └─► 更新远程玩家状态
```

#### 7.3 场景同步流程

```
Unity 场景切换
    │
    ▼
LevelManager.OnLevelInitialized
    │
    ▼
SceneManager.OnLevelInitialized()
    │
    ├─► NotifySceneLoaded(sceneName)
    │   ├─► SceneService.EnterSceneAsync()
    │   ├─► SceneService.GetScenePlayersAsync()
    │   └─► CreateModelsForCurrentScene()
    │
    └─► 服务器广播场景变化
        │
        ▼
其他客户端收到场景更新
    │
    └─► SceneClientServiceImpl.OnPlayerEnteredScene()
        │
        └─► SceneManager.UpdatePlayerScene()
            │
            └─► 创建或更新玩家模型
```

#### 7.4 玩家外观同步流程

```
服务器收到其他玩家外观
    │
    ▼
CharacterClientServiceImpl.OnCharacterAppearanceUpdated()
    │
    ├─► SceneManager.UpdatePlayerAppearance()
    │   │
    │   ├─► 检查模型是否存在
    │   │   └─► 不存在则创建 (CreatePlayerModel)
    │   │
    │   ├─► CharacterAppearanceData.FromBytes()
    │   ├─► CharacterAppearanceConverter.ConvertToGameData()
    │   └─► CharacterCustomizationManager.ApplyToCharacter()
    │
    └─► 更新玩家外观
```

### 8. 事件驱动流程

```
┌─────────────────────────────────────────────────────────────┐
│                    连接事件                                    │
└─────────────────────────────────────────────────────────────┘
         │
         ├─► RpcClient.Connected
         │   │
         │   └─► ModBehaviour.OnConnected()
         │       ├─► TryUploadExistingCharacterAsync()
         │       └─► SyncManager.StartSync()
         │
         └─► RpcClient.Disconnected
             │
             └─► ModBehaviour.OnDisconnected()
                 ├─► SyncManager.StopSync()
                 └─► SceneManager.OnLeftRoom()

┌─────────────────────────────────────────────────────────────┐
│                    场景事件                                    │
└─────────────────────────────────────────────────────────────┘
         │
         ├─► SceneManager.OnSceneLoaded
         │   └─► 创建场景内玩家模型
         │
         └─► SceneManager.OnSceneUnloading
             └─► 清理场景模型

┌─────────────────────────────────────────────────────────────┐
│                    RPC 事件                                    │
└─────────────────────────────────────────────────────────────┘
         │
         ├─► PlayerClientService.OnChatMessageReceived
         │   └─► ChatWindow.AddMessage()
         │
         ├─► SceneClientService.OnPlayerEnteredScene
         │   └─► SceneManager.UpdatePlayerScene()
         │
         └─► CharacterClientService.OnCharacterAppearanceUpdated
             └─► SceneManager.UpdatePlayerAppearance()
```

## 关键服务依赖关系

```
ModBehaviour
    │
    └─► GameContext (单例)
            │
            ├─► LocalPlayer ──────────┐
            │                        │
            ├─► RpcClient ──────────┤
            │                        │
            ├─► InputManager         │
            │                        │
            ├─► UIManager ──────────┤
            │                        │
            ├─► SceneManager ───────┼─► 依赖关系
            │                        │
            ├─► SyncManager ────────┤
            │   ├─► RpcClient       │
            │   ├─► CharacterCustomizationManager
            │   ├─► LocalPlayer ────┘
            │   └─► SceneManager
            │
            ├─► UnitManager
            │   └─► CharacterCustomizationManager (可选)
            │
            └─► CharacterCustomizationManager
                └─► 游戏原生角色系统
```

## 系统生命周期

```
启动阶段:
1. ModBehaviour.Awake()
2. GameContext.Initialize()
3. 注册所有服务
4. Harmony Patch 应用
5. UI 初始化

运行阶段:
每帧:
- ModBehaviour.Update() → GameContext.Update()
  - RpcClient.Update()
  - InputManager.Update()
  - UIManager.Update()
  
- ModBehaviour.OnGUI() → GameContext.OnGUI()
  - UIManager.OnGUI()

后台任务:
- SyncManager 同步循环 (每 0.05 秒)
- RpcClient 网络事件处理

关闭阶段:
1. ModBehaviour.OnDestroy()
2. 取消 Harmony Patch
3. 取消事件订阅
4. GameContext.Cleanup()
   - 清理所有服务
5. ConsoleModule.Cleanup()
```

## 关键数据结构

### RPC 消息流程

```
客户端调用:
  RpcClient.InvokeServerAsync<TService>(method, params)
    │
    ├─► 创建 RpcMessage
    │   ├─► MessageId
    │   ├─► ServiceName
    │   ├─► MethodName
    │   └─► Parameters (序列化)
    │
    ├─► 发送到服务器
    │
    └─► 等待 RpcResponse
        ├─► Success
        ├─► Result (反序列化)
        └─► ErrorMessage

服务器调用客户端:
  服务器发送 RpcMessage
    │
    ▼
  RpcClient.HandleMessage()
    │
    ├─► 反序列化参数
    ├─► RpcInvoker.Invoke()
    │   └─► 调用本地服务实现
    └─► 发送 RpcResponse
```

### 角色同步数据结构

```
CharacterSyncData
├─► PlayerId (SteamId)
├─► Position (Vector3)
├─► Rotation (Quaternion)
├─► AnimationState
│   ├─► MovementSpeed
│   ├─► IsGrounded
│   ├─► VerticalVelocity
│   └─► CurrentAnimation
└─► Timestamp
```

### 场景信息数据结构

```
PlayerSceneInfo
├─► SteamId
├─► SceneName
├─► HasCharacter
└─► PlayerInfo (PlayerInfo)
    ├─► SteamId
    ├─► SteamName
    ├─► AvatarUrl
    └─► Status
```

## 总结

DuckyNet 客户端系统采用模块化设计，通过 GameContext 统一管理所有服务。核心特性包括：

1. **网络通信**: 基于 LiteNetLib 的 RPC 框架
2. **场景同步**: 实时追踪玩家场景位置
3. **角色同步**: 20次/秒的状态同步
4. **外观系统**: 自动上传和应用角色外观
5. **UI 系统**: 模块化的窗口管理
6. **事件驱动**: 基于事件的响应式架构

系统通过 Harmony 框架与游戏原生系统集成，实现无缝的多人在线体验。

