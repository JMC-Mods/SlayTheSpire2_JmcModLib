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
| `MultiplayerCompat.TryGetConnectionExtraInfo()` | Read multiplayer error details | private `_connectionExtraInfo` in 0.99.1–0.107.1; public `ConnectionExtraInfo` in 0.108 |
| `MultiplayerCompat.TryGetJoinFlowNetService()` | Read the join-flow network service | internally created `NetClientGameService?` property in 0.99.1–0.107.1; constructor-injected `INetClientGameService` property in 0.108; `_netService` is a defensive fallback |

Early versions in this table explain the historical origin of compatibility candidates; they do not define the complete support range of the current JML package. The effective minimum version is the published manifest's `min_game_version`.

## Child-MOD usage

```csharp
using JmcModLib.Compat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;

int loadedModCount = ModCompat.GetLoadedMods().Count;

if (MultiplayerCompat.TryGetConnectionExtraInfo(errorInfo, out ConnectionFailureExtraInfo? extraInfo))
{
    IReadOnlyCollection<string> hostOnlyMods = extraInfo.missingModsOnLocal ?? [];
}
```

Every `Try...` method returns `false` when the current game version exposes no recognized member or access fails. Callers should preserve the native flow or disable the dependent enhancement instead of guessing a field layout.

## Maintenance rules

1. Confirm stable semantics before adding a newly discovered game-version difference to `Compat`.
2. Prefer JML `MemberAccessor` / `MethodAccessor`; do not scatter direct `FieldInfo.GetValue` or Harmony `AccessTools` calls.
3. Public methods do not expose version-specific member names; those names exist only in the compatibility implementation and this table.
4. Compile against both the current game DLL and every still-supported historical DLL.
