**🌐[ 中文 | [English](JML_API_Reference_en.md) ]**

# JmcModLib STS2 API 文档

源码基准：JML `1.6.1`。本文按源码重新整理，不以旧文档为准。命名空间常用组合：

```csharp
using JmcModLib.Core;
using JmcModLib.Config;
using JmcModLib.Config.UI;
using JmcModLib.Config.Storage;
using JmcModLib.Security;
using JmcModLib.Persistence;
using JmcModLib.UI.PauseMenu;
using JmcModLib.Reflection;
using JmcModLib.Utils;
using JmcModLib.Prefabs;
using JmcModLib.Multiplayer;
```

`ExprHelper` 已位于 `JmcModLib.Utils` 命名空间，通常由 `global using JmcModLib.Utils;` 自动引入。

---

## 1. JML 整体生命周期

```mermaid
flowchart TD
    A[游戏加载 JmcModLib.dll] --> B[Bootstrap 读取 runtime.config]
    B --> C[解析依赖并加载 Runtime]
    C --> D[JmcModLib.MainFile.Initialize]
    D --> E[注册 JML 自身 / Harmony Patch]
    E --> F[子 MOD Initialize]
    F --> G[ModRegistry.Register]
    G --> H[ModContext 创建或更新]
    H --> I[默认服务启用: Logger + ConfigManager + Persistence]
    I --> J[ConfigManager/Persistence 初始化 AttributeRouter 与 handlers]
    J --> K[Register 立即 Done 或 Builder.Done]
    K --> L[ModRegistry.OnRegistered]
    L --> M[AttributeRouter.ScanAssembly]
    M --> N1[Config / UI / Button entries]
    M --> N2[Hotkey registrations]
    M --> N3[PauseMenuButton 条目]
    N1 --> O[Storage 同步并生成设置 UI]
    N2 --> P[Input Relay / Steam Backend]
    N3 --> V[暂停菜单打开时注入/刷新]
    O --> Q[用户修改设置]
    P --> R[用户触发热键]
    Q --> S[写回字段/属性 + 持久化 + 回调]
    R --> T[执行 MOD action]
    S --> U[注销/退出 Flush 与清理]
    T --> U
    V --> U
```

---

## 2. Core：注册、上下文、运行时信息

### 2.1 生命周期流程图

```mermaid
flowchart TD
    A[子 MOD 调用 Register] --> B[ResolveAssembly]
    B --> C[推断 ModId / DisplayName / Version]
    C --> D[Contexts AddOrUpdate]
    D --> E[EnsureDefaultServices]
    E --> F[ModLogger.RegisterAssembly]
    E --> G[ConfigManager.Init + JmcPersistenceManager.Init]
    G --> H{deferred?}
    H -- no --> I[Builder.Done]
    H -- yes --> J[WithDisplayName / WithVersion / WithConfigStorage / RegisterButton / RegisterPauseMenuButton]
    J --> I
    I --> K[context.IsCompleted = true]
    K --> L[OnRegistered]
    L --> M[AttributeRouter 扫描]
    M --> N[运行期 GetContext/GetTag/Unregister]
```

### 2.2 `VersionInfo`

命名空间：`JmcModLib.Core`

| 成员 | 说明 |
|---|---|
| `const string Name = "JmcModLib"` | JML 名称 |
| `const string Version = "1.6.1"` | JML 版本 |
| `string Tag` | `"[JmcModLib v1.6.1]"` |
| `GetName(Assembly? assembly = null)` | 获取指定程序集名称，JML 自身返回固定名称 |
| `GetVersion(Assembly? assembly = null)` | 获取指定程序集版本，JML 自身返回固定版本 |
| `GetTag(Assembly? assembly = null)` | 生成日志标签 |

### 2.3 `ModContext`

命名空间：`JmcModLib.Core`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Assembly` | `Assembly` | 当前 MOD 托管程序集 |
| `ModId` | `string` | 稳定 ID，通常等于 manifest `id` |
| `DisplayName` | `string` | UI 与日志显示名 |
| `Version` | `string` | 当前注册版本 |
| `IsCompleted` | `bool` | 是否已触发 `Done()` 完成注册 |
| `LoggerContext` | `string` | 传给 STS2 日志的上下文 |
| `Tag` | `string` | 形如 `[DisplayName v1.0.0]` |

### 2.4 `ModRegistry`

命名空间：`JmcModLib.Core`

| 成员 | 签名 / 默认参数 | 说明 |
|---|---|---|
| `OnRegistered` | `event Action<ModContext>?` | MOD 完成注册后触发，AttributeRouter 依赖此事件 |
| `OnUnregistered` | `event Action<ModContext>?` | MOD 注销后触发 |
| `Register` | `Register(string modId, string? displayName = null, string? version = null, Assembly? assembly = null)` | 手动 ID 注册，返回 builder |
| `Register` | `Register(bool deferredCompletion, string modId, string? displayName = null, string? version = null, Assembly? assembly = null)` | bool 控制是否延迟 Done |
| `Register` | `Register(bool deferredCompletion, object? modInfo, string? displayName = null, string? version = null, Assembly? assembly = null)` | 从匿名对象/元数据对象读取 id/name/version |
| `Register<T>` | `void Register<T>()` | 推荐入口，立即完成注册 |
| `Register<T>` | `RegistryBuilder? Register<T>(bool deferredCompletion)` | 泛型入口，支持延迟 builder |
| `Register<T>` | `RegistryBuilder Register<T>(string modId, string? displayName = null, string? version = null)` | 指定 ID 的泛型入口 |
| `Register<T>` | `RegistryBuilder? Register<T>(bool deferredCompletion, string modId, string? displayName = null, string? version = null)` | 指定 ID 且可延迟 |
| `IsRegistered` | `bool IsRegistered(Assembly? assembly = null)` | 判断 Assembly 是否注册 |
| `TryGetContext` | `bool TryGetContext(out ModContext? context, Assembly? assembly = null)` | 尝试获取上下文 |
| `GetContext` | `ModContext? GetContext(Assembly? assembly = null)` | 获取上下文 |
| `GetModId` | `string GetModId(Assembly? assembly = null)` | 获取 ID，未注册时回退 Assembly |
| `GetDisplayName` | `string GetDisplayName(Assembly? assembly = null)` | 获取显示名 |
| `GetVersion` | `string GetVersion(Assembly? assembly = null)` | 获取版本 |
| `GetTag` | `string GetTag(Assembly? assembly = null)` | 获取日志标签 |
| `Unregister` | `bool Unregister(Assembly? assembly = null)` | 注销上下文，并触发清理 |

推荐：普通子 MOD 使用 `Register<MainFile>()`；共享 helper 或跨 Assembly 操作时显式传 `Assembly`。

### 2.5 `RegistryBuilder`

命名空间：`JmcModLib.Core`

| 方法 | 默认参数 | 说明 |
|---|---|---|
| `WithDisplayName(string displayName)` | 无 | 覆盖显示名 |
| `WithVersion(string version)` | 无 | 覆盖版本 |
| `WithConfigStorage(IConfigStorage storage)` | 无 | 在扫描前设置自定义存储 |
| `RegisterButton(out string key, string description, Action action, string buttonText = "按钮", string group = ConfigAttribute.DefaultGroup, string? storageKey = null, string? helpText = null, string? locTable = null, string? displayNameKey = null, string? helpTextKey = null, string? buttonTextKey = null, string? groupKey = null, int order = 0, UIButtonColor color = UIButtonColor.Default)` | 见签名 | 注册手动按钮并返回 key |
| `RegisterButton(string description, Action action, ...)` | 同上 | 注册手动按钮，不取 key |
| `RegisterPauseMenuButton(string key, string text, Action<PauseMenuButtonContext> action, int order = 0, PauseMenuButtonAnchor anchor = PauseMenuButtonAnchor.BeforeExitActions, string? locTable = null, string? textKey = null, Func<PauseMenuButtonContext, bool>? visibleWhen = null, Func<PauseMenuButtonContext, bool>? enabledWhen = null, bool closeMenuOnClick = false, UIButtonColor color = UIButtonColor.Default)` | 另有无参和异步 overload，见暂停菜单章节 | 在当前注册链中声明运行内暂停菜单按钮 |
| `Done()` | 无 | 完成注册并触发 Attribute 扫描 |

`Done()` 可重复调用，第一次触发生命周期，之后只返回现有 context。

### 2.6 `ModRuntime`

命名空间：`JmcModLib.Core`

| 方法 | 说明 |
|---|---|
| `TryGetLoadedMod(Assembly? assembly = null)` | 查找 STS2 已加载的 `Mod` |
| `TryGetManifest(Assembly? assembly = null)` | 查找当前 Assembly 对应 manifest |
| `GetManifestId(Assembly? assembly = null)` | manifest `id` |
| `GetPckName(Assembly? assembly = null)` | pck 名，失败回退 Assembly 名 |
| `GetDisplayName(Assembly? assembly = null)` | manifest name 或 Assembly 名 |
| `GetLoadedVersion(Assembly? assembly = null)` | manifest version 或 Assembly version |
| `FindModById(string modId)` | 按 manifest id 精确查找 |
| `FindLoadedMod(string modId)` | 按 id/pck/name/assembly 名查找 |

---

## 3. Bootstrap / Runtime 加载

### 3.1 生命周期流程图

```mermaid
flowchart TD
    A[STS2 加载 JmcModLib.dll] --> B[BootstrapMain.Initialize]
    B --> C[定位 mod 目录]
    C --> D[读取 JmcModLib.runtime.config]
    D --> E[Normalize runtimeAssembly / initializer / probe dirs]
    E --> F[Install Assembly Resolver]
    F --> G[加载 dependencies]
    G --> H[加载 JmcModLib.Runtime.dll]
    H --> I[反射查找 initializerType + initializerMethod]
    I --> J[调用 JmcModLib.MainFile.Initialize]
