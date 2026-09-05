# AGENTS.md — Marooned Project Instructions

## Project Overview
Marooned is a single-player 2D sandbox card-survival game with social
deduction mechanics, playable by an AI VTuber through MCP.
- Genre: Card Survival × Social Deduction
- Engine: Unity (C#)
- DI Framework: VContainer
- Message Bus: MessagePipe + MessagePipe.Interprocess (TCP port 3216)
- Data Pipeline: Luban (CSV → typed C# classes)
- UI Pattern: MVP Lite (Views/ + Presenters/)
- AI Integration: MCP Bridge (.NET console app) ↔ Unity via TCP

## Workspace Layout
- `Marooned/` or `UnityProject/` → Open in Unity Hub
- `McpBridge/` → .NET console app, build with `dotnet`
- `Shared/` → Canonical shared types (edit ONLY here, run sync-shared.sh)
- `DataTables/` → Luban source CSV drafts
- `Tools/Luban/` → Luban binary (not included in repo)
- `marooned-wiki/` → Obsidian Vault with Karpathy LLM Wiki plugin

## Knowledge Base (Karpathy LLM Wiki)
The project uses a Karpathy LLM Wiki stored in `marooned-wiki/`.
This wiki contains ALL design decisions, architecture docs, game design
documents, and development progress.

### Wiki Structure:
- `marooned-wiki/wiki/sources/architecture/` → System architecture docs
- `marooned-wiki/wiki/sources/code-snippets/` → Key C# script documentation
- `marooned-wiki/wiki/sources/mechanics/` → Game mechanics design
- `marooned-wiki/wiki/sources/bug-log/` → Bug reports and solutions
- `marooned-wiki/wiki/sources/devlog-history/` → Development progress logs
- `marooned-wiki/wiki/sources/game-design-doc/` → GDD (game_design_doc.md)
- `marooned-wiki/wiki/concepts/` → Auto-generated concept pages
- `marooned-wiki/wiki/entities/` → Auto-generated entity pages

### CRITICAL RULES for AI Agent:
1. **BEFORE writing or modifying ANY code**, READ the relevant wiki files
   in `marooned-wiki/wiki/sources/` to understand existing architecture.
2. **ALWAYS read** `marooned-wiki/wiki/sources/game-design-doc/game_design_doc.md`
   before making design decisions — it contains all agreed-upon decisions.
3. **AFTER making significant changes**, suggest updates to relevant wiki
   files to keep documentation in sync.
4. When creating new systems, create corresponding docs in
   `marooned-wiki/wiki/sources/architecture/` or `mechanics/`.
5. Reference wiki files using relative paths from repo root:
   `marooned-wiki/wiki/sources/architecture/filename.md`
6. Never contradict decisions documented in the wiki without explicitly
   flagging the conflict first.

## Coding Conventions
- Language: C# (Unity-compatible subset)
- Edit Shared/ files ONLY at top-level, then run `./sync-shared.sh`
- DI registration goes in `GameLifetimeScope.cs`
- Cross-system communication via MessagePipe (in-process) or
  MessagePipe.Interprocess (Unity ↔ McpBridge TCP)
- UI follows MVP Lite: Views are passive, Presenters hold logic
- Data access through `LubanDataService` (typed table access)
- Async operations use UniTask, not coroutines
- Port 3216 for MCP Bridge TCP (NOT 3215 — that's Cultivation Together)

## Known Architecture (from reference project Cultivation-Together)
- Composition Root: `Core/GameLifetimeScope.cs`
- Systems live in `Systems/` folder
- UI in `UI/Views/` + `UI/Presenters/`
- Chibi sprite-swap renderer: `Data/ChibiAnimatedRenderer.cs`

## Validation
- Unity: Open in Unity Hub, check Console for errors
- McpBridge: `cd McpBridge && dotnet build`
- Shared sync: `./sync-shared.sh` after editing Shared/*.cs

## Current Status (Lab A)
- Code scaffolding only — no scenes, prefabs, or art yet
- LubanDataService.LoadAll() is a stub
- NpcDirectorSystem TickBehavior is placeholder
- McpBridge tool methods are NotImplementedException
- First milestone: "round trip" — bridge connects + GetGameState returns data