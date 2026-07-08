**🌐[ 中文 | [English](CHANGELOG_en.md) ]**

# 🧾 Changelog

所有对本项目的重要更改都将记录在此文件中。

版本号规则： 主版本号.次版本号.修订号，其中主版本号涉及到大功能完善，次版本号原则上Steam创意工坊发布新版本后进行更新，修订号每次涉及代码的commit后更新（从0开始）。

## [1.5.11] - 2026-7-7
### Added
- Persistence 新增 `JmcRunContext.TryGetCurrentRunIdentity()` 和 `JmcRunIdentity`，供本地偏好保存当前 run 身份并自行判断是否仍属于当前这一局。

## [1.5.10] - 2026-7-7
### Added
- Persistence 新增 `[JmcLocalPreference]`，用于保存当前机器本地 UI 偏好等不影响玩法的数据，并提供 `JmcPersistenceManager.FlushLocalPreferences()`。

### Changed
- Run save `_jml` 扩展数据会在原版 `RunManager.CanonicalizeSave` 后继续保留，降低多人存档整理流程丢失 JML 数据的风险。

## [1.5.9] - 2026-7-7
### Changed
- 调整 Persistence run save 写入策略：不再用 Harmony Prefix 跳过原版 `RunSaveManager.SaveRun`，改为原版保存成功后附加写入 `_jml` 扩展数据，以降低兼容性风险。

## [1.5.8] - 2026-7-7
### Added
- 新增 JML Persistence 第一阶段：支持 global/profile/run 非同步持久化、Slot 写入 API、Attribute 扫描接入和 run save `_jml` 扩展文档。

## [1.5.7] - 2026-7-6
### Changed
- Secret 的 `ScopeProvider` 解析改为使用 JML 自带反射访问器，避免直接使用原生反射调用。

## [1.5.6] - 2026-7-6
### Fixed
- 修复 Secret 输入弹窗只显示模态遮罩、不显示输入面板的问题。

## [1.5.5] - 2026-7-6
### Added
- 新增 JML SecretStore：支持 `[Secret]`、`RegistryBuilder.RegisterSecret`、设置页自动设置/清空 UI、Windows current-user DPAPI 后端和显式允许的弱保护文件回退。

## [1.5.4] - 2026-7-6
### Changed
- 撤回此前的 Steam Input 手柄映射回退处理，恢复原有手柄映射补全逻辑。

## [1.5.3] - 2026-7-5
### Added
- 新增 `UIVisibleWhenAttribute`，支持配置项根据其他配置项的当前值在设置 UI 中动态显示或隐藏。

## [1.5.2] - 2026-7-4
### Added
- 新增 `UIDropdownOptionsProviderAttribute`，支持下拉候选项根据其他配置项在运行时动态刷新。

## [1.5.0] - 2026-7-3
### Added
- 新增一个用于显示 Markdown 信息流的预制件。

### Fixed
- 修复 STS2 0.108 版本带来的 MOD 程序集接口兼容性问题。

## [1.4.4] - 2026-6-26
### Fixed
- 规避某些 Linux 发行版下 Harmony 底层原生辅助库无法加载，导致补丁无法应用的问题。
- 修复暂停菜单按钮高亮状态错误的问题。

## [1.4.1] - 2026-6-22
### Added
- 新增 JML Dispatch 多版本 DLL 分派工具链，可为子 MOD 生成零 JML 运行时依赖的 Bootstrap，并按 STS2 版本加载对应 Runtime DLL。
- 新增 `docs/JML_Dispatch.md` 使用文档。

## [1.4.0] - 2026-6-19
### Fixed
修复0.99.1版本不兼容的问题

### Changed
- 适配正式MOD发布的格式迁移

## [1.3.3] - 2026-6-6
### Fixed
- 对上一条更新的一些小修复。

## [1.3.2] - 2026-6-6
### Fixed
- 修复版本更新后注册手柄事件会导致手柄布局失效的灾难性问题，建议更新。

## [1.3.0] - 2026-6-5
### Added
- 新增暂停菜单条目扩展 API，子 MOD 可通过 `[PauseMenuButton]` 或手动注册在运行中暂停菜单增加按钮条目。
- 暂停菜单条目支持稳定排序、本地化回退、点击上下文、异常隔离和键盘/手柄 focus 链。

## [1.2.0] - 2026-5-26
### Fixed
- 修复 Android 等受限运行时下，动态反射访问器创建失败会导致 Attribute 扫描中断，进而使 `[Config]`、`[UIButton]`、`[JmcHotkey]`、`[UIHotkey]` 无法注册的问题。
- 当动态 IL 或表达式委托不可用时，反射访问器现在会自动回退到普通反射调用，保证配置与热键系统优先保持可用。

## [1.1.0] - 2026-5-8
### Added
- 新增 JmcModLib 黑金徽章头像。
- 正式发布版本。

## [1.0.105] - 2026-5-7
### Added
初始版本发布
新增 README、CHANGELOG、API 文档、快速指南的英文版本，并添加中英文切换链接。