```

### 3.2 `BootstrapMain`

命名空间：Bootstrap 项目中独立 assembly。对普通子 MOD 来说不需要直接调用。

| 方法 | 说明 |
|---|---|
| `Initialize()` | 游戏加载 JML Bootstrap 后调用，负责加载 Runtime |

发布目录关键文件：

```text
JmcModLib.dll                 # Bootstrap，游戏 manifest 的 dll
JmcModLib.Runtime.dll         # 子 MOD 引用的 Runtime
JmcModLib.Runtime.xml         # IntelliSense XML
JmcModLib.Sts2.props          # 子 MOD MSBuild 引用入口
JmcModLib.runtime.config      # Runtime 加载描述
Newtonsoft.Json.dll           # 依赖
JmcModLib.pck                 # Godot 资源包
JmcModLib.json                # JML manifest
```

`JmcModLib.runtime.config` 当前核心字段：

```json
{
  "runtimeAssembly": "JmcModLib.Runtime.dll",
  "initializerType": "JmcModLib.MainFile",
  "initializerMethod": "Initialize",
  "probeDirectories": [".", "lib", "libs"],
  "dependencies": ["Newtonsoft.Json.dll"],
  "probeAllDlls": true
}
```

### 3.3 JML Dispatch 多版本 DLL 分派

JML 发布目录提供 `JmcModLib.Dispatch.targets`。子 MOD 导入后，可以生成零 JML 运行时依赖的同名 Bootstrap DLL，并根据当前 STS2 版本加载 `runtimes/<version>/` 下的 Runtime DLL。

完整使用方式见[多版本 DLL 分派](JML_Dispatch.md)。

---

## 4. AttributeRouter：Attribute 扫描与扩展

### 4.1 生命周期流程图

```mermaid
flowchart TD
    A[ConfigManager.Init 或手动 Init] --> B[AttributeRouter.Init]
    B --> C[订阅 ModRegistry.OnRegistered/OnUnregistered]
    C --> D[注册 IAttributeHandler]
    D --> E[MOD Done 后 OnRegistered]
    E --> F[ScanAssembly]
    F --> G[遍历 TypeAccessor]
    F --> H[遍历 MethodAccessor]
    F --> I[遍历 MemberAccessor]
    G --> J[读取 Attributes]
    H --> J
    I --> J
    J --> K[按 Attribute 精确类型查找 handlers]
    K --> L[handler.Handle]
    L --> M[记录 handler/accessor]
    M --> N[AssemblyScanned]
    C --> O[OnUnregistered]
    O --> P[UnscanAssembly]
    P --> Q[调用 handler.Unregister]
    Q --> R[AssemblyUnscanned]
```

### 4.2 `AttributeRouter`

命名空间：`JmcModLib.Core.AttributeRouter`

| 成员 | 说明 |
|---|---|
| `AssemblyScanned` | 扫描完成事件 |
| `AssemblyUnscanned` | 反扫描/注销完成事件 |
| `IsInitialized` | 是否初始化 |
| `Init()` | 订阅注册生命周期 |
| `Dispose()` | 退订事件、unscan 已扫描 Assembly、清理 handlers |
| `RegisterHandler<TAttribute>(IAttributeHandler handler)` | 注册 handler |
| `RegisterHandler<TAttribute>(Action<Assembly, ReflectionAccessorBase, TAttribute> action)` | 注册简单 action handler |
| `UnregisterHandler(IAttributeHandler handler)` | 移除 handler，但不自动 unscan 既有记录 |
| `ScanAssembly(Assembly assembly)` | 扫描 Assembly |
| `UnscanAssembly(Assembly assembly)` | 执行 handler unregister 并清理记录 |

### 4.3 `IAttributeHandler`

| 成员 | 说明 |
|---|---|
| `Handle(Assembly assembly, ReflectionAccessorBase accessor, Attribute attribute)` | 处理发现的 Attribute |
| `Unregister` | 可选清理回调，参数为 Assembly 与该 handler 处理过的 accessor 列表 |

### 4.4 `SimpleAttributeHandler<TAttribute>`

构造：

```csharp
new SimpleAttributeHandler<TAttribute>(Action<Assembly, ReflectionAccessorBase, TAttribute> action)
```

它只处理注册类型 `TAttribute`，`Unregister` 当前为 `null`。适合轻量扩展，不适合需要卸载清理的扩展。

---

## 5. Config：配置、存储、配置项

### 5.1 生命周期流程图

```mermaid
flowchart TD
    A[ConfigManager.Init] --> B[注册 Config/UIButton/JmcHotkey/UIHotkey handlers]
    B --> C[AttributeRouter 扫描成员]
    C --> D{Attribute 类型}
    D -- Config --> E[BuildConfigEntry]
    D -- UIButton --> F[BuildButtonEntry]
    D -- UIHotkey --> G[RegisterConfig + RegisterHotkey]
    E --> H[RegisterEntry]
    F --> H
    G --> H
    H --> I[SyncFromStorage]
    I --> J{配置文件已有值?}
    J -- yes --> K[Convert 并写回字段/属性]
    J -- no --> L[保存默认值]
    K --> M[注册 UI / 输入 action]
    L --> M
    M --> N[用户修改 UI 或 SetValue]
    N --> O[ConfigEntry.SetValue]
    O --> P[setter 写回源成员]
    P --> Q[Persist/Flush]
    Q --> R[onChanged + ValueChanged]
    R --> S[退出/注销时 Flush/Unregister]
```

### 5.2 `ConfigAttribute`

命名空间：`JmcModLib.Config`

构造：

```csharp
[Config(string displayName, string? onChanged = null, string group = ConfigAttribute.DefaultGroup)]
```

| 成员 | 默认 | 说明 |
|---|---:|---|
| `DefaultGroup` | `"DefaultGroup"` | 默认分组常量 |
| `DisplayName` | 构造参数 | UI 回退显示名 |
| `OnChanged` | `null` | 静态回调方法名 |
| `Group` | `DefaultGroup` | 分组 |
| `Key` | `null` | 存储 key；为空时 Attribute 注册使用 `DeclaringType.FullName.MemberName` |
| `Description` | `null` | 描述回退文本 |
| `LocTable` | `null` | 本地化表，默认由 UI 层使用 `settings_ui` |
| `DisplayNameKey` | `null` | 显示名本地化 key |
| `DescriptionKey` | `null` | 描述本地化 key |
| `GroupKey` | `null` | 分组本地化 key |
| `Order` | `0` | 排序，越小越靠前 |
| `RestartRequired` | `false` | 是否提示需要重启/重进流程 |
| `IsValidMethod(MethodInfo method, Type valueType, out LogLevel? level, out string? errorMessage)` | 静态方法 | 校验 onChanged 回调 |

### 5.3 `ConfigManager`

命名空间：`JmcModLib.Config`

| 成员 | 默认 / 签名 | 说明 |
|---|---|---|
| `FlushOnSet` | `true` | 每次写入立即 flush |
| `AssemblyRegistered` | event | Assembly 配置项注册完成事件 |
| `AssemblyUnregistered` | event | Assembly 配置清理事件 |
| `EntryRegistered` | event | 单个配置项注册事件 |
| `ValueChanged` | event | 配置值变更事件 |
| `IsInitialized` | bool | 是否初始化 |
| `Init()` | 无 | 初始化 AttributeRouter 和默认 handlers |
| `Dispose()` | 无 | 清理所有配置注册 |
| `SetStorage(IConfigStorage storage, Assembly? assembly = null)` | assembly 自动推断 | 设置 Assembly 存储 |
| `GetStorage(Assembly? assembly = null)` | assembly 自动推断 | 获取存储，未设置则默认 Newtonsoft |
| `CreateStorageKey(Type declaringType, string memberName)` | 无 | 生成 `FullName.Member` |
| `CreateKey(string storageKey, string group = ConfigAttribute.DefaultGroup)` | 默认组 | 生成 `group.storageKey` |
| `Flush(Assembly? assembly = null)` | assembly 自动推断 | 刷盘 |
| `GetEntries(Assembly? assembly = null)` | assembly 自动推断 | 获取并按 order/displayName 排序 |
| `GetEntries(string group, Assembly? assembly = null)` | group 必填 | 获取指定组配置 |
| `GetGroups(Assembly? assembly = null)` | assembly 自动推断 | 获取分组名 |
| `TryGetEntry(string key, out ConfigEntry? entry, Assembly? assembly = null)` | assembly 自动推断 | 查配置项 |
| `GetValue(string key, Assembly? assembly = null)` | assembly 自动推断 | 获取值，找不到返回 null |
| `SetValue(string key, object? value, Assembly? assembly = null)` | assembly 自动推断 | 设置值，返回是否成功 |
| `ResetAssembly(Assembly? assembly = null)` | assembly 自动推断 | 重置 Assembly 全部配置到默认值 |
| `RegisterConfig<TValue>(...)` | 详见下方 | 手动注册配置 |
| `RegisterButton(...)` | 详见按钮章节 | 手动注册按钮 |
| `Unregister(Assembly? assembly = null)` | assembly 自动推断 | 清理 Assembly 配置、输入 action、存储 |

手动配置完整签名：

```csharp
string RegisterConfig<TValue>(
    string displayName,
    Func<TValue> getter,
    Action<TValue> setter,
    string group = ConfigAttribute.DefaultGroup,
    Action<TValue>? onChanged = null,
    UIConfigAttribute? uiAttribute = null,
    string? storageKey = null,
    string? locTable = null,
    string? displayNameKey = null,
    string? groupKey = null,
    string? description = null,
    string? descriptionKey = null,
    int order = 0,
    bool restartRequired = false,
    Assembly? assembly = null)
```

手动注册适合动态配置。正式 MOD 建议显式传 `storageKey`。

### 5.4 `ConfigEntry` / `ConfigEntry<TValue>`

命名空间：`JmcModLib.Config.Entry`

| 成员 | 说明 |
|---|---|
| `Assembly` | 所属 Assembly |
| `StorageKey` | 持久化 key，不含 group |
| `Group` | 分组 |
| `DisplayName` | 回退显示名 |
| `Key` | `CreateKey(StorageKey, Group)` |
| `Attribute` | 配置元数据 |
| `UIAttribute` | UI 元数据，可为空 |
| `DropdownOptionsProviderAttribute` | 动态下拉候选项提供器元数据，可为空 |
| `VisibleWhenAttribute` | 动态可见性元数据，可为空 |
| `SourceDeclaringType` | Attribute 来源类型，可为空 |
| `SourceMemberName` | Attribute 来源成员名，可为空 |
| `ValueType` | 值类型 |
| `DefaultValue` | 默认值 |
| `GetValue()` | 读取当前源值 |
| `SetValue(object? value)` | 转换并设置值 |
| `Reset()` | 重置默认值 |
| `ValueChanged` | entry 层变更事件 |
| `CreateStorageKey(Type declaringType, string memberName)` | 静态 key 生成 |
| `CreateKey(string storageKey, string group = DefaultGroup)` | 完整 key 生成 |

`ConfigEntry<TValue>` 额外成员：

| 成员 | 说明 |
|---|---|
| `DefaultValueTyped` | 强类型默认值 |
| `GetTypedValue()` | 强类型读取 |
| `SetTypedValue(TValue value)` | 强类型设置 |

### 5.5 `IConfigStorage`

命名空间：`JmcModLib.Config.Storage`

| 方法 | 说明 |
|---|---|
| `GetFileName(Assembly? assembly = null)` | 获取文件名 |
| `GetFilePath(Assembly? assembly = null)` | 获取完整路径 |
| `Exists(Assembly? assembly = null)` | 文件是否存在 |
| `Save(string key, string group, object? value, Assembly? assembly = null)` | 保存到缓存并标记 dirty |
| `TryLoad(string key, string group, Type valueType, out object? value, Assembly? assembly = null)` | 尝试读取并反序列化 |
| `Flush(Assembly? assembly = null)` | 写入磁盘 |

### 5.6 `NewtonsoftConfigStorage` / `JsonConfigStorage`

构造：

```csharp
new NewtonsoftConfigStorage(string? rootDirectory = null)
new JsonConfigStorage(string? rootDirectory = null)
```

两者都实现 `IConfigStorage`。默认 root 为空时会使用 `OS.GetUserDataDir()/mods/config`。默认存储是 `NewtonsoftConfigStorage`，对复杂类型更宽容；`JsonConfigStorage` 更轻，但复杂类型兼容性需要额外验证。

### 5.7 SecretStore

命名空间：`JmcModLib.Security`

SecretStore 用于保存 API Key、Token、Webhook URL 等敏感文本。Secret 会像配置项一样出现在 JML 设置页，但它不是普通 Config：不会写入 `NewtonsoftConfigStorage` / `JsonConfigStorage`，不会进入普通配置 JSON，也不通过 `IConfigStorage` 持久化。设置页只提供状态、设置/更新按钮和清空按钮。

#### Attribute 声明

```csharp
using JmcModLib.Security;

