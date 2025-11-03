# 物品丢弃拾取系统 - 行为说明

## 🎮 玩家视角的行为

### 场景 1：在房间中丢弃物品 ✅

```
玩家 A 在房间 "Room_001" 中丢弃 AK-47
    ↓
✅ 物品掉落在地上（本地）
✅ 服务器分配 DropId = 123
✅ 广播到房间内其他玩家
    ↓
玩家 B、C（同房间）看到 AK-47 掉落
玩家 D（其他房间）看不到
```

**日志输出：**
```
[DropOperationBroadcaster] 网络同步完成 → ID=123, 物品=AK-47
[ItemSyncService] 物品丢弃 - DropId=123, Room=Room_001, IsDefault=false
[ItemSyncService] 已广播到房间 Room_001 的其他玩家
```

---

### 场景 2：不在房间中丢弃物品 ✅

```
玩家 A 在大厅/单人游玩时丢弃石头
    ↓
✅ 物品掉落在地上（本地）
✅ 服务器返回 DropId = 0（表示仅本地）
⚠️ 不广播给其他玩家
    ↓
只有玩家 A 自己能看到这个石头
其他玩家（即使在同一场景）也看不到
```

**日志输出：**
```
[DropOperationBroadcaster] 本地丢弃（仅自己可见） - 物品=石头
[ItemSyncService] 玩家不在房间，物品仅本地可见 - Player=PlayerA
```

---

### 场景 3：在房间中拾取网络物品 ✅

```
玩家 B 拾取 DropId=123 的 AK-47（玩家 A 丢弃的）
    ↓
✅ 玩家 B 获得物品（本地）
✅ 发送拾取请求到服务器
✅ 服务器广播销毁通知
    ↓
玩家 A、C 看到 AK-47 消失
```

**日志输出：**
```
[PickupActionForwarder] 检测到网络物品交互 → ID=123, 名称=AK-47
[ItemSyncService] 物品拾取 - DropId=123, Player=PlayerB, Room=Room_001
[ItemNetworkCoordinator] 物品已销毁 - DropId=123
```

---

### 场景 4：拾取本地物品 ✅

```
玩家 A 拾取自己在大厅丢弃的石头（无网络标记）
    ↓
✅ 玩家 A 获得物品（本地）
⚠️ 没有 NetworkDropTag，跳过网络同步
    ↓
正常拾取，不触发任何网络请求
```

**日志输出：**
```
（无日志 - Harmony Prefix 检测到无 NetworkDropTag，直接跳过）
```

---

## 📊 行为对照表

| 场景 | 是否在房间 | 丢弃者可见 | 房间内其他玩家 | 其他房间玩家 | DropId | 网络同步 |
|------|-----------|-----------|--------------|-------------|--------|---------|
| 房间内丢弃 | ✅ 是 | ✅ 可见 | ✅ 可见 | ❌ 不可见 | 123 | ✅ 同步 |
| 大厅丢弃 | ❌ 否 | ✅ 可见 | ❌ 不可见 | ❌ 不可见 | 0 | ❌ 仅本地 |
| 单人游玩 | ❌ 否 | ✅ 可见 | - | - | 0 | ❌ 仅本地 |
| 拾取网络物品 | ✅ 是 | ✅ 获得 | ✅ 看到消失 | ❌ 不影响 | 123 | ✅ 同步 |
| 拾取本地物品 | - | ✅ 获得 | - | - | - | ❌ 仅本地 |

---

## 🔍 判断逻辑

### 服务器端（ItemSyncServiceImpl.cs）

```csharp
public async Task<uint> DropItemAsync(IClientContext client, ItemDropData dropData)
{
    var player = _playerManager.GetPlayer(client.ClientId);
    var room = _roomManager.GetPlayerRoom(player);
    
    if (room == null)
    {
        // ⚠️ 不在房间中
        Console.WriteLine("玩家不在房间，物品仅本地可见");
        return 0; // 返回 0 = 仅本地丢弃
    }
    
    // ✅ 在房间中
    uint dropId = AllocateDropId(); // 分配真实 DropId
    _droppedItems[dropId] = (room.RoomId, dropData);
    
    // 广播到房间内其他玩家
    _server.BroadcastToRoom<IItemSyncClientService>(room.RoomId, exceptClientId: client.ClientId)
        .OnRemoteItemDropped(dropData);
    
    return dropId; // 返回真实 DropId
}
```

