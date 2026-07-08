**🌐[ [中文](CHANGELOG.md) | English ]**

# 🧾 Changelog

All notable changes to this project will be recorded in this file.

Versioning rule: major.minor.patch. The major version is used for larger feature-complete milestones, the minor version is generally updated when a new Steam Workshop version is published, and the patch version is updated after each code-related commit, starting from 0.

## [1.6.0] - 2026-7-9
### Added
- Added a public cross-platform restart API. After the user confirms in a popup, it restarts the game; on Android and iOS it fails gracefully and falls back.
- Completed restart support. Config entries marked as requiring a restart now provide a quick restart button.
- Added default support for showing a restart button in the game's native mod manager when changes require a restart. This feature can be disabled in settings.
- Added semantics for dynamic config UI visibility, dropdown options, and other dynamic display content.
- Added a cross-platform secret storage service.
- Added a Markdown-rendering information feed prefab.
- Added a series of persistence APIs.
### Fixed
- Fixed abnormal settings-screen aspect ratio when the aspect ratio is set to "auto" on non-16:9 displays.
- Fixed incomplete AttributeRouter scanning when a mod uses split DLLs. It now automatically falls back to scanning DLLs with the same ID.

## [1.5.0] - 2026-7-3
### Added
- Added a prefab for displaying Markdown information feeds.

### Fixed
- Fixed compatibility issues introduced by the STS2 0.108 mod assembly API changes.

## [1.4.4] - 2026-6-26
### Fixed
- Worked around Harmony's native helper library failing to load on some Linux distributions, which could prevent patches from being applied.
- Fixed incorrect highlight state handling for pause menu buttons.

## [1.4.1] - 2026-6-22
### Added
- Added the JML Dispatch multi-version DLL dispatch toolchain. Child mods can generate a zero-JML-runtime bootstrap and load the matching Runtime DLL for the current STS2 version.
- Added `docs/JML_Dispatch.md`.

## [1.4.0] - 2026-6-19
### Fixed
Fixed compatibility with version 0.99.1.

### Changed
- Adapted the manifest format migration for official MOD publishing.

## [1.3.3] - 2026-6-6
### Fixed
- Minor fixes for the previous update.

## [1.3.2] - 2026-6-6
### Fixed
- Fixed a catastrophic issue where registering controller events after a game version update could invalidate controller layouts. Updating is recommended.

## [1.3.0] - 2026-6-5
### Added
- Added pause menu entry extension APIs, allowing child mods to add button entries to the in-run pause menu through `[PauseMenuButton]` or manual registration.
- Pause menu entries support stable ordering, localization fallback, click context, exception isolation, and keyboard/controller focus-chain integration.

## [1.2.0] - 2026-5-26
### Fixed
- Fixed Attribute scanning being interrupted on Android and other restricted runtimes when dynamic reflection accessors fail to initialize, which could prevent `[Config]`, `[UIButton]`, `[JmcHotkey]`, and `[UIHotkey]` from registering.
- Reflection accessors now fall back to standard reflection calls when dynamic IL or expression delegates are unavailable, keeping config and hotkey systems usable across platforms.

## [1.1.0] - 2026-5-8
### Added
- Added the JmcModLib black-and-gold badge avatar.
- Official release.

## [1.0.105] - 2026-5-7
### Added
Initial version release.
Added English versions of the README, CHANGELOG, API Reference, and Quick Start docs, with language switch links for each document pair.