[Secret(
    "llm.api_key",
    Group = "secrets",
    DisplayNameKey = "EXTENSION.MYMOD.SECRET.api_key.NAME",
    DescriptionKey = "EXTENSION.MYMOD.SECRET.api_key.DESCRIPTION",
    SetButtonTextKey = "EXTENSION.MYMOD.SECRET.api_key.SET_BUTTON",
    ClearButtonTextKey = "EXTENSION.MYMOD.SECRET.api_key.CLEAR_BUTTON",
    GroupKey = "EXTENSION.MYMOD.GROUP.secrets",
    Order = 10)]
internal static readonly JmcSecretSlot ApiKey = new();
```

如果同一个 Secret 键需要按服务商、账号或环境分隔，声明一个同类型静态无参 `string` 方法或属性，并通过 `ScopeProvider` 指向它：

```csharp
[Secret("llm.api_key", ScopeProvider = nameof(GetProviderScope))]
internal static readonly JmcSecretSlot ApiKey = new();

private static string GetProviderScope() => CurrentProvider;
```

#### Builder 注册

```csharp
ModRegistry.Register<MainFile>(true)?
    .RegisterSecret(
        out JmcSecretSlot apiKey,
        "llm.api_key",
        new JmcSecretOptions
        {
            Group = "secrets",
            DisplayName = "API Key",
            Description = "用于请求当前服务商。",
            ScopeProvider = () => CurrentProvider,
            Order = 10
        })
    .Done();
```

也可以传入调用方已有的 `JmcSecretSlot`：

```csharp
private static readonly JmcSecretSlot ApiKey = new();

ModRegistry.Register<MainFile>(true)?
    .RegisterSecret(ApiKey, "llm.api_key", new JmcSecretOptions { Group = "secrets" })
    .Done();
```

#### 读取、保存与删除

普通业务代码优先通过槽位读取：

```csharp
if (!ApiKey.TryRead(out string apiKey, out JmcSecretReadStatus status))
{
    ModLogger.Warn($"API Key 不可用：{status}");
    return;
}

// 使用后尽快丢弃 apiKey；不要写日志、异常、状态栏或剪贴板。
```

高级场景可用静态入口：

```csharp
bool ok = JmcSecretStore.TryRead(
    "llm.api_key",
    out string value,
    out JmcSecretReadStatus status,
    scope: CurrentProvider);
```

公开 API：

| 类型 / 成员 | 说明 |
|---|---|
| `SecretAttribute` | 声明静态 `JmcSecretSlot` 字段或属性；支持显示名、描述、按钮文本、分组、动态 scope、弱保护开关和排序 |
| `JmcSecretSlot` | 子 MOD 持有的槽位句柄；提供 `TryRead`、`TrySave`、`TryDelete`、`Exists`、`ProtectionLevel` |
| `JmcSecretOptions` | 手动注册 Secret 时使用的显示、本地化、scope 与弱保护选项 |
| `JmcSecretStore` | 高级静态入口；按 key/scope/assembly 读取、保存、删除和检查存在 |
| `JmcSecretProtectionLevel` | `SystemKeychain`、`UserProfileProtected`、`WeakFileProtection`、`SessionOnly`、`Unavailable` 等保护等级 |
| `JmcSecretReadStatus` | `Success`、`Missing`、`Unavailable`、`AccessDenied`、`DecryptionFailed`、`BackendError` |
| `JmcSecretWriteStatus` | `Success`、`Unavailable`、`AccessDenied`、`WeakProtectionNotAllowed`、`BackendError` |
| `RegistryBuilder.RegisterSecret(...)` | 在注册链中手动注册 Secret 设置行 |

#### 平台保护等级

| 平台 / 条件 | 第一版行为 |
|---|---|
| Windows | 使用 current-user DPAPI，保护等级为 `UserProfileProtected` |
| 非 Windows 默认 | 返回 `Unavailable` 或 `WeakProtectionNotAllowed`，不抛未处理异常 |
| 显式 `AllowWeakFileProtection = true` | 可使用弱保护文件保存，保护等级为 `WeakFileProtection` |

弱保护文件保存只会尽量收紧文件权限，并不等于安全加密。不要把它用于共享设备或高价值密钥；文档、UI 和日志都应明确提示风险。无论哪种后端，业务代码都不要记录 Secret 明文，读取到的 `string` 也无法在 .NET 中真正清零。

### 5.8 Persistence：非配置持久化

命名空间：`JmcModLib.Persistence`

Persistence 是独立于 `ConfigManager` 的数据持久化模块，用于保存“不是设置项”的数据：当前机器本地偏好、跨 profile 的全局数据、当前 profile 数据，以及当前 run 的本地非同步数据。它复用 `AttributeRouter` 扫描静态字段/属性，但不生成设置 UI。

```csharp
using JmcModLib.Persistence;

internal sealed class Stats
{
    public int TotalRuns { get; set; }
    public List<string> Notes { get; set; } = [];
}

internal sealed class RunState
{
    public int RoomsVisited { get; set; }
}

internal sealed class PanelState
{
    public string LastTab { get; set; } = "overview";
    public bool IsCollapsed { get; set; }
}

internal sealed class ClientRunUiState
{
    public bool OverlayPinned { get; set; }
    public ulong? LockedNetId { get; set; }
}

internal static class DemoPersistence
{
    [JmcLocalPreference("ui.panel_state")]
    internal static readonly JmcDataSlot<PanelState> PanelState = new(new PanelState());

    [JmcClientRunData("ui.client_overlay_state")]
    internal static readonly JmcRunDataSlot<ClientRunUiState> ClientOverlayState = new(new ClientRunUiState());

    [JmcGlobalData("stats.global_launches")]
    internal static int GlobalLaunches;

    [JmcProfileData("stats")]
    internal static readonly JmcDataSlot<Stats> Stats = new(new Stats());

    [JmcProfileData("stats.total_runs")]
    internal static int TotalRuns;

    [JmcRunData("run_state")]
    internal static readonly JmcRunDataSlot<RunState> RunState = new(new RunState());

    public static void RecordRunStart()
    {
        GlobalLaunches++;
        TotalRuns++;
        Stats.Modify(static stats => stats.TotalRuns++);
        JmcPersistenceManager.Flush();
    }

    public static void TogglePanel()
    {
        PanelState.Modify(static state =>
        {
            state.IsCollapsed = !state.IsCollapsed;
            state.LastTab = state.IsCollapsed ? "compact" : "overview";
        });
    }

    public static void ToggleClientOverlay()
    {
        ClientOverlayState.Modify(static state =>
        {
            state.OverlayPinned = !state.OverlayPinned;
            state.LockedNetId = 123;
        });
    }

    public static void RecordRoomVisited()
    {
        RunState.Modify(static state => state.RoomsVisited++);
    }
}
```

生命周期：

```mermaid
flowchart TD
    A[ModRegistry.EnsureDefaultServices] --> B[JmcPersistenceManager.Init]
    B --> C[注册 LocalPreference/Global/Profile/ClientRun/Run Attribute handlers]
    C --> D[AttributeRouter 扫描 static 字段/属性]
    D --> E{数据范围}
    E -- LocalPreference --> F[local-preferences.v1.json]
    E -- Global --> G[account scoped global.v1.json]
    E -- Profile --> H[当前 profile profile.v1.json]
    E -- ClientRun --> I[client-runs profile/run sidecar]
    E -- Run --> N[当前 run save 根节点 _jml]
    H --> J[切 profile 前 flush]
    J --> K[切 profile 后 reload]
    I --> L[保存退出保留 SL 不回滚]
    N --> M[RunSaveManager 保存/读取时合并扩展文档]
    N --> O[CanonicalizeSave 后保留 _jml]
