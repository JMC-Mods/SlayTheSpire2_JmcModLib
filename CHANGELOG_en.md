**🌐[ [中文](CHANGELOG.md) | English ]**

# 🧾 Changelog

All notable changes to this project will be recorded in this file.

Versioning rule: major.minor.patch. The major version is used for larger feature-complete milestones, the minor version is generally updated when a new Steam Workshop version is published, and the patch version is updated after each code-related commit, starting from 0.

## [1.5.4] - 2026-7-6
### Changed
- Reverted the previous Steam Input controller mapping fallback handling and restored the original mapping completion behavior.

## [1.5.3] - 2026-7-5
### Added
- Added `UIVisibleWhenAttribute` for dynamically showing or hiding config entries in the settings UI based on other config values.

## [1.5.2] - 2026-7-4
### Added
- Added `UIDropdownOptionsProviderAttribute` for refreshing dropdown choices at runtime based on other config values.

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
0.99.1 compact

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
