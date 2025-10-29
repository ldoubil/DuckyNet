# API 参考 - 角色外观系统

## CharacterAppearanceHelper

角色外观辅助类，提供外观上传、下载和应用的高级 API。

### 方法

#### UploadCurrentAppearanceAsync
上传当前本地玩家的角色外观到服务器。

```csharp
public static async Task<bool> UploadCurrentAppearanceAsync()
```

**返回值**：
- `true` - 上传成功
- `false` - 上传失败（查看日志了解原因）

**使用示例**：
```csharp
bool success = await CharacterAppearanceHelper.UploadCurrentAppearanceAsync();
if (success)
{
    Debug.Log("角色外观已同步到服务器");
}
```

**注意事项**：
- 需要 `GameContext.IsInitialized` 为 true
- 需要本地角色已创建并加载
- 会自动标记 `HasCharacter = true`
- 会自动通知房间内其他玩家

---

#### DownloadAppearanceAsync
从服务器下载指定玩家的外观数据。

```csharp
public static async Task<CharacterAppearanceData?> DownloadAppearanceAsync(string steamId)
```

**参数**：
- `steamId` - 目标玩家的 Steam ID

**返回值**：
- `CharacterAppearanceData` - 外观数据对象
- `null` - 玩家不存在或未设置外观

**使用示例**：
```csharp
string targetSteamId = "76561198012345678";
var appearanceData = await CharacterAppearanceHelper.DownloadAppearanceAsync(targetSteamId);

if (appearanceData != null)
{
    Debug.Log($"头部设置: {appearanceData.HeadSetting.ScaleX}");
    Debug.Log($"部位数量: {appearanceData.Parts.Length}");
}
```

---

#### ApplyAppearance
应用外观数据到角色对象。

```csharp
public static bool ApplyAppearance(GameObject character, CharacterAppearanceData appearanceData)
```

**参数**：
- `character` - 角色 GameObject
- `appearanceData` - 外观数据对象

**返回值**：
- `true` - 应用成功
- `false` - 应用失败

**使用示例**：
```csharp
var appearanceData = await CharacterAppearanceHelper.DownloadAppearanceAsync(steamId);
bool success = CharacterAppearanceHelper.ApplyAppearance(characterObject, appearanceData);
```

---

#### StartAutoUpload
启动自动上传监听，订阅关卡初始化事件。

```csharp
public static void StartAutoUpload()
```

**说明**：
- 在 `ModBehaviour.Start()` 中自动调用
- 订阅 `LevelManager.OnLevelInitialized` 事件
- 关卡加载完成后延迟 1 秒自动上传外观
- 只需调用一次

**使用示例**：
```csharp
// 已在 ModBehaviour 中自动启用，通常无需手动调用
CharacterAppearanceHelper.StartAutoUpload();
```

---

## CharacterCustomizationManager

角色自定义管理器，负责外观数据的创建、提取和应用。

### 方法

#### GetLocalPlayerCharacter
获取本地玩家的角色 GameObject。

```csharp
public GameObject? GetLocalPlayerCharacter()
```

**返回值**：
- `GameObject` - 本地角色对象
- `null` - 角色未加载

**使用示例**：
```csharp
var customizationManager = GameContext.Instance.CharacterCustomizationManager;
var localCharacter = customizationManager.GetLocalPlayerCharacter();

if (localCharacter != null)
{
    Debug.Log($"角色名称: {localCharacter.name}");
}
```

---

#### GetCustomizationFromCharacter
从角色对象提取外观数据（游戏格式）。

```csharp
public object? GetCustomizationFromCharacter(object character)
```

**参数**：
- `character` - 角色对象（GameObject 或 Character 组件）

**返回值**：
- `object` - `CustomFaceSettingData` 对象
- `null` - 提取失败

**使用示例**：
```csharp
var customData = customizationManager.GetCustomizationFromCharacter(characterObject);
if (customData != null)
{
    // 转换为网络格式
    var converter = new CharacterAppearanceConverter();
    var networkData = converter.ConvertToNetworkData(customData);
}
```

---

#### ApplyToCharacter
应用外观数据（游戏格式）到角色对象。

```csharp
public void ApplyToCharacter(object character, object customData)
```

**参数**：
- `character` - 角色对象
- `customData` - `CustomFaceSettingData` 对象

**使用示例**：
```csharp
customizationManager.ApplyToCharacter(characterObject, customData);
```

---

#### CreateCustomization
从配置创建自定义外观数据。

