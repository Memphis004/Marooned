#!/usr/bin/env python3
"""Call a Unity MCP tool over streamable HTTP.

Usage: python Tools/mcp_call.py <tool_name> ['{"json":"args"}']
Self-healing: re-initializes and retries when the session id is rejected.
"""
import json
import sys
import urllib.error
import urllib.request

URL = "http://localhost:24394/p/3a283544"
SID_FILE = "Tools/.mcp_sid"


def post(payload: dict, sid: str | None) -> tuple[int, str, str]:
    req = urllib.request.Request(
        URL,
        data=json.dumps(payload).encode(),
        headers={
            "Content-Type": "application/json",
            "Accept": "application/json, text/event-stream",
            **({"Mcp-Session-Id": sid} if sid else {}),
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(req, timeout=180) as r:
            return r.status, r.headers.get("Mcp-Session-Id", ""), r.read().decode()
    except urllib.error.HTTPError as e:
        return e.code, "", e.read().decode()


def parse_sse(body: str):
    for line in body.splitlines():
        if line.startswith("data: "):
            try:
                return json.loads(line[6:])
            except json.JSONDecodeError:
                pass
    try:
        return json.loads(body)
    except json.JSONDecodeError:
        return {"raw": body}


def init() -> str:
    code, sid, body = post({"jsonrpc": "2.0", "method": "initialize", "params": {
        "protocolVersion": "2024-11-05", "capabilities": {},
        "clientInfo": {"name": "buffy", "version": "1.0"}}, "id": 1}, None)
    if code != 200 or not sid:
        raise RuntimeError(f"initialize failed: HTTP {code}: {body[:300]}")
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, sid)
    with open(SID_FILE, "w") as f:
        f.write(sid)
    return sid


def call_tool(name: str, args: dict, retries: int = 3):
    last = None
    for _ in range(retries):
        try:
            sid = open(SID_FILE).read().strip()
        except FileNotFoundError:
            sid = ""
        if not sid:
            sid = init()
        code, new_sid, body = post({"jsonrpc": "2.0", "method": "tools/call",
                                    "params": {"name": name, "arguments": args},
                                    "id": 99}, sid)
        if code == 200:
            if new_sid and new_sid != sid:
                with open(SID_FILE, "w") as f:
                    f.write(new_sid)
            return parse_sse(body)
        last = f"HTTP {code}: {body[:300]}"
        sid = init()  # session rejected — get a fresh one and retry
    raise RuntimeError(f"tool call '{name}' failed after retries: {last}")


if __name__ == "__main__":
    tool = sys.argv[1]
    args = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
    result = call_tool(tool, args)
    print(json.dumps(result, indent=1, ensure_ascii=False))