```

公开 API：

| 类型 / 成员 | 说明 |
|---|---|
| `JmcLocalPreferenceAttribute` | 声明当前机器本地偏好数据；不随 profile 切换，不进入 run save，不参与云同步或多人同步 |
| `JmcGlobalDataAttribute` | 声明账号范围数据，不随 profile 切换 |
| `JmcProfileDataAttribute` | 声明当前 profile 范围数据，切 profile 时 flush/reload |
| `JmcClientRunDataAttribute` | 声明当前客户端、当前 run 生命周期数据；写入本地 sidecar，不进入 run save，run 结束、放弃、删除或开启新 run 时清理 |
| `JmcRunDataAttribute` | 声明当前 run 本地非同步数据，写入 run save 的 `_jml` 扩展文档 |
| `JmcDataSlot<T>` | local/global/profile 槽位；提供 `IsBound`、`Key`、`Value`、`SetValue(T)`、`Modify(Action<T>)` |
| `JmcRunDataSlot<T>` | run/client-run 槽位；无 run 上下文时读取默认值，写入返回失败结果 |
| `JmcDataWritePolicy` | `WhenChanged` 或 `Always` |
| `JmcDataWriteResult` | `SetValue` / `Modify` 的结果，包含 `Success` 和 `Message` |
| `JmcPersistenceManager.Init()` | 初始化 Attribute handler，通常由 `ModRegistry` 自动调用 |
| `JmcPersistenceManager.Flush(Assembly? assembly = null)` | 刷新当前 MOD 的 local/global/profile/client-run 数据 |
| `JmcPersistenceManager.FlushLocalPreferences(Assembly? assembly = null)` | 只刷新当前 MOD 的本地偏好数据 |
| `JmcPersistenceManager.FlushClientRunData(Assembly? assembly = null)` | 只刷新当前 MOD 的客户端本局数据 |
| `JmcPersistenceManager.FlushAll()` | 刷新所有已注册 MOD 的 local/global/profile/client-run 数据 |

Attribute 参数：

| 参数 | 默认 | 说明 |
|---|---:|---|
| `key` | 必填 | 当前 MOD 内稳定 key。进入 JSON 属性名前会做安全化处理 |
| `SchemaVersion` | `1` | 写入文档，第一阶段不自动迁移 |
| `WritePolicy` | `WhenChanged` | `WhenChanged` 仅变化时写入，`Always` 每次 flush 写入 |

使用建议：

- Slot 适合复杂对象；引用类型内部变化必须通过 `Modify` 包裹，或修改后调用 `SetValue`。
- 裸静态字段/属性适合 `int`、`bool`、`string`、`enum` 和简单 JSON 对象。裸静态值不会在直接赋值时即时 dirty，只会在 flush / 保存边界读取当前值。`JmcClientRunData` 不支持裸静态值，必须使用 `JmcRunDataSlot<T>`。
- LocalPreference 数据保存到 `OS.GetUserDataDir()/mods/persistence/<modId>/local-preferences.v1.json`，直接本地写盘，不走 `SaveManager`，适合 UI 面板状态、排序、折叠、窗口位置、上次打开页签等不影响玩法结果的数据。
- LocalPreference 的 `JmcDataSlot<T>.SetValue` / `Modify` 会立即调用 `FlushLocalPreferences()`；裸静态本地偏好至少会在显式 `FlushLocalPreferences()`、`Flush()`、MOD 注销或进程退出时写盘。
- ClientRun 数据保存到 `OS.GetUserDataDir()/mods/persistence/<modId>/client-runs/<profileId>/<runIdentity>.v1.json`，直接本地写盘，不走 `SaveManager`、run save 或 cloud save store。Slot 写入会立即刷新；保存退出后可读回，加载旧 run save 不会回滚，run 结束、放弃、删除或开启新 run 时清理。
- Global 数据保存到账号范围 `mods/persistence/<modId>/global.v1.json`。
- Profile 数据保存到当前 profile 范围 `mods/persistence/<modId>/profile.v1.json`。
- Run 数据第一阶段不参与多人同步、重连同步、回放或一致性校验，只在本地 run save 的 `_jml` 根扩展文档中保存；JML 会保留未知 MOD 的 `_jml` 数据，并在原版 `RunManager.CanonicalizeSave` 生成新 `SerializableRun` 后继续携带扩展文档。
- Run save 写入不跳过原版 `RunSaveManager.SaveRun`，而是在原版保存成功后追加 `_jml`；原版异常会继续向外传播，JML 附加写入失败只记录 warning。若 JML 或子 MOD 卸载，已存在的 `_jml` 是按 MOD ID 分区的惰性扩展 JSON，不会参与原版玩法逻辑。

---

## 6. Config UI：设置界面 Attribute

### 6.1 生命周期流程图

```mermaid
flowchart TD
    A[字段/属性带 Config + UIConfigAttribute] --> B[AttributeRouter 扫描]
    B --> C[ConfigManager.BuildConfigEntry]
    C --> D[ValidateUiAttribute]
    D --> E[ConfigEntry 注册]
    E --> F[ModSettingsTabBridge 注入 Settings Tab]
    F --> G[ModSettingsPanel 读取 entries/groups]
    G --> H{UIAttribute 类型}
    H -- UIToggle --> I[Tickbox]
    H -- UIInput --> J[Input]
    H -- UISlider --> K[Slider]
    H -- UIDropdown --> L[Dropdown]
    H -- UIKeybind --> M[Keybind Button]
    H -- UIColor --> N[Color Picker]
    I --> O[用户修改]
    J --> O
    K --> O
    L --> O
    M --> O
    N --> O
    O --> P[ConfigManager.SetValue]
    P --> Q[写回 + 持久化 + 刷新 UI]
```

### 6.2 `UIButtonAttribute`

构造：

```csharp
[UIButton(string description, string buttonText = "按钮", string group = ConfigAttribute.DefaultGroup)]
```

| 属性 | 默认 | 说明 |
|---|---:|---|
| `Description` | 构造参数 | 行显示名/描述 |
| `ButtonText` | `"按钮"` | 按钮文本 |
| `Group` | `DefaultGroup` | 分组 |
| `Key` | `null` | 存储 key，空时从方法名推导 |
| `LocTable` | `null` | 本地化表 |
| `DisplayNameKey` | `null` | 显示名 key |
| `DescriptionKey` | `null` | 帮助文本 key |
| `ButtonTextKey` | `null` | 按钮文本 key |
| `GroupKey` | `null` | 分组 key |
| `Color` | `UIButtonColor.Default` | 按钮颜色 |
| `Order` | `0` | 排序 |
| `HelpText` | `null` | 悬停帮助文本 |
| `IsValidMethod` | 静态 | 要求静态无参；返回值非 void 会警告 |

### 6.3 UI Attribute 总表

| Attribute | 构造 / 默认 | 支持类型 | 说明 |
|---|---|---|---|
| `UIConfigAttribute` | 抽象基类 | 任意 | UI 元数据基类 |
| `UIConfigAttribute<TValue>` | 抽象泛型基类 | 精确 `TValue` | 自动校验值类型 |
| `UIToggleAttribute` | 无 | `bool` | 勾选框 |
| `UIKeybindAttribute` | `(bool allowController = false, bool allowKeyboard = true)` | `Godot.Key` 或 `JmcKeyBinding` | 按键绑定；手柄要求 `JmcKeyBinding`，如无必要建议使用`JKB` |
| `UIInputAttribute` | `(int characterLimit = 0, bool multiline = false)` | `string` | 文本输入 |
| `UIColorAttribute` | `(params string[] presets)` | `Godot.Color` | 颜色选择，默认 `Palette=Game`、`AllowCustom=true`、`AllowAlpha=true` |
| `UISliderAttribute` | `(double min, double max, double step = 1.0)` | 数字类型 | 通用数字滑条 |
| `UIIntSliderAttribute` | `(int min, int max, int characterLimit = 5)` | `int` | int 滑条 |
| `UIDropdownAttribute` | `(params string[]? exclude)` | `string` 或 enum | string 用作选项；enum 用作排除项 |
| `UIDropdownOptionsProviderAttribute` | `(string providerName, params string[] dependsOn)` | `string` 或 enum 下拉 | 为下拉项提供运行时动态候选项，并声明依赖项变化后刷新 |
| `UIVisibleWhenAttribute` | `(string dependsOn)` / `(string dependsOn, bool/string/int/double expectedValue)` | 任意配置项 | 根据同 MOD 内其他配置项的当前值动态显示或隐藏当前配置项 |

枚举：

```csharp
public enum UIButtonColor { Default, Green, Red, Gold, Blue, Reset }
public enum UIColorPalette { None, Basic, Game, CardRarity, Rainbow }
public enum UIDropdownInvalidValuePolicy { KeepCurrent, SelectFirstAvailable, ResetToDefault }
```

接口：

```csharp
public interface ISliderConfigAttribute
{
    double Min { get; }
    double Max { get; }
    double Step { get; }
}

public interface IConfigUiContext
{
    T Get<T>(string key);
    bool TryGet<T>(string key, out T value);
    object? Get(string key);
    bool TryGet(string key, out object? value);
}
```

动态下拉示例：

```csharp
[UIDropdown("Offense", "Defense")]
[Config("模式", Key = "dropdown.mode")]
public static string Mode = "Offense";

[UIDropdown]
[UIDropdownOptionsProvider(
    nameof(GetChoiceOptions),
    nameof(Mode),
    InvalidValuePolicy = UIDropdownInvalidValuePolicy.SelectFirstAvailable)]
[Config("选项", Key = "dropdown.choice")]
public static string Choice = "Strike";

private static IReadOnlyList<string> GetChoiceOptions(IConfigUiContext ctx)
{
    return ctx.Get<string>(nameof(Mode)) == "Defense"
        ? ["Block", "Guard", "Barrier"]
        : ["Strike", "Bash", "Whirlwind"];
}
```

动态可见性示例：

```csharp
[UIToggle]
[Config("显示高级项", Key = "advanced.enabled")]
public static bool AdvancedEnabled = false;

[UIInput(64)]
[UIVisibleWhen(nameof(AdvancedEnabled))]
[Config("高级文本", Key = "advanced.text")]
public static string AdvancedText = "Only visible when enabled";

[UIDropdown("Simple", "Advanced")]
[Config("模式", Key = "advanced.mode")]
public static string Mode = "Simple";

[UIIntSlider(0, 100)]
[UIVisibleWhen(nameof(Mode), "Advanced")]
[Config("高级强度", Key = "advanced.power")]
public static int AdvancedPower = 50;
```

隐藏只影响设置 UI 行的显示状态，不会取消配置项注册，也不会自动清空、重置或停止持久化该配置值。

---

## 7. Hotkey / Input：热键与输入

### 7.1 生命周期流程图

```mermaid
flowchart TD
    A[ConfigManager.Init 注册 Hotkey handlers] --> B[AttributeRouter 扫描方法]
    B --> C{Attribute}
    C -- JmcHotkey --> D[找到 bindingMember 字段/属性]
    D --> E[创建 bindingGetter]
    E --> F[JmcHotkeyManager.Register]
    C -- UIHotkey --> G[创建 JmcKeyBinding 配置项]
    G --> H[注册 ConfigEntry]
    H --> F
    F --> I[JmcHotkeyManager.Init]
    I --> J[JmcInputManager.Initialize]
    J --> K[安装 JmcHotkeyInputRelay]
    K --> L[Godot _Input/_UnhandledInput]
    J --> M[Steam/Godot 后端每帧 Process]
    L --> N[HandleInput]
    M --> N
    N --> O[匹配 JmcKeyBinding]
    O --> P[防抖/ConsumeInput]
    P --> Q[action.Invoke]