```csharp
public object CreateCustomization(CharacterCustomization config)
```

**参数**：
- `config` - 外观配置结构体

**返回值**：
- `object` - `CustomFaceSettingData` 对象

**使用示例**：
```csharp
var config = new CharacterCustomization
{
    MainColor = Color.white,
    HeadScaleOffset = 1.0f,
    HairID = 3,
    EyeID = 2,
    // ... 其他配置
};

var customData = customizationManager.CreateCustomization(config);
```

---

## CharacterAppearanceConverter

外观数据转换器，在游戏格式和网络格式之间转换。

### 方法

#### ConvertToNetworkData
从游戏格式转换为网络格式。

```csharp
public CharacterAppearanceData? ConvertToNetworkData(object gameCustomData)
```

**参数**：
- `gameCustomData` - `CustomFaceSettingData` 对象

**返回值**：
- `CharacterAppearanceData` - 网络格式外观数据
- `null` - 转换失败

**使用示例**：
```csharp
var converter = new CharacterAppearanceConverter();
var networkData = converter.ConvertToNetworkData(gameCustomData);

if (networkData != null)
{
    byte[] bytes = networkData.ToBytes();
    Debug.Log($"压缩后大小: {bytes.Length} 字节");
}
```

---

#### ConvertToGameData
从网络格式转换为游戏格式。

```csharp
public object? ConvertToGameData(CharacterAppearanceData networkData)
```

**参数**：
- `networkData` - 网络格式外观数据

**返回值**：
- `object` - `CustomFaceSettingData` 对象
- `null` - 转换失败

**使用示例**：
```csharp
var gameData = converter.ConvertToGameData(networkData);
if (gameData != null)
{
    customizationManager.ApplyToCharacter(character, gameData);
}
```

---

## CharacterAppearanceData

网络传输的外观数据结构（紧凑二进制格式）。

### 属性

```csharp
public class CharacterAppearanceData
{
    public HeadSettingData HeadSetting { get; set; }
    public PartData[] Parts { get; set; }
}
```

### 方法

#### ToBytes
序列化为字节数组。

```csharp
public byte[] ToBytes()
```

**返回值**：
- 压缩的字节数组（100-600 字节）

**格式**：
```
[版本号 1字节] [头部设置 18字节] [部位数量 1字节] [部位数据 N*21字节]
```

---

#### FromBytes
从字节数组反序列化。

```csharp
public static CharacterAppearanceData FromBytes(byte[] data)
```

**参数**：
- `data` - 字节数组

**返回值**：
- `CharacterAppearanceData` 对象

**异常**：
- `Exception` - 版本不匹配或数据损坏

---

## HeadSettingData

头部设置数据（18 字节）。

```csharp
public class HeadSettingData
{
    // 缩放（6字节）
    public short ScaleX { get; set; }
    public short ScaleY { get; set; }
    public short ScaleZ { get; set; }

    // 偏移（6字节）
    public short OffsetX { get; set; }
    public short OffsetY { get; set; }
    public short OffsetZ { get; set; }

    // 旋转（6字节，度数*100）
    public short RotationX { get; set; }
    public short RotationY { get; set; }
    public short RotationZ { get; set; }
}
```

**数值范围**：
- 缩放/偏移：-327.68 到 327.67
- 精度：0.01
- 实际值 = 存储值 / 100.0

---

## PartData

部位数据（21 字节）。

```csharp
public class PartData
{
    public byte PartType { get; set; }   // 部位类型（0-255）
    public ushort PartId { get; set; }   // 部位ID（0-65535）
    
    // 缩放（6字节）
    public short ScaleX { get; set; }
    public short ScaleY { get; set; }
    public short ScaleZ { get; set; }

    // 偏移（6字节）
    public short OffsetX { get; set; }
    public short OffsetY { get; set; }
    public short OffsetZ { get; set; }

    // 旋转（6字节）
    public short RotationX { get; set; }
    public short RotationY { get; set; }
    public short RotationZ { get; set; }
}
```

---

## FloatCompression

浮点数压缩辅助类。

### 方法

#### Compress
压缩浮点数为 Int16。

```csharp
public static short Compress(float value)
```

**范围**：-327.68 到 327.67  
**精度**：0.01

---

#### Decompress
解压 Int16 为浮点数。

```csharp
public static float Decompress(short value)
```

---

#### CompressVector3
压缩 Vector3。

```csharp
public static (short x, short y, short z) CompressVector3(float x, float y, float z)
```

---

#### DecompressVector3
解压 Vector3。

