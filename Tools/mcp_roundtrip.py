#!/usr/bin/env python3
"""
Lab C Phase 1 - Test B: MCP round-trip driver.

Spawns the McpBridge as an MCP stdio client, drives the tools over the running
Unity Play session, and writes JSON-lines evidence to
TestEvidence/lab-c-phase1/mcp-roundtrip.jsonl

Prereqs:
  - Unity in Play mode with BiomeScatterView set up (run Test A first)
  - bridge built: McpBridge/bin/Debug/net8.0/McpBridge.dll (dotnet build)

Usage (repo root):
  python Tools/mcp_roundtrip.py            # full Test B sequence
  python Tools/mcp_roundtrip.py --smoke    # connect + GetGameState only
"""

import json
import os
import subprocess
import sys
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
BRIDGE_DLL = REPO_ROOT / "McpBridge" / "bin" / "Debug" / "net8.0" / "McpBridge.dll"
EVIDENCE = REPO_ROOT / "TestEvidence" / "lab-c-phase1"
EVIDENCE.mkdir(parents=True, exist_ok=True)


class McpClient:
    """Minimal MCP stdio client (initialize -> tools/call, newline-delimited JSON)."""

    def __init__(self):
        if not BRIDGE_DLL.exists():
            sys.exit(f"bridge not built: {BRIDGE_DLL} (run: cd McpBridge && dotnet build)")
        self.proc = subprocess.Popen(
            ["dotnet", str(BRIDGE_DLL)],
            stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
            cwd=REPO_ROOT / "McpBridge", text=True, encoding="utf-8",
        )
        self._next_id = 0

    def _send(self, method, params=None, is_notification=False):
        msg = {"jsonrpc": "2.0", "method": method}
        if params is not None:
            msg["params"] = params
        if not is_notification:
            self._next_id += 1
            msg["id"] = self._next_id
        self.proc.stdin.write(json.dumps(msg) + "\n")
        self.proc.stdin.flush()
        return None if is_notification else self._next_id

    def _recv(self, want_id, timeout=60):
        deadline = time.time() + timeout
        while time.time() < deadline:
            line = self.proc.stdout.readline()
            if not line:
                time.sleep(0.05)
                continue
            try:
                msg = json.loads(line)
            except json.JSONDecodeError:
                continue
            if msg.get("id") == want_id:
                return msg
        raise TimeoutError(f"no response for id={want_id} within {timeout}s")

    def request(self, method, params=None, timeout=60):
        rid = self._send(method, params)
        return self._recv(rid, timeout)

    def initialize(self):
        self.request("initialize", {
            "protocolVersion": "2024-11-05",
            "capabilities": {},
            "clientInfo": {"name": "marooned-test-driver", "version": "0.1"},
        })
        self._send("notifications/initialized", is_notification=True)

    def call_tool(self, name, arguments=None, timeout=60):
        res = self.request("tools/call",
                           {"name": name, "arguments": arguments or {}}, timeout)
        if "error" in res:
            return {"ok": False, "error": res["error"]}
        content = res["result"].get("content", [])
        text = "\n".join(c.get("text", "") for c in content)
        return {"ok": not res["result"].get("isError", False), "text": text}

    def close(self):
        try:
            self.proc.stdin.close()
            self.proc.wait(timeout=10)
        except Exception:
            self.proc.kill()


def record(results, step, payload):
    entry = {"ts": time.strftime("%Y-%m-%dT%H:%M:%S"), "step": step, **payload}
    with open(EVIDENCE / "mcp-roundtrip.jsonl", "a", encoding="utf-8") as f:
        f.write(json.dumps(entry, ensure_ascii=False) + "\n")
    print(f"[{step}] {json.dumps(payload, ensure_ascii=False)[:220]}")


def main():
    smoke = "--smoke" in sys.argv
    evidence_file = EVIDENCE / "mcp-roundtrip.jsonl"
    if evidence_file.exists() and not smoke:
        evidence_file.unlink()  # fresh run

    client = McpClient()
    results = []
    try:
        client.initialize()
        record(results, "initialize", {"ok": True})

        tools = client.request("tools/list")["result"]["tools"]
        tool_names = [t["name"] for t in tools]
        record(results, "tools_list", {"count": len(tool_names), "names": sorted(tool_names)})

        # ---- smoke: does Unity answer at all? ----
        r = client.call_tool("GetGameState", timeout=90)
        record(results, "GetGameState", r)
        if not r["ok"]:
            sys.exit("Unity not responding — is it in Play mode? (see mcp-bridge wiki)")

        if smoke:
            print("SMOKE OK")
            return

        # ---- Test B step 1: harvest in current biome (proves harvest_node tool) ----
        r = client.call_tool("HarvestNode", timeout=90)
        record(results, "HarvestNode@start", r)

        # ---- Test B step 2: move to cave_entrance (single hop from beach) ----
        r = client.call_tool("MoveToLocation", {"locationId": "cave_entrance"}, timeout=90)
        record(results, "MoveToLocation_cave_entrance", r)

        # ---- Test B step 3: harvest again — cave biome (rock_stone/bush_berry) ----
        r = client.call_tool("HarvestNode", timeout=90)
        record(results, "HarvestNode@cave", r)

        # ---- Test B step 4: deep_jungle is 2 hops via jungle_edge ----
        r = client.call_tool("MoveToLocation", {"locationId": "deep_jungle"}, timeout=90)
        record(results, "MoveToLocation_deep_jungle_expect_fail", r)

        r = client.call_tool("MoveToLocation", {"locationId": "jungle_edge"}, timeout=90)
        record(results, "MoveToLocation_jungle_edge", r)
        r = client.call_tool("MoveToLocation", {"locationId": "deep_jungle"}, timeout=90)
        record(results, "MoveToLocation_deep_jungle", r)

        # ---- Test B step 5: invalid location rejected ----
        r = client.call_tool("MoveToLocation", {"locationId": "atlantis"}, timeout=90)
        record(results, "MoveToLocation_atlantis_expect_fail", r)

        # ---- final state ----
        r = client.call_tool("GetGameState", timeout=90)
        record(results, "GetGameState_final", r)
    finally:
        client.close()

    ok = all(r.get("ok", False) for r in results)
    print(f"\nROUND-TRIP {'OK' if ok else 'WITH FAILURES'} — evidence: {evidence_file}")


if __name__ == "__main__":
    main()
