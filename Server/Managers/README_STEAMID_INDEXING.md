# SteamId 索引最佳实践

## 统一的索引方式

在整个 Server 项目中，通过 SteamId 索引数据统一使用 **字典直接索引** 的方式，保证 O(1) 时间复杂度。

## ✅ 正确的模式

### 1. 字典查询 - TryGetValue（推荐）
```csharp
if (_playersBySteamId.TryGetValue(steamId, out var player))
{
    // 使用 player
    return player;
}
return null;
```

### 2. 字典查询 - 索引器
```csharp
_playersBySteamId[steamId] = playerInfo;  // 添加或更新
_clientIdBySteamId[steamId] = clientId;
```

### 3. 字典查询 - ContainsKey
```csharp
if (_playersBySteamId.ContainsKey(steamId))
{
    // 玩家存在
}
```

### 4. 字典操作 - Remove
```csharp
_playersBySteamId.Remove(steamId);
_clientIdBySteamId.Remove(steamId);
```

## ❌ 错误的模式（已修复）

### 遍历查询（低效 O(n)）
```csharp
// ❌ 不要这样做！
foreach (var kvp in _playersByClientId)
{
    if (kvp.Value.SteamId == steamId)
    {
        return kvp.Key;
    }
}
```

**问题：** 需要遍历整个字典，时间复杂度 O(n)，在玩家数量多时性能下降。

## 实现细节

### PlayerManager 中的三个映射

为了支持高效的双向查询，PlayerManager 维护了三个字典：

```csharp
// ClientId -> PlayerInfo
private readonly Dictionary<string, PlayerInfo> _playersByClientId;

// SteamId -> PlayerInfo
private readonly Dictionary<string, PlayerInfo> _playersBySteamId;

// SteamId -> ClientId（反向映射）
private readonly Dictionary<string, string> _clientIdBySteamId;
```

### 数据一致性保证

**关键原则：** 在所有修改玩家映射的地方，必须同时维护这三个字典，保证数据一致性。

#### 添加玩家（OnClientLogin）
```csharp
_playersByClientId[ClientId] = playerInfo;
_playersBySteamId[playerInfo.SteamId] = playerInfo;
_clientIdBySteamId[playerInfo.SteamId] = ClientId;
```

#### 移除玩家（OnClientDisconnected）
```csharp
_playersByClientId.Remove(ClientId);
_playersBySteamId.Remove(player.SteamId);
_clientIdBySteamId.Remove(player.SteamId);
```

## 应用示例

### PlayerManager.cs
- ✅ `GetPlayerBySteamId()` - 使用 `TryGetValue`
- ✅ `GetClientIdBySteamId()` - 使用 `TryGetValue`
- ✅ `UpdatePlayerSceneDataBySteamId()` - 使用 `TryGetValue`
- ✅ `IsLoggedIn()` - 使用 `ContainsKey`

### RoomManager.cs
- ✅ 所有操作都使用 `_playerRoom.TryGetValue(steamId, ...)` 或 `ContainsKey`

### 各种 Service 实现
- ✅ `PlayerUnitySyncServiceImpl` - 使用 `TryGetValue` 和索引器
- ✅ `HealthSyncServiceImpl` - 使用 `TryGetValue` 和索引器
- ✅ `CharacterAppearanceServiceImpl` - 使用 `TryGetValue` 和索引器
- ✅ `WeaponSyncServerServiceImpl` - 使用索引器

## 性能对比

| 操作方式 | 时间复杂度 | 100 玩家耗时 | 1000 玩家耗时 |
|---------|-----------|-------------|--------------|
| 字典索引 (TryGetValue) | O(1) | ~1μs | ~1μs |
| 遍历查询 (foreach) | O(n) | ~10μs | ~100μs |

## 总结

- 🎯 **统一使用字典索引**，避免遍历
- 🔒 **维护数据一致性**，同时更新所有相关字典
- ⚡ **保证 O(1) 性能**，支持高并发场景
- 📝 **代码可读性好**，意图清晰明确

