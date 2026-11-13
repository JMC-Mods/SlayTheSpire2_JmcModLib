**🌐[ [中文](JML_Compatibility.md) | English ]**

# JML Game-Version Compatibility Layer

JML centralizes confirmed STS2 member differences in the `Compat` directory and the `JmcModLib.Compat` namespace. JML internals and child MODs should call these typed methods instead of hard-coding private game fields or version-specific member names again.

This layer does not replace general reflection. Only cross-version access with stable semantics belongs here; configuration callbacks, business reflection, and single-version UI injection remain in their owning modules.

## Current capabilities

| Facade | Stable capability | Encapsulated version differences |
|---|---|---|
| `ModCompat.GetKnownMods()` | Read MODs known by the game | 0.99.1 `ModManager.AllMods`; 0.103–0.108 `ModManager.Mods`; `LoadedMods` / `GetLoadedMods()` are loaded-only last-resort fallbacks |
| `ModCompat.GetLoadedMods()` / `IsLoaded()` | Read or identify loaded MODs | 0.99.1 `LoadedMods` / `wasLoaded`; 0.103–0.108 `GetLoadedMods()` / `state` |
| `ModCompat.GetAssemblies()` | Read every managed assembly of a MOD | 0.99.1–0.107.1 `assembly`; 0.108 `assemblies` |
| `ModCompat.GetManifest()` | Read the manifest | `ModManifest` is a class in 0.99.1–0.107.1 and a record in 0.108; the `manifest` field name did not change |
| `ModCompat.GetPckName()` | Read the PCK name | no such member was found in archived 0.99.1–0.108 DLLs; `pckName` / `PckName` are defensive candidates for unarchived historical or future builds |
| `ModCompat.GetManifestId/Name/Version()` | Read manifest metadata | lowercase fields in archived 0.99.1–0.108 DLLs; PascalCase names are defensive fallbacks |
| `MultiplayerCompat.TryGetConnectionExtraInfo()` | Read the multiplayer error-detail object | private `_connectionExtraInfo` in 0.99.1–0.107.1; public `ConnectionExtraInfo` since 0.108; 0.110 replaces the object's old mismatch fields with `localInfo` / `remoteInfo` and `GetMissingModsOnLocal/Remote()`, so the raw object's field layout is not a stable API |
| `MultiplayerCompat.TryGetJoinFlowNetService()` | Read the join-flow network service | `NetClientGameService?` created inside `Begin()` in 0.99.1–0.107.1, where a pre-call null value does not mean the member is missing; constructor-injected `INetClientGameService` property in 0.108; `_netService` is a defensive fallback |
| `MultiplayerCompat.GetRunLobbyPlayerIds()` / `GetLoadRunLobbyPlayerIds()` | Read a snapshot of lobby player IDs | `ConnectedPlayerIds` in 0.99.1–0.109.1; 0.110 stores version-aware player records and exposes their plain IDs through `PlayerIds` |
| `MultiplayerCompat.GetConnectedHostPeerIds()` | Read a snapshot of fully connected remote host peers | `ConnectedPeers` belongs to `INetHostGameService` in 0.107.1–0.110.1; 0.111 removes it from the interface while retaining the property on the native host implementation |

Version ranges in this table describe the historical origin of compatibility candidates or the latest known member change; they do not mean that JML only supports versions through the range endpoint. The effective minimum version is the published manifest's `min_game_version`. Unless stated otherwise, later supported versions retain the most recently listed member shape.

## 0.113 adaptation

JML 1.10.0 keeps the existing public API unchanged and handles these differences internally:

- The join-flow patch resolves both the old `Begin(IClientConnectionInitializer, SceneTree)` and 0.113's `Begin(SceneTree)`, allowing initialization to reach the hotkey input patches.
- Settings hover tips keep their constructor-generated ID instead of assigning the now-read-only `HoverTip.Id`. Each owner displays one tip in its own set.
- Tip positioning prefers `NSettingsScreen.GetSettingTipPosition(Control)` in 0.113, including native RTL layout handling, and falls back to `settingTipsOffset` on older versions.
- The existing peer-ID snapshot API continues to enumerate `ConnectedPeers` after its change to a read-only list in 0.113; child MOD calls do not change.

## Child-MOD usage

```csharp
using JmcModLib.Compat;
using JmcModLib.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Multiplayer;

int loadedModCount = ModCompat.GetLoadedMods().Count;
IReadOnlyList<ulong> runPlayerIds = MultiplayerCompat.GetRunLobbyPlayerIds(runManager.RunLobby);
IReadOnlyList<ulong> connectedPeerIds = MultiplayerCompat.GetConnectedHostPeerIds(hostService);
bool keepJmlPrompt = OptionalNetworkMismatch.ShouldHandle(errorInfo);
```

Every `Try...` method returns `false` when the current game version exposes no recognized member or access fails; snapshot methods return an empty collection under the same conditions. Callers should preserve the native flow or disable the dependent enhancement instead of guessing a field layout. `TryGetConnectionExtraInfo` only stabilizes whether the error-detail object is stored in a private field or public property. Child MODs that need to classify JML optional-network mismatches should call `OptionalNetworkMismatch.ShouldHandle` instead of directly reading the raw `missingModsOnHost` / `missingModsOnLocal` fields removed in 0.110 or relying on `NetError` integer values renumbered in 0.111.

## Maintenance rules

1. Confirm stable semantics before adding a newly discovered game-version difference to `Compat`.
2. Prefer JML `MemberAccessor` / `MethodAccessor`; do not scatter direct `FieldInfo.GetValue` or Harmony `AccessTools` calls.
3. Public methods do not expose version-specific member names; those names exist only in the compatibility implementation and this table.
4. Compile against both the current game DLL and every still-supported historical DLL.