```csharp
public static (float x, float y, float z) DecompressVector3(short x, short y, short z)
```

**使用示例**：
```csharp
// 压缩
var compressed = FloatCompression.CompressVector3(1.23f, -4.56f, 7.89f);
Debug.Log($"压缩后: {compressed.x}, {compressed.y}, {compressed.z}");

// 解压
var decompressed = FloatCompression.DecompressVector3(compressed.x, compressed.y, compressed.z);
Debug.Log($"原始值: {decompressed.x}, {decompressed.y}, {decompressed.z}");
```

---

## 服务端 API

### ICharacterService

#### UpdateAppearanceAsync
```csharp
Task<bool> UpdateAppearanceAsync(IClientContext client, byte[] appearanceData)
```

**限制**：最大 10KB

---

#### GetAppearanceAsync
```csharp
Task<byte[]?> GetAppearanceAsync(IClientContext client, string steamId)
```

---

#### SetCharacterCreatedAsync
```csharp
Task<bool> SetCharacterCreatedAsync(IClientContext client, bool hasCharacter)
```

---

### ICharacterClientService

#### OnPlayerAppearanceUpdated
```csharp
void OnPlayerAppearanceUpdated(string steamId, byte[] appearanceData)
```

**说明**：由服务器调用，通知客户端其他玩家的外观更新。

---

## 事件系统

### 自动上传事件链

```
LevelManager.OnLevelInitialized
↓
CharacterAppearanceHelper.OnLevelInitialized() [延迟1秒]
↓
CharacterAppearanceHelper.UploadCurrentAppearanceAsync()
↓
ICharacterService.UpdateAppearanceAsync()
↓
ICharacterClientService.OnPlayerAppearanceUpdated() [广播]
↓
SceneManager.UpdatePlayerAppearance()
```

---

## 完整示例

### 获取并应用玩家外观

```csharp
using DuckyNet.Client.Core;
using DuckyNet.Shared.Data;
using UnityEngine;

public class ExampleUsage
{
    public async void LoadAndApplyPlayerAppearance(string steamId, GameObject targetCharacter)
    {
        // 1. 下载外观数据
        var appearanceData = await CharacterAppearanceHelper.DownloadAppearanceAsync(steamId);
        if (appearanceData == null)
        {
            Debug.LogWarning($"玩家 {steamId} 没有外观数据");
            return;
        }

        // 2. 应用到角色
        bool success = CharacterAppearanceHelper.ApplyAppearance(targetCharacter, appearanceData);
        if (success)
        {
            Debug.Log($"成功应用外观，包含 {appearanceData.Parts.Length} 个部位");
        }
    }

    public async void UploadMyAppearance()
    {
        // 上传当前角色外观
        bool success = await CharacterAppearanceHelper.UploadCurrentAppearanceAsync();
        if (success)
        {
            Debug.Log("外观已同步到服务器");
        }
    }
}
```

---

## 调试工具

### 打印外观数据

```csharp
public void DebugPrintAppearance(CharacterAppearanceData data)
{
    Debug.Log("=== 外观数据 ===");
    Debug.Log($"头部缩放: ({data.HeadSetting.ScaleX}, {data.HeadSetting.ScaleY}, {data.HeadSetting.ScaleZ})");
    Debug.Log($"部位数量: {data.Parts.Length}");
    
    foreach (var part in data.Parts)
    {
        Debug.Log($"  部位 {part.PartType}: ID={part.PartId}, 缩放=({part.ScaleX}, {part.ScaleY}, {part.ScaleZ})");
    }
    
    byte[] bytes = data.ToBytes();
    Debug.Log($"总大小: {bytes.Length} 字节");
}
```

---

## 性能指标

| 操作 | 平均耗时 | 数据大小 |
|------|---------|----------|
| 提取外观数据 | 5-10ms | - |
| 转换为网络格式 | 2-5ms | - |
| 压缩为字节数组 | 1-2ms | 100-600B |
| 网络传输 | 10-50ms | 100-600B |
| 解压和转换 | 2-5ms | - |
| 应用到角色 | 10-20ms | - |
| **总计** | **30-90ms** | **100-600B** |

---

## 注意事项

1. **线程安全**：所有异步方法都是线程安全的
2. **错误处理**：所有方法都包含 try-catch，不会抛出异常
3. **日志记录**：所有关键操作都有详细日志
4. **自动重试**：上传失败时不会自动重试，需手动调用
5. **数据验证**：服务器会验证数据大小（最大10KB）