```

### 7.2 `JmcKeyModifiers`

命名空间：`JmcModLib.Config.UI`

```csharp
[Flags]
public enum JmcKeyModifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Meta = 8
}
```

### 7.3 `JmcKeyBinding`

命名空间：`JmcModLib.Config.UI`

构造：

```csharp
new JmcKeyBinding()
new JmcKeyBinding(Key keyboard)
new JmcKeyBinding(Key keyboard = Key.None, string controller = "", JmcKeyModifiers modifiers = JmcKeyModifiers.None, bool enabled = true)
new JmcKeyBinding(Key keyboard, string controller, JmcKeyModifiers modifiers)
new JmcKeyBinding(Key keyboard, JmcKeyModifiers modifiers, bool enabled = true)
```

| 成员 | 说明 |
|---|---|
| `Keyboard` | 键盘按键，`Key.None` 表示无键盘绑定 |
| `Controller` | 手柄 action 名称 |
| `Modifiers` | 修饰键组合 |
| `Enabled` | 是否启用；默认 struct 也会视为启用 |
| `HasKeyboard` | 是否有键盘 |
| `HasModifiers` | 是否有修饰键 |
| `HasController` | 是否有手柄 action |
| `WithKeyboard(Key keyboard)` | 替换键盘并清空修饰键 |
| `WithKeyboard(Key keyboard, JmcKeyModifiers modifiers)` | 替换键盘与修饰键 |
| `WithController(string? controller)` | 替换手柄 action |
| `WithEnabled(bool enabled)` | 修改启用状态 |
| `IsPressed(InputEvent inputEvent, bool allowEcho = false, bool exactModifiers = true)` | 判断输入事件是否触发 |
| `IsReleased(InputEvent inputEvent)` | 判断释放 |
| `IsDown(bool exactModifiers = true)` | 当前是否按下 |
| `implicit operator JmcKeyBinding(Key keyboard)` | 从 Key 隐式创建 |
| `static IsPressed(Key keyboard, InputEvent inputEvent, bool allowEcho = false)` | 静态便捷方法 |
| `static IsReleased(Key keyboard, InputEvent inputEvent)` | 静态便捷方法 |
| `ToKeyboardText()` | 键盘绑定可读文本 |
| `ToString()` | 键盘/手柄组合文本 |
| `ReadModifiers(InputEventKey keyEvent)` | 从事件读取修饰键 |
| `ReadCurrentModifiers()` | 读取当前按下的修饰键 |
| `IsModifierKey(Key key)` | 是否修饰键 |
| `ReadKey(InputEventKey keyEvent)` | 读取实际 keycode |

### 7.4 `JmcHotkeyAttribute`

```csharp
[JmcHotkey(string bindingMember)]
```

| 属性 | 默认 | 说明 |
|---|---:|---|
| `BindingMember` | 构造参数 | 保存 `Key` 或 `JmcKeyBinding` 的静态字段/属性名 |
| `Key` | `null` | 热键注册 key；空时按方法名推导 |
| `ConsumeInput` | `true` | 触发后吃输入 |
| `ExactModifiers` | `true` | 是否禁止额外修饰键；`true` 表示修饰键必须完全一致，`false` 表示只要求包含配置的修饰键 |
| `AllowEcho` | `false` | 是否允许键盘长按产生的 echo 输入重复触发 |
| `DebounceMs` | `150` | 防抖毫秒 |

方法必须是静态无参；返回值会被忽略。

### 7.5 `UIHotkeyAttribute`

```csharp
[UIHotkey(string displayName, string group = ConfigAttribute.DefaultGroup)]
```

| 属性 | 默认 | 说明 |
|---|---:|---|
| `DisplayName` | 构造参数 | 设置 UI 显示名 |
| `Group` | `DefaultGroup` | 分组 |
| `Key` | `null` | 配置 key / 热键 key 基础 |
| `Description` | `null` | 描述 |
| `LocTable` | `null` | 本地化表 |
| `DisplayNameKey` | `null` | 显示名 key |
| `DescriptionKey` | `null` | 描述 key |
| `GroupKey` | `null` | 分组 key |
| `Order` | `0` | 排序 |
| `RestartRequired` | `false` | 是否提示需要重启 |
| `DefaultKeyboard` | `Key.None` | 默认键盘 |
| `DefaultModifiers` | `None` | 默认修饰键 |
| `DefaultController` | `""` | 默认手柄 action |
| `AllowKeyboard` | `true` | 允许键盘绑定 |
| `AllowController` | `false` | 允许手柄绑定 |
| `ConsumeInput` | `true` | 触发后吃输入 |
| `ExactModifiers` | `true` | 是否禁止额外修饰键；`true` 表示修饰键必须完全一致，`false` 表示只要求包含配置的修饰键 |
| `AllowEcho` | `false` | 是否允许键盘长按产生的 echo 输入重复触发 |
| `DebounceMs` | `150` | 防抖 |

### 7.6 `JmcHotkeyManager`

| 成员 | 说明 |
|---|---|
| `IsInitialized` | 热键系统是否初始化 |
| `Init()` | 初始化输入后端和注销事件 |
| `Register(string key, Func<JmcKeyBinding> bindingGetter, Action action, bool consumeInput = true, bool exactModifiers = true, bool allowEcho = false, ulong debounceMs = 150, Assembly? assembly = null)` | 注册动态绑定热键 |
| `Register(string key, Func<Key> keyGetter, Action action, bool consumeInput = true, bool exactModifiers = true, bool allowEcho = false, ulong debounceMs = 150, Assembly? assembly = null)` | 注册键盘热键 |
| `Unregister(string key, Assembly? assembly = null)` | 注销单个热键 |
| `UnregisterAssembly(Assembly? assembly = null)` | 注销 Assembly 下全部热键 |

### 7.7 `HotkeyOptions`

```csharp
public readonly record struct HotkeyOptions(
    bool ConsumeInput = true,
    bool ExactModifiers = true,
    bool AllowEcho = false,
    ulong DebounceMs = 150);
```

---

## 8. Steam Input 集成

### 8.1 生命周期流程图

```mermaid
flowchart TD
    A[配置项使用 UIKeybind AllowController=true] --> B[JmcInputActionRegistry.RegisterConfigEntry]
    B --> C[热键 handler 绑定 hotkeyKey 与 entryKey/sourceMember]
    C --> D[ActionsChanged]
    D --> E[SteamInputManifestInstaller / Patches]
    E --> F[读取原始 Steam Input manifest]
    F --> G[SteamInputManifestMerger 合并 JML actions]
    G --> H[生成/安装合并 manifest]
    H --> I[SteamInputBackend 初始化 action handles]
    I --> J[每帧轮询 digital action]
    J --> K[转换成 JmcKeyBinding controller press/release]
    K --> L[JmcHotkeyManager.HandleInput]
```

Steam Input 相关类型大多是 internal，公共 API 主要通过 `UIKeybind(allowController: true)`、`JmcKeyBinding.Controller` 与 `UIHotkey.AllowController` 间接暴露。子 MOD 不应直接依赖内部 installer/merger。

---

## 9. Logger：日志

### 9.1 生命周期流程图

```mermaid
flowchart TD
    A[ModRegistry.Register] --> B[ModLogger.RegisterAssembly]
    B --> C[创建 AssemblyLogConfiguration]
    C --> D[读取 ModRegistry LoggerContext]
    D --> E[MOD 调用 Debug/Info/Warn/Error/Fatal]
    E --> F[添加 Prefix / Exception Details]
    F --> G[转发 STS2 Logger]
    G --> H[由 STS2 原生等级决定是否显示]
    H --> I{Fatal && ThrowOnFatal?}
    I -- yes --> J[抛出异常]
    I -- no --> K[结束]
    B --> M[UnregisterAssembly 时清理]
```

JML 不维护最低显示等级。需要调整日志显示时，使用 STS2 开发者控制台的原生命令，例如 `log Debug` 或 `log Generic Debug`。

### 9.2 类型与成员

命名空间：`JmcModLib.Utils`

```csharp
[Flags]
public enum LogPrefixFlags
{
    None = 0,
    Timestamp = 1,
    Default = Timestamp
}
```

`AssemblyLogConfiguration`：

| 属性 | 默认 |
|---|---:|
| `LogType` | `LogType.Generic` |
| `PrefixFlags` | `LogPrefixFlags.Default` |
| `ThrowOnFatal` | `true` |
| `IncludeExceptionDetails` | `true` |

`LoggerSnapshot`：

```csharp
public readonly record struct LoggerSnapshot(
    LogType LogType,
    LogPrefixFlags PrefixFlags,
    bool ThrowOnFatal,
    bool IncludeExceptionDetails,
    string Context);
