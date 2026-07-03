# JMC STS2 MOD BuildTools

这组 MSBuild 文件提供 STS2 MOD 的通用构建流水线，和 JmcModLib 运行时引用保持正交。

## 文件

- `Jmc.Sts2Mod.Build.props`：公共属性默认值、游戏 DLL 引用、作者元信息。
- `Jmc.Sts2Mod.Build.targets`：Godot 暂存文件生成、版本读取、清单同步、PCK 打包、发布目录同步。
- `scripts/Sync-ModManifestVersion.ps1`：保留 JSON 原格式，只同步 `version` 字段。

如果发布清单不存在，构建工具会生成一个默认清单：依赖 `JmcModLib >= 1.4.0`，且默认不标记为影响游戏玩法。已有清单不会被模板覆盖，构建时只会同步 `version` 字段。

## 用法

普通 MOD 可以从 JML 源码仓库显式导入：

```xml
<Import Project="..\JmcModLib_STS2\BuildTools\Jmc.Sts2Mod.Build.props" />

<!-- 如果需要 JmcModLib Runtime，单独导入 JML 的 props。 -->
<Import Project="$(ModDir)\JmcModLib\JmcModLib.Sts2.props" />

<Import Project="..\JmcModLib_STS2\BuildTools\Jmc.Sts2Mod.Build.targets" />
```

JML 构建发布后，也会把这组文件复制到 `JmcModLib\modPublish\BuildTools`。本地已安装 JML 时，普通 MOD 可以从游戏 MOD 目录导入：

```xml
<PropertyGroup>
  <Sts2Path Condition="'$(Sts2Path)' == ''">D:\SteamLibrary\steamapps\common\Slay the Spire 2</Sts2Path>
  <JmcModLibDir Condition="'$(JmcModLibDir)' == ''">$(Sts2Path)\mods\JmcModLib</JmcModLibDir>
</PropertyGroup>

<Import Project="$(JmcModLibDir)\BuildTools\Jmc.Sts2Mod.Build.props" />

<!-- 如果需要 JmcModLib Runtime，单独导入 JML 的 props。 -->
<Import Project="$(JmcModLibDir)\JmcModLib.Sts2.props" />

<Import Project="$(JmcModLibDir)\BuildTools\Jmc.Sts2Mod.Build.targets" />
```

`JmcModLib.Sts2.props` 只负责引用 JmcModLib Runtime；BuildTools 只负责构建流水线，二者互不隐式导入。
