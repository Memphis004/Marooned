# Marooned (working title) — Card Survival x Social Deduction

Single-player 2D sandbox card-survival game, playable by an AI VTuber through
MCP. Architecture reused from the `Cultivation-Together` reference project
(MCP Bridge, Luban DataTable pipeline, `UI.MVP Lite`, dictionary-based avatar
appearance) — see `LLMWiki/game_design_doc.md` for the full design rationale
and the decisions made so far (Killer:Innocent ratio, Chibi sprite-swap
avatar, etc).

This is the **Lab A** scaffold: data models + system skeletons that compile
conceptually and establish the architecture, not a finished playable build.
Nothing here has been opened in Unity or `dotnet build`'d yet — treat method
bodies marked `TODO` / `NotImplementedException` as the next work items.

## Workspace layout

```
Shared/          canonical source for shared types — not a buildable project, just files
UnityProject/    open this folder directly in Unity Hub
McpBridge/       a .NET console app — build/run with `dotnet`, not Unity
DataTables/      Luban source tables (currently plain .csv drafts — see note below)
Tools/Luban/     drop the real Luban binary here (not included)
LLMWiki/         design doc + any future architecture notes
sync-shared.sh   copies Shared/*.cs into both UnityProject/ and McpBridge/
```

## Why Shared/ isn't a real package (yet)

Same reasoning as the reference project: schema is still moving every lab
round, and Unity doesn't consume NuGet packages anyway. `sync-shared.sh` just
copies the `.cs` files into both projects' `Shared/` folders.

**Edit files ONLY in the top-level `Shared/` folder** — the copies inside
`UnityProject/Assets/Scripts/Shared/` and `McpBridge/Shared/` get overwritten
every time you run:

```
./sync-shared.sh
```

## DataTables/ — CSV drafts, not real Luban tables yet

The `.csv` files here are schema drafts (`CardDef`, `LocationDef`,
`RecipeDef`, `ClueDef`, `IllnessDef`, `WorldEventDef`, `ChibiPartDef`) meant
to be opened in Excel, filled out properly, saved as `.xlsx`, and then run
through the real Luban toolchain (drop the binary in `Tools/Luban/`, add a
`gen.bat`/`gen.sh`, same pipeline as the reference project's Lab 11-12).

One schema note: `ChibiPartDef.FramesByAnimKey` (direction+animState →
ordered sprite frame list) is too nested for a flat spreadsheet row. Two
options once you get to real data: (a) a small per-part JSON sidecar file
referenced by path from the CSV, or (b) a second flat table
`ChibiPartFrame.csv` with columns `PartId, AnimKey, FrameIndex, SpritePath`
that Luban aggregates into the nested dictionary at codegen time. Pick
whichever the actual Luban config handles more cleanly — not decided yet.

## UnityProject/

Open in Unity Hub. Package manifest has the OpenUPM scoped registry wired up
but VContainer/MessagePipe/MessagePipe.Interprocess/UniTask entries are left
out on purpose — add them for real versions:

```
openupm add jp.hadashikick.vcontainer
openupm add com.cysharp.messagepipe
openupm add com.cysharp.messagepipe.vcontainer
openupm add com.cysharp.messagepipe.interprocess
openupm add com.cysharp.unitask
```

**What's actually here:**
- `Core/GameLifetimeScope.cs` — DI wiring (VContainer + MessagePipe + TCP interprocess on port `3216`). Interprocess API call shapes are illustrative — confirm against whatever `MessagePipe.Interprocess` actually exposes once installed.
- `Systems/` — `SurvivalStatSystem`, `CardInventorySystem`, `CraftingSystem`, `ExplorationSystem`, `NpcDirectorSystem`, `DeductionSystem`, `WorldEventSystem`, `GameStateProvider`, `LubanDataService` (stub loader — wire up real Luban-generated table access here), `McpRequestHandlers.cs`.
- `Data/ChibiAnimatedRenderer.cs` — the new sprite-swap paperdoll renderer (see design doc §4.1). Builds one `SpriteRenderer` per limb slot and flips frames based on `ChibiAppearance.Facing` / `AnimState`.
- `UI/` — `Views/` + `Presenters/` stubs for `CardHand`, `MapExplore`, `ClueBoard`, `MeetingVote`, `ConditionOverlay`, plus `UI/Core/UIRoot.cs`.

**Known gaps (expected for Lab A):**
- No Unity scenes, prefabs, or sprite art exist yet — this is code only.
- `LubanDataService.LoadAll()` is a stub; nothing is actually loaded from `DataTables/` yet.
- `NpcDirectorSystem` has a placeholder daily-schedule (`TickBehavior` does nothing) — real NPC movement/schedule is a later lab.
- No movement/input controller wired to `ChibiAnimatedRenderer.SetMotion(...)` yet.

## McpBridge/

Plain .NET console app, separate from Unity:

```
cd McpBridge
dotnet restore
dotnet run
```

Package versions in `McpBridge.csproj` are copied from the reference
project's *last known-good* pins — re-verify with `dotnet add package <name>`
before relying on them, versions may have moved on.

`Program.cs` lists the intended MCP tool surface
(`SurvivalQueryTools`, `SurvivalActionTools`, `DeductionTools`) with method
signatures but `NotImplementedException` bodies — wiring these to the actual
`ModelContextProtocol` SDK attributes/hosting calls is the first real
McpBridge task in Lab A.

**Both sides assume** Unity hosts the TCP endpoint at `127.0.0.1:3216`
(note: different port than the reference project's `3215`, to avoid
collisions if both projects are ever run side by side) and the bridge
connects as a client. Start Unity first, then run the bridge.

## Order of operations to get something running end to end (Lab A target)

1. `./sync-shared.sh`
2. Open `UnityProject/` in Unity Hub, resolve packages via `openupm-cli`
3. Fill in `LubanDataService.LoadAll()` — even a hardcoded dictionary is fine to unblock testing before the real Luban pipeline is wired up
4. Wire the real `MessagePipe.Interprocess` TCP host call in `GameLifetimeScope`
5. `cd McpBridge && dotnet add package MessagePipe && dotnet add package MessagePipe.Interprocess && dotnet add package ModelContextProtocol && dotnet add package Microsoft.Extensions.Hosting && dotnet add package MessagePack && dotnet run`
6. Confirm the bridge connects, and `GetGameState` returns something (even mock data) — this is the "round trip" milestone, same as Lab 1-3 in the reference project

## Design doc

Full concept, all systems, MCP tool table, roadmap: [`LLMWiki/game_design_doc.md`](LLMWiki/game_design_doc.md)