```

`ModLogger`：

| 成员 | 说明 |
|---|---|
| `DefaultLogType` | 默认 Generic |
| `DefaultPrefixFlags` | 默认 Timestamp |
| `DefaultThrowOnFatal` | 默认 true |
| `DefaultIncludeExceptionDetails` | 默认 true |
| `RegisterAssembly(Assembly? assembly = null, LogPrefixFlags prefixFlags = LogPrefixFlags.Default, bool throwOnFatal = true, LogType logType = LogType.Generic, bool includeExceptionDetails = true)` | 注册 Assembly 日志配置 |
| `UnregisterAssembly(Assembly? assembly = null)` | 清理日志配置 |
| `GetLogType/SetLogType` | 读取/设置 STS2 日志类型 |
| `GetPrefixFlags/SetPrefixFlags` | 读取/设置前缀 |
| `HasPrefixFlag/TogglePrefixFlag` | 检查/切换前缀 flag |
| `GetSnapshot` | 获取当前配置快照 |
| `Load/Trace/Debug/Info/Warn/Error/Fatal` | 日志输出方法 |
| `Warn(string message, Exception exception, Assembly? assembly = null)` | 带异常 warn |
| `Error(string message, Exception exception, Assembly? assembly = null)` | 带异常 error |
| `Fatal(Exception exception, string? message = null, Assembly? assembly = null)` | fatal，可能抛出 |

---

## 10. L10n：本地化

### 10.1 生命周期流程图

```mermaid
flowchart TD
    A[调用 L10n.Resolve / ConfigLocalization] --> B[确定 table/key]
    B --> C{key 是否包含 table/key?}
    C -- yes --> D[拆分 table 与 key]
    C -- no --> E[使用 DefaultTable]
    D --> F[LocString.Exists]
    E --> F
    F -- false --> G[返回 fallback]
    F -- true --> H[LocString.GetFormattedText]
    H --> I[返回文本]
    A --> J[TryResolveForLanguage]
    J --> K[读取 res://<pck>/localization/<lang>/<table>.json]
    K --> L[当前语言失败则回退 eng]
```

### 10.2 `L10n`

命名空间：`JmcModLib.Utils`

| 成员 | 说明 |
|---|---|
| `FallbackLanguage = "eng"` | 回退语言 |
| `DefaultTable = "settings_ui"` | 默认表 |
| `SupportedLanguages` | STS2 支持语言列表 |
| `CurrentLanguage` | 当前语言，失败回退 `eng` |
| `GetModLocalizationRoot(Assembly? assembly = null)` | `res://<pck>/localization` |
| `GetModLocalizationDirectory(string? language = null, Assembly? assembly = null)` | 指定语言目录 |
| `GetModTablePath(string fileName, string? language = null, Assembly? assembly = null)` | 表文件路径，自动补 `.json` |
| `HasModTable(string fileName, string? language = null, Assembly? assembly = null)` | 资源是否存在 |
| `EnumerateExistingModTablePaths(string fileName, Assembly? assembly = null)` | 当前语言与 fallback 路径 |
| `Create(string table, string key, Action<LocString>? configure = null)` | 创建 `LocString` |
| `CreateIfExists(string table, string key, Action<LocString>? configure = null)` | 存在则创建 |
| `Exists(string table, string key)` | key 是否存在 |
| `TryGetFormattedText(string table, string key, out string? text, Action<LocString>? configure = null, Assembly? assembly = null)` | 尝试格式化 |
| `Resolve(string? key, string? fallback = null, string? table = null, Assembly? assembly = null, Action<LocString>? configure = null)` | 解析文本，失败返回 fallback/空串 |
| `ResolveAny(IEnumerable<string?> keys, string? fallback = null, string? table = null, Assembly? assembly = null, Action<LocString>? configure = null)` | 多 key fallback |
| `ResolvePath(string? path, string? fallback = null, Assembly? assembly = null, Action<LocString>? configure = null)` | 使用默认表解析 |
| `TryResolve(string? key, out string text, string? table = null, Assembly? assembly = null, Action<LocString>? configure = null)` | 尝试解析 |
| `GetFormattedText(string table, string key, Action<LocString>? configure = null)` | 直接格式化 |
| `GetRawText(string table, string key)` | 原始文本 |
| `SubscribeToLocaleChange(LocManager.LocaleChangeCallback callback)` | 订阅语言切换 |
| `UnsubscribeToLocaleChange(LocManager.LocaleChangeCallback callback)` | 取消订阅 |

---

## 11. Reflection：反射访问器

### 11.1 生命周期流程图

```mermaid
flowchart TD
    A[调用 Type/Member/MethodAccessor.Get] --> B[检查缓存]
    B -- hit --> C[返回 accessor]
    B -- miss --> D[反射查找 MemberInfo]
    D --> E[校验支持类型]
    E --> F[构建动态方法/表达式/委托]
    F --> G[缓存 accessor]
    G --> H[GetValue/SetValue/Invoke]
    H --> I[AttributeRouter 使用 GetAll 扫描]
```

### 11.2 `ReflectionAccessorBase`

命名空间：`JmcModLib.Reflection`

| 成员 | 说明 |
|---|---|
| `DefaultFlags` | `Instance | Static | Public | NonPublic` |
| `Name` | 成员名 |
| `DeclaringType` | 声明类型 |
| `IsStatic` | 是否静态 |
| `IsSaveOwner(Type? declaringType)` | 判断类型是否适合作为 owner |
| `GetAttribute<T>()` | 获取单个 Attribute |
| `HasAttribute<T>()` | 是否有 Attribute |
| `GetAttributes(Type? type = null)` | 获取 Attribute |
| `GetAllAttributes()` | 获取全部 Attribute |

泛型基类 `ReflectionAccessorBase<TMemberInfo,TAccessor>`：

| 成员 | 说明 |
|---|---|
| `CacheCount` | 缓存数量 |
| `ClearCache()` | 清理缓存 |
| `MemberInfo` | 原始 `MemberInfo` |

### 11.3 `TypeAccessor`

| 成员 | 说明 |
|---|---|
| `Type` | 原始 `Type` |
| `new TypeAccessor(Type type)` | 构造并判断静态类 |
| `Get(Type type)` | 缓存获取 |
| `Get<T>()` | 泛型获取 |
| `GetAll(Assembly asm)` | 获取 Assembly 中所有安全类型 |
| `CreateInstance()` | 无参构造实例，失败返回 null 并日志 |
| `CreateInstance(params object?[] args)` | 带参构造 |
| `CreateInstance<T>() where T : class` | 泛型创建 |

### 11.4 `MemberAccessor`

| 成员 | 说明 |
|---|---|
| `CanRead` / `CanWrite` | 是否可读/写 |
| `ValueType` | 字段/属性类型 |
| `MemberType` | Field/Property |
| `TypedGetter` / `TypedSetter` | 强类型委托，索引器/ref-like 等可能为空 |
| `GetValue(object? target)` | 读非索引成员 |
| `SetValue(object? target, object? value)` | 写非索引成员 |
| `GetValue(object? target, params object?[] indexArgs)` | 读索引器 |
| `SetValue(object? target, object? value, params object?[] indexArgs)` | 写索引器 |
| `GetValue<TTarget,TValue>(TTarget target)` | 泛型实例读取 |
| `SetValue<TTarget,TValue>(TTarget target, TValue value)` | 泛型实例写入 |
| `GetValue<TValue>()` | 静态读取 |
| `SetValue<TValue>(TValue value)` | 静态写入 |
| `Get(Type type, string memberName)` | 按名查字段/属性 |
| `GetIndexer(Type type, params Type[] parameterTypes)` | 按索引参数查索引器 |
| `Get(MemberInfo member)` | 按 MemberInfo 获取 |
| `GetAll(Type type, BindingFlags flags = DefaultFlags)` | 获取全部字段/属性 |
| `GetAll<T>(BindingFlags flags = DefaultFlags)` | 泛型获取全部 |

### 11.5 `MethodAccessor`

| 成员 | 说明 |
|---|---|
| `IsStatic` | 是否静态 |
| `TypedDelegate` | 强类型委托，部分复杂方法可能为空 |
| `Get(MethodInfo method)` | 获取 accessor |
| `GetTypedDelegate(MethodInfo method)` | 获取强类型委托 |
| `GetAll(Type type, BindingFlags flags = DefaultFlags)` | 获取全部方法 |
| `GetAll<T>(BindingFlags flags = DefaultFlags)` | 泛型获取全部方法 |
| `Get(Type type, string methodName, Type[]? parameterTypes = null)` | 按名/参数查方法 |
| `MakeGeneric(params Type[] genericTypes)` | 构造泛型方法 accessor |
| `Invoke(object? instance, params object?[] args)` | 通用调用 |
| `Invoke(object? instance)` / `Invoke(instance, a0/a1/a2)` | 0-3 参数快捷调用 |
| `Invoke<TTarget,TResult>(TTarget instance)` | 泛型实例调用 |
| `Invoke<TTarget,T1,TResult>(...)` | 1 参数泛型实例调用 |
| `Invoke<TTarget,T1,T2,TResult>(...)` | 2 参数泛型实例调用 |
| `Invoke<TTarget,T1,T2,T3,TResult>(...)` | 3 参数泛型实例调用 |
| `InvokeVoid<TTarget>(...)` | void 实例调用 |
| `InvokeVoid<TTarget,T1/T2/T3>(...)` | 1-3 参数 void 实例调用 |
| `InvokeStatic<TResult>()` | 静态返回值调用 |
| `InvokeStatic<T1,TResult>(...)` | 1 参数静态返回值调用 |
| `InvokeStatic<T1,T2,TResult>(...)` | 2 参数静态返回值调用 |
| `InvokeStatic<T1,T2,T3,TResult>(...)` | 3 参数静态返回值调用 |
| `InvokeStaticVoid()` | 静态 void 调用 |
| `InvokeStaticVoid<T1/T2/T3>(...)` | 1-3 参数静态 void 调用 |

### 11.6 `ExprHelper`

命名空间：`JmcModLib.Utils`。源码文件在 `Utils/ExprHelper.cs`，功能上仍属于反射访问辅助。

| 成员 | 说明 |
|---|---|
| `EnableCache` | 是否启用缓存，默认 true |
| `AccessMode` | 访问器生成模式，默认 `MemberAccessMode.Default` |
| `MemberAccessMode` | `Reflection` / `ExpressionTree` / `Emit` / `Default=Emit` |
| `MemberAccessors(Delegate Getter, Delegate Setter)` | 访问器 record |
| `GetOrCreateAccessors<T>(Expression<Func<T>> expr, Assembly? assembly = null)` | 从表达式获取 getter/setter |
| `GetOrCreateAccessors<T>(Expression<Func<T>> expr, out bool cacheHit, Assembly? assembly = null)` | 带 cache hit 输出 |
| `ClearAssemblyCache(Assembly? assembly = null)` | 清理指定 Assembly 缓存 |

### 11.7 `GameRestart`

命名空间：`JmcModLib.Utils`。源码文件在 `Utils/GameRestart.cs`。

`GameRestart` 用于请求游戏重启。桌面平台会调用 Godot `OS.SetRestartOnExit` 安排退出后重启，并通过游戏原生 `NGame.Quit()` 退出，以保留设置、进度和档案保存流程。Android、iOS 等 Godot 当前不支持自动重启的平台会返回 `false`，调用方应提示用户手动重启。

| 成员 | 说明 |
|---|---|
| `IsRestartSupported` | 当前平台是否支持由 JML 请求自动重启 |
| `TryScheduleRestart(bool preserveCommandLineArguments = true, Assembly? assembly = null)` | 只安排下次正常退出后重启，不主动退出 |
| `RequestRestart(bool preserveCommandLineArguments = true, Assembly? assembly = null)` | 安排重启并请求原生退出流程 |
| `ShowRestartConfirmationAsync(bool preserveCommandLineArguments = true, Assembly? assembly = null)` | 使用 JML 确认弹窗询问用户；确认后请求重启 |

```csharp
using JmcModLib.Utils;

bool requested = await GameRestart.ShowRestartConfirmationAsync(
    assembly: typeof(MainFile).Assembly);
```

---

## 12. Prefabs：弹窗

### 12.1 生命周期流程图

```mermaid
flowchart TD
    A[调用 ShowConfirmationAsync / ShowMessageAsync] --> B{NModalContainer 可用?}
    B -- no --> C[返回默认/失败结果或记录日志]
    B -- yes --> D[构造 Modal 内容]
    D --> E[解析字符串或 LocString]
    E --> F[OpenModal]
    F --> G[用户点击 Confirm/Cancel/OK]
    G --> H[TaskCompletionSource 完成]
    H --> I[调用方 await 返回]
```

### 12.2 `JmcConfirmationPopup`

命名空间：`JmcModLib.Prefabs`。源码文件在 `Prefabs/`。

| 成员 | 说明 |
|---|---|
| `IsAvailable` | 当前是否可显示 modal |
| `ShowConfirmationAsync(string title, string body, string? confirmText = null, string? cancelText = null, bool showBackstop = true, Assembly? assembly = null)` | 确认/取消弹窗，返回 bool |
| `ShowMessageAsync(string title, string body, string? okText = null, bool showBackstop = true, Assembly? assembly = null)` | OK 消息弹窗 |
| `LocString` overloads | 支持本地化字符串参数 |

### 12.3 `JmcSecretInputPopup`

命名空间：`JmcModLib.Prefabs`。源码文件在 `Prefabs/`。

`JmcSecretInputPopup` 用于输入 Secret 明文。它使用 `LineEdit.Secret = true`，不会预填旧值，也不会记录输入内容。普通子 MOD 通常不需要直接调用；通过 `[Secret]` 或 `RegisterSecret` 注册后，JML 设置页会自动使用它。

| 成员 | 说明 |
|---|---|
| `IsAvailable` | 当前是否可显示 modal |
| `PromptAsync(JmcSecretInputPopupOptions options, Assembly? assembly = null)` | 打开 Secret 输入弹窗；确认返回输入内容，取消/关闭/不可用返回 `null` |
| `JmcSecretInputPopupOptions.Title` | 弹窗标题，必填 |
| `Description` | 可选说明或风险提示 |
| `Placeholder` | 输入框占位文本 |
| `ConfirmText` / `CancelText` | 按钮文本 |
| `EmptyText` | 输入为空时的提示 |
| `ProtectionLevel` | 当前 Secret 保护等级，用于调用方组织风险说明 |
| `ShowBackstop` | 是否显示原生深色模态遮罩 |
| `MinimumSize` | 弹窗最小尺寸 |

### 12.4 `JmcReportPopup`

命名空间：`JmcModLib.Prefabs`。源码文件在 `Prefabs/`。

`JmcReportPopup` 用于显示较长的诊断报告、日志摘要或调试信息。它同样通过游戏 `NModalContainer` 打开，正文使用带裁剪边界的 `ScrollContainer` 承载游戏 `MegaRichTextLabel`，适合显示较长日志和诊断报告。正文可选择纯文本、游戏富文本或轻量 Markdown。

轻量 Markdown 会把正文先转义为游戏富文本，再渲染标题、加粗、斜体、列表、引用、分隔线、行内代码、代码块和普通链接展示；引用会显示为灰色缩进文本，并在左侧加一条竖线。普通段落与代码块里的日志行会自动识别 `[WARN]`/`WARNING:`、`[ERROR]`/`ERROR:` 等前缀，并使用与 LogConsole 接近的警告黄和错误红展示。

| 成员 | 说明 |
|---|---|
| `IsAvailable` | 当前是否可显示 modal |
| `Open(JmcReportPopupOptions options, Assembly? assembly = null)` | 打开报告弹窗，成功时返回 `JmcReportPopupHandle` |
| `JmcReportPopupOptions.Title` | 弹窗标题，必填 |
| `JmcReportPopupOptions.Body` | 正文，默认按纯文本显示 |
| `JmcReportPopupOptions.Subtitle` | 可选副标题 |
| `JmcReportPopupOptions.Status` | 可选底部状态文本 |
| `JmcReportPopupOptions.BodyFormat` | 正文解析格式：`PlainText`、`RichText` 或 `Markdown` |
| `JmcReportPopupOptions.BodyUsesRichText` | 旧兼容写法；新代码建议使用 `BodyFormat` |
| `JmcReportPopupOptions.Buttons` | 底部按钮列表；为空时自动添加关闭按钮 |
| `JmcReportPopupOptions.ShowBackstop` | 是否显示原生深色模态遮罩 |
| `JmcReportPopupOptions.CloseOnEscape` | 是否允许 Escape 关闭 |
| `JmcReportPopupOptions.MinimumSize` | 弹窗最小尺寸 |
| `JmcReportPopupBodyFormat` | 正文格式枚举 |
| `JmcReportPopupButton` | 定义按钮 key、文本、回调、点击后是否关闭、初始是否启用 |
| `JmcReportPopupHandle` | 可更新标题、副标题、状态、正文与正文格式，启停按钮或关闭弹窗 |

示例：

```csharp
JmcReportPopupHandle? popup = JmcReportPopup.Open(new JmcReportPopupOptions
{
    Title = "SpireDoctor 诊断报告",
    Subtitle = "最近 3 份日志",
    Body = reportText,
    BodyFormat = JmcReportPopupBodyFormat.Markdown,
    Status = "分析完成",
    Buttons =
    [
        new JmcReportPopupButton("copy", "复制", _ => DisplayServer.ClipboardSet(reportText)),
        new JmcReportPopupButton("close", "关闭", closeOnClick: true)
    ]
});

popup?.SetStatus("已复制到剪贴板");
```

---

## 13. UI：暂停菜单按钮扩展

命名空间：`JmcModLib.UI.PauseMenu`

暂停菜单按钮扩展用于向运行内右上角暂停菜单添加普通按钮。它不属于配置项，不做持久化；如果同一个动作还需要热键，应额外注册 `JmcHotkey` 或 `UIHotkey`，让二者调用同一业务方法。

### 13.1 生命周期流程图

```mermaid
flowchart TD
    A[子 MOD Register deferred=true] --> B[RegistryBuilder.RegisterPauseMenuButton]
    A --> C[静态方法标记 PauseMenuButtonAttribute]
    B --> D[Builder.Done]
    C --> D
    D --> E[ModRegistry.OnRegistered]
    E --> F[AttributeRouter 扫描]
    F --> G[PauseMenuRegistry 保存条目]
    G --> H[NPauseMenu Ready / Initialize / OnSubmenuOpened]
    H --> I[克隆原生按钮模板]
    I --> J[解析 settings_ui 本地化文本]
    J --> K[计算 VisibleWhen / EnabledWhen]
    K --> L[按 Anchor + Order + ModId + Assembly + Key 排序]
    L --> M[刷新可见性、启用状态与焦点链]
```

### 13.2 Attribute 用法

```csharp
using JmcModLib.UI.PauseMenu;
using JmcModLib.Utils;

[PauseMenuButton(
    "调试面板",
    Key = "open.debug.panel",
    LocTable = "settings_ui",
    TextKey = "EXTENSION.JMCMODLIB.PAUSE_MENU.MyMod.open_debug_panel.TEXT",
    Anchor = PauseMenuButtonAnchor.BeforeExitActions,
    Order = 10)]
internal static void OpenDebugPanel(PauseMenuButtonContext context)
{
    ModLogger.Info($"从暂停菜单打开调试面板，运行中={context.IsRunInProgress}");
}
```

Attribute 入口用于静态方法，支持以下签名：

| 签名 | 说明 |
|---|---|
| `static void Method()` | 不需要上下文的同步动作 |
| `static void Method(PauseMenuButtonContext context)` | 需要读取当前暂停菜单状态的同步动作 |
| `static Task Method()` | 不需要上下文的异步动作 |
| `static Task Method(PauseMenuButtonContext context)` | 需要上下文的异步动作 |

### 13.3 手动注册用法

```csharp
ModRegistry.Register<MainFile>(true)?
    .RegisterPauseMenuButton(
        key: "open.debug.panel",
        text: "调试面板",
        action: OpenDebugPanel,
        anchor: PauseMenuButtonAnchor.BeforeExitActions,
        order: 10,
        locTable: "settings_ui",
        textKey: "EXTENSION.JMCMODLIB.PAUSE_MENU.MyMod.open_debug_panel.TEXT",
        enabledWhen: static context => context.IsRunInProgress && !context.IsGameOver)
    .Done();

static void OpenDebugPanel(PauseMenuButtonContext context)
{
    ModLogger.Info($"手动注册暂停菜单按钮被点击，运行状态={context.RunState?.GetType().Name}");
}
```

手动注册适合需要动态条件、跨文件组合或显式注册链的场景。正式 MOD 建议始终显式传 `key` 与 `textKey`，避免发布后方法名或 fallback 文本变化导致本地化与反注册语义漂移。

### 13.4 `PauseMenuButtonAttribute`

| 属性 | 默认 | 说明 |
|---|---:|---|
| `Text` | 构造参数 | fallback 按钮文本 |
| `Key` | `null` | 稳定键；为空时按声明方法推导 |
| `Order` | `0` | 同锚点内排序，越小越靠前 |
| `Anchor` | `BeforeExitActions` | 插入锚点 |
| `LocTable` | `"settings_ui"` | 文本本地化表 |
| `TextKey` | `null` | 显式按钮文本 key |
| `CloseMenuOnClick` | `false` | 点击后是否关闭暂停菜单 |
| `Color` | `UIButtonColor.Default` | 复用设置按钮颜色语义或等价抽象 |

### 13.5 `PauseMenuButtonOptions`

手动注册和底层 registry 共享的元数据对象。

| 属性 | 默认 | 说明 |
|---|---:|---|
| `Key` | 必填 | Assembly 内唯一稳定键，用于节点名、本地化约定 key 与反注册 |
| `Text` | 必填 | fallback 显示文本 |
| `Order` | `0` | 同锚点内排序 |
| `Anchor` | `BeforeExitActions` | 插入锚点 |
| `LocTable` | `"settings_ui"` | 本地化表 |
| `TextKey` | `null` | 显式按钮文本 key |
| `VisibleWhen` | `null` | 运行时可见性判断；异常时隐藏 |
| `EnabledWhen` | `null` | 运行时启用判断；异常时禁用 |
| `CloseMenuOnClick` | `false` | 回调成功触发后是否关闭暂停菜单 |
| `Color` | `UIButtonColor.Default` | 按钮颜色风格 |

### 13.6 `PauseMenuButtonAnchor`

| 值 | 说明 |
|---|---|
| `AfterResume` | 插在继续按钮之后 |
| `AfterSettings` | 插在设置按钮之后 |
| `AfterCompendium` | 插在百科大全按钮之后 |
| `BeforeExitActions` | 插在放弃、断开连接、保存并退出这组离开/危险操作之前；默认推荐 |
| `End` | 插在暂停菜单末尾 |

排序规则固定为 `Anchor`、`Order`、`ModId`、`AssemblyName`、`Key`。不同 MOD 的相同 `Key` 允许共存，同一 Assembly 内重复 `Key` 后注册者覆盖先注册者并记录 warning。

### 13.7 `PauseMenuButtonContext`

| 属性 | 类型 | 说明 |
|---|---|---|
| `Mod` | `ModContext` | 所属 MOD 上下文 |
| `Assembly` | `Assembly` | 所属 Assembly |
| `RunState` | `IRunState?` | 当前运行状态，可能为空 |
| `Menu` | `NPauseMenu` | 原生暂停菜单节点；普通 MOD 不建议随意改动 |
| `Button` | `NButton` | 当前 JML 按钮节点；普通 MOD 不建议随意改动 |
| `IsMultiplayerClient` | `bool` | 当前是否多人客户端 |
| `IsRunInProgress` | `bool` | 是否处于运行中 |
| `IsGameOver` | `bool` | 是否已游戏结束 |

### 13.8 `PauseMenuRegistry`

| 成员 | 说明 |
|---|---|
| `RegisterButton(...)` | 手动注册暂停菜单按钮，按 `(Assembly, Key)` 建立唯一身份 |
| `UnregisterButton(string key, Assembly? assembly = null)` | 注销指定 Assembly 下的按钮 |
| `GetEntries(Assembly? assembly = null)` | 获取指定 Assembly 的已注册条目快照 |

`PauseMenuRegistry` 应随 `ModRegistry.OnUnregistered` 清理对应 Assembly 条目。刷新暂停菜单时，JML 只移动或更新自己创建的节点，保留未知节点以兼容其他 MOD。

### 13.9 本地化约定

按钮文本默认使用 `settings_ui` 表。推荐显式写 `TextKey`，例如：

```json
{
  "EXTENSION.JMCMODLIB.PAUSE_MENU.MyMod.open_debug_panel.TEXT": "调试面板"
}
```

如果未指定 `TextKey`，实现可按 `ModId + Key` 推导约定 key；文档和 Demo 建议显式 key，方便多语言文件和后续重命名。

---

## 14. Build / Deploy 与子 MOD props

### 14.1 生命周期流程图

```mermaid
flowchart TD
    A[dotnet build JmcModLib] --> B[EnsureGodotProjectFiles]
    B --> C[生成/检查 project.godot / export_presets / manifest]
    C --> D[BuildAndDeploy]
    D --> E[构建 Bootstrap]
    E --> F[复制 Runtime 为 JmcModLib.Runtime.dll]
    F --> G[复制 Bootstrap 为 JmcModLib.dll]
    G --> H[复制 XML / Newtonsoft / references]
    H --> I[Godot export-pack 生成 pck]
    I --> J[robocopy 到 STS2 mods/JmcModLib]
    J --> K{LaunchGamePrompt?}
    K -- true --> L[PowerShell 弹窗启动游戏]
    K -- false --> M[结束]
```

### 14.2 当前 MSBuild 关键点

JML 主项目：

- `TargetFramework=net9.0`
- `GenerateDocumentationFile=true`
- 默认本地路径是 Windows 个人路径，建议本地化到 user props。
- `BuildAndDeploy` 会同时构建 Bootstrap 与 Runtime。
- Runtime 被复制为 `JmcModLib.Runtime.dll`，Bootstrap 被复制为 `JmcModLib.dll`。

子 MOD props：

```xml
<Project>
  <PropertyGroup>
    <JmcModLibPublishDir Condition="'$(JmcModLibPublishDir)' == ''">$(MSBuildThisFileDirectory)</JmcModLibPublishDir>
    <JmcModLibRoot Condition="'$(JmcModLibRoot)' == ''">$(JmcModLibPublishDir)</JmcModLibRoot>
    <JmcModLibRuntimePath Condition="'$(JmcModLibRuntimePath)' == ''">$(JmcModLibPublishDir)JmcModLib.Runtime.dll</JmcModLibRuntimePath>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="JmcModLib">
      <HintPath>$(JmcModLibRuntimePath)</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

---

## 15. 默认参数语义索引

| 默认参数 | 所在 API | 语义 | 文档建议 |
|---|---|---|---|
| `assembly = null` | 多数 API | 通过调用栈解析调用方 Assembly | 入口可省略；helper 中显式传 |
| `displayName/version = null` | `ModRegistry.Register` | 从 manifest / Assembly 回退 | 普通 MOD 省略 |
| `deferredCompletion = bool` | `Register<T>(bool)` | true 返回 builder，false 立即 Done | 建议新增语义化 overload |
| `group = DefaultGroup` | Config/UI/Button | 默认分组 | UI 层最好本地化为常规 |
| `storageKey = null` | 手动 config/button | 用显示文本派生 | 正式 MOD 不建议省略 |
| `Key = null` | Attribute config/button/hotkey | 从类型/成员/方法推导 | 发布后建议显式稳定 key |
| `buttonText = "按钮"` | Button | 按钮回退文本 | 建议本地化或中性英文默认 |
| `FlushOnSet = true` | ConfigManager | 每次 SetValue 落盘 | 滑条高频可考虑 debounce |
| `allowKeyboard = true` | UIKeybind/UIHotkey | 默认键盘绑定 | 合理 |
| `allowController = false` | UIKeybind/UIHotkey | 默认不开手柄 | 合理 |
| `ConsumeInput = true` | Hotkey | 触发后吃输入 | 动作热键合理；调试热键设 false |
| `ExactModifiers = true` | Hotkey | 禁止额外修饰键 | 避免 `Ctrl + F8` 误触发 `F8` |
| `AllowEcho = false` | Hotkey | 不响应长按重复输入 | 普通动作热键合理；连发类操作可设 true |
| `DebounceMs = 150` | Hotkey | 防抖 150ms | 合理 |
| `Anchor = BeforeExitActions` | PauseMenuButton | 插在离开/危险操作之前 | 普通工具按钮的推荐默认位置 |
| `CloseMenuOnClick = false` | PauseMenuButton | 点击后默认不关闭暂停菜单 | 避免工具按钮、弹窗按钮或状态切换动作产生意外导航 |
| `FallbackLanguage = eng` | L10n | 英文回退 | 合理 |
| `DefaultTable = settings_ui` | L10n | 默认设置表 | 合理 |

---

## 16. 模块间依赖概览

```mermaid
flowchart LR
    Core[Core: ModRegistry/Runtime] --> Config[ConfigManager]
    Core --> Logger[ModLogger]
    Config --> Router[AttributeRouter]
    Router --> Reflection[Reflection Accessors]
    Config --> Storage[Config Storage]
    Config --> UI[Settings UI]
    Config --> Input[Input/Hotkeys]
    Input --> Steam[Steam Input]
    UI --> L10n[L10n]
    UI --> Prefabs[Prefabs]
    UI --> PauseMenu[Pause Menu Buttons]
    Config --> Multiplayer[Optional Network Features]
    Multiplayer --> Logger
    Logger --> Core
    Reflection --> Logger
```

架构上最重要的方向是保持 `Core` 轻、`Config` 稳、`Input/UI` 可替换。游戏 UI 和 Steam Input 都属于外部易变区域，应把硬编码选择器和 manifest 合并逻辑隔离在内部层。

---

## 17. Multiplayer：可选网络功能

命名空间：`JmcModLib.Multiplayer`

该模块把一个静态布尔配置、它独占的 `INetMessage` 标记接口和运行时协议状态绑定在一起。初始 manifest 使用 `affects_gameplay=false`；功能真正启用时，JML 才把所属 MOD 提升为影响玩法并加入兼容性身份。

### 17.1 `OptionalNetworkFeatureAttribute`

目标必须是同时带 `[Config]` 的静态 `bool` 字段或属性。

| 成员 | 说明 |
|---|---|
| `OptionalNetworkFeatureAttribute(string id, Type messageMarkerType)` | 声明稳定功能 ID 与独占消息标记接口；标记必须是继承 `INetMessage` 的接口 |
| `Id` | 功能在所属 MOD 内的稳定标识 |
| `MessageMarkerType` | 该功能独占的消息标记接口 |
| `CompatibilityVersion` | 协议兼容版本，默认 `"1"`；不兼容修改时递增 |

一个具体消息不能同时属于两项可选功能。所有属于该功能的消息都必须实现其标记接口。
声明必须在常规 MOD 初始化阶段完成扫描；游戏基础协议初始化后的延迟注册会被拒绝并安全回退。

### 17.2 `OptionalNetworkFeatureHandle`

| 成员 | 说明 |
|---|---|
| `Id` | 功能 ID |
| `ModId` | 所属 MOD ID |
| `CompatibilityVersion` | 当前兼容版本 |
| `RequestedEnabled` | 用户配置请求的状态，可能尚未应用 |
| `EffectiveEnabled` | 当前消息协议真正采用的状态；消息注册、发送和业务入口必须以此为准 |
| `ApplyState` | 当前应用状态 |
| `HasPendingApply` | 请求或应用状态是否仍未完成 |
| `StateChanged` | 任意公开状态变化时触发 |
| `EffectiveEnabledChanged` | 真正生效的启用状态变化时触发，适合注册/注销消息处理器并清理未完成流程 |

`OptionalNetworkFeatureApplyState`：

| 值 | 说明 |
|---|---|
| `Applied` | 请求状态已应用到当前运行时协议 |
| `PendingNetworkIdle` | 配置已保存，正在等待主机、加入流程或会话完整断开 |
| `RestartRequired` | 热重建失败，已保留旧的有效协议，需要重启完成应用 |

### 17.3 `OptionalNetworkFeatures`

| 成员 | 说明 |
|---|---|
| `Get(string id, Assembly? assembly = null)` | 获取指定程序集的句柄；未注册、声明无效或 ID 不存在时抛出 `KeyNotFoundException` |
| `Get<TOwner>(string id)` | 使用 `TOwner` 所在程序集获取句柄 |
| `TryGet(string id, out OptionalNetworkFeatureHandle? handle, Assembly? assembly = null)` | 尝试获取句柄 |

句柄应在 `ModRegistry.Register` 完成 Attribute 扫描后查询。业务代码不得直接用配置字段决定是否注册或发送消息：等待断开时，`RequestedEnabled` 与 `EffectiveEnabled` 会有意保持不同。

### 17.4 应用策略

- 无网络活动时，JML 在主线程安全点重建游戏消息表并热应用。
- 主机创建、加入流程、大厅或局内仍活跃时，保持旧协议并进入 `PendingNetworkIdle`；完整断开后自动应用最后一次请求。
- 重建失败时回滚旧协议、进入 `RestartRequired`，并复用 `GameRestart.ShowRestartConfirmationAsync` 的确认与安全退出流程。
- 正常切换不需要为 `[Config]` 设置 `RestartRequired=true`。
- 协议启用时兼容性身份包含 `ModId`、功能 `Id` 与 `CompatibilityVersion`，两端必须一致。

完整示例、manifest 约束和接入检查表见[可选网络功能专题](JML_OptionalNetworkFeatures.md)。