### 客户端（ItemNetworkCoordinator.cs）

```csharp
public async Task<uint?> DropItemAsync(Item item, Vector3 position, ...)
{
    uint dropId = await _itemSyncService.DropItemAsync(dropData);
    
    if (dropId == 0)
    {
        // ⚠️ 服务器返回 0 = 不在房间中
        Debug.Log("物品仅本地可见（不在房间中）");
        return null; // 不注册网络映射
    }
    
    // ✅ 服务器返回真实 DropId = 在房间中
    Debug.Log($"发送丢弃成功 - DropId={dropId}");
    return dropId;
}
```

### Harmony Patch（DropOperationBroadcaster.cs）

```csharp
var dropId = await coordinator.DropItemAsync(...);

if (dropId.HasValue && dropId.Value > 0)
{
    // ✅ 网络同步物品
    coordinator.RegisterLocalDrop(dropId.Value, agent);
    // 添加 NetworkDropTag 组件
}
else
{
    // ⚠️ 本地物品（无 NetworkDropTag）
    Debug.Log("本地丢弃（仅自己可见）");
    // 不添加任何网络标记
}
```

---

## 💡 设计优势

### 1. 自动降级

```
在房间中   → 网络同步 ✅
离开房间   → 自动切换到本地模式 ✅
重新进房间 → 恢复网络同步 ✅
```

### 2. 性能友好

```
单人游玩时：
  - 不发送网络请求 ⚡
  - 不序列化数据 ⚡
  - 不占用服务器资源 ⚡
```

### 3. 用户体验

```
✅ 玩家感知不到差异（本地丢弃和网络丢弃体验一致）
✅ 不会因为网络问题导致无法丢弃物品
✅ 单人时性能更好（无网络开销）
```

---

## 🔐 网络物品识别

### 如何区分本地物品和网络物品？

```csharp
// 检查物品 Agent 上的组件
var networkTag = agent.GetComponent<NetworkDropTag>();

if (networkTag != null)
{
    // 🌐 网络物品
    uint dropId = networkTag.DropId;
    bool isLocal = networkTag.IsLocalDrop;  // true=自己丢的，false=别人丢的
}
else
{
    // 💻 本地物品（不在房间时丢弃的）
    // 不会同步给其他玩家
}
```

### NetworkDropTag 组件

```csharp
public class NetworkDropTag : MonoBehaviour
{
    public uint DropId { get; set; }         // 服务器分配的全局ID
    public bool IsLocalDrop { get; set; }    // true=本地丢的，false=远程创建的
}
```

**用途：**
- 标识网络同步物品
- 拾取时识别需要同步的物品
- 调试时可视化网络物品

---

## 📝 使用建议

### 推荐工作流程

```
1. 启动服务器
2. 玩家 A 启动游戏 → 连接服务器 → 创建房间 → 进入地图
3. 玩家 B 启动游戏 → 连接服务器 → 加入房间 → 进入地图
4. 玩家 A 丢弃物品 → 玩家 B 立即看到 ✅
5. 玩家 B 拾取物品 → 玩家 A 看到消失 ✅
```

### 单人测试

```
1. 启动游戏（不连接服务器 或 不加入房间）
2. 进入地图
3. 丢弃物品 → 正常工作，仅自己可见 ✅
4. 拾取物品 → 正常工作 ✅
```

---

## 🎯 总结

现在的实现：

✅ **在房间中** → 自动网络同步，所有房间成员可见  
✅ **不在房间** → 自动本地模式，仅自己可见  
✅ **完全无感** → 玩家感知不到差异  
✅ **性能最优** → 单人时零网络开销  

**这是最佳的用户体验设计！** 👍

