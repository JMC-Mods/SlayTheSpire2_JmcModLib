**🌐[ 中文 | [English](JML_Compatibility_en.md) ]**

# JML 游戏版本兼容层

JML 将已经确认的 STS2 版本成员差异集中在 `Compat` 目录和 `JmcModLib.Compat` 命名空间。JML 内部与子 MOD 应优先调用这里的类型安全方法，不要再次硬编码游戏私有字段或版本成员名称。

这不是任意反射工具的替代品。只有明确用于跨游戏版本、且能够提供稳定语义的访问才进入兼容层；配置回调、业务反射和单一版本 UI 注入仍留在所属模块。

## 当前兼容能力

| 门面 | 稳定能力 | 已封装的版本差异 |
|---|---|---|
| `ModCompat.GetKnownMods()` | 获取游戏已识别的 MOD | 0.99.1 `ModManager.AllMods`；0.103–0.108 `ModManager.Mods`；`LoadedMods` / `GetLoadedMods()` 是只返回已加载 MOD 的最后回退 |
| `ModCompat.GetLoadedMods()` / `IsLoaded()` | 获取或判断已加载 MOD | 0.99.1 `LoadedMods` / `wasLoaded`；0.103–0.108 `GetLoadedMods()` / `state` |
| `ModCompat.GetAssemblies()` | 获取 MOD 的全部托管程序集 | 0.99.1–0.107.1 `assembly`；0.108 `assemblies` |
| `ModCompat.GetManifest()` | 获取 manifest | 0.99.1–0.107.1 `ModManifest` 为 class，0.108 改为 record；`manifest` 字段未改名 |
| `ModCompat.GetPckName()` | 获取 PCK 名称 | 0.99.1–0.108 归档 DLL 均未发现该成员；`pckName` / `PckName` 是历史非归档构建和未来构建的防御性候选 |
| `ModCompat.GetManifestId/Name/Version()` | 获取 manifest 元数据 | 0.99.1–0.108 均为小写字段；PascalCase 名称是防御性回退 |
| `MultiplayerCompat.TryGetConnectionExtraInfo()` | 获取联机错误附加信息 | 0.99.1–0.107.1 `_connectionExtraInfo` 私有字段；0.108 `ConnectionExtraInfo` 公开属性 |
| `MultiplayerCompat.TryGetJoinFlowNetService()` | 获取加入流程网络服务 | 0.99.1–0.107.1 在 `Begin()` 内部创建的 `NetClientGameService?` 属性，调用前为空不代表成员缺失；0.108 构造注入的 `INetClientGameService` 属性；`_netService` 是防御性回退 |

表中的早期版本用于说明兼容候选的历史来源，不等同于当前 JML 发布包的完整支持范围；实际最低版本以发布 manifest 的 `min_game_version` 为准。

## 子 MOD 调用

```csharp
using JmcModLib.Compat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;

int loadedModCount = ModCompat.GetLoadedMods().Count;

if (MultiplayerCompat.TryGetConnectionExtraInfo(errorInfo, out ConnectionFailureExtraInfo? extraInfo))
{
    IReadOnlyCollection<string> hostOnlyMods = extraInfo.missingModsOnLocal ?? [];
}
```

所有 `Try...` 方法在当前游戏版本缺少可识别成员或读取失败时返回 `false`。调用方应保留游戏原始流程或禁用依赖该信息的增强功能，不应猜测字段布局。

## 维护约定

1. 新发现的游戏版本成员差异先确认稳定语义，再添加到 `Compat`。
2. 优先使用 JML 的 `MemberAccessor` / `MethodAccessor`，不直接散落 `FieldInfo.GetValue` 或 Harmony `AccessTools`。
3. 公共方法不暴露具体版本成员名；成员名只存在于兼容层实现和本表中。
4. 至少使用当前游戏 DLL 与仍受支持的历史 DLL 分别编译验证。
