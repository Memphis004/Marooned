#!/usr/bin/env python3
"""Call a tool on the local gamedev-mcp-server (Unity MCP plugin).

Usage:
  python Tools/mcp_call.py <tool-name> [--arg key=value ...] [--json '<json-args>']
  python Tools/mcp_call.py --list

Each call bootstraps a fresh MCP session (initialize -> Mcp-Session-Id) because
the server is stateful. Endpoint/port come from the Freebuff client config.
"""
import argparse
import json
import sys
import urllib.request

URL = "http://localhost:24394/p/3a283544"
HEADERS = {
    "Content-Type": "application/json",
    "Accept": "application/json, text/event-stream",
}
INIT_PAYLOAD = {
    "jsonrpc": "2.0",
    "id": 1,
    "method": "initialize",
    "params": {
        "protocolVersion": "2025-03-26",
        "capabilities": {},
        "clientInfo": {"name": "codebuff-probe", "version": "0.1"},
    },
}


def _parse_sse_or_json(body: str):
    body = body.strip()
    if body.startswith("{"):
        return json.loads(body)
    for line in body.splitlines():
        if line.startswith("data:"):
            return json.loads(line[5:])
    raise RuntimeError(f"Unparseable response: {body[:400]}")


def _post(payload: dict, session_id: str | None = None, timeout: int = 60):
    headers = dict(HEADERS)
    if session_id:
        headers["Mcp-Session-Id"] = session_id
    req = urllib.request.Request(
        URL, data=json.dumps(payload).encode("utf-8"), headers=headers, method="POST"
    )
    with urllib.request.urlopen(req, timeout=timeout) as resp:
        sid = resp.headers.get("Mcp-Session-Id")
        body = resp.read().decode("utf-8", errors="replace")
        return sid, body


def _start_session() -> str:
    session_id, _ = _post(INIT_PAYLOAD, timeout=30)
    if not session_id:
        raise RuntimeError("Server did not return Mcp-Session-Id on initialize")
    return session_id


def rpc(session_id: str, method: str, params: dict, req_id: int = 2, timeout: int = 120):
    _, body = _post(
        {"jsonrpc": "2.0", "id": req_id, "method": method, "params": params},
        session_id=session_id,
        timeout=timeout,
    )
    return _parse_sse_or_json(body)


def list_tools():
    session_id = _start_session()
    data = rpc(session_id, "tools/list", {})
    for t in data["result"]["tools"]:
        print(t["name"])


def call_tool(name: str, arguments: dict, timeout: int = 120):
    session_id = _start_session()
    return rpc(session_id, "tools/call", {"name": name, "arguments": arguments}, timeout=timeout)


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if "--list" in sys.argv:
        list_tools()
        return
    parser = argparse.ArgumentParser()
    parser.add_argument("tool")
    parser.add_argument("--arg", action="append", default=[], help="key=value (value parsed as JSON, fallback string)")
    parser.add_argument("--json", help="full JSON arguments")
    parser.add_argument("--timeout", type=int, default=120)
    parser.add_argument("--max-output", type=int, default=200000, help="truncate printed JSON; 0 = no limit")
    args = parser.parse_args()

    arguments = {}
    if args.json:
        arguments = json.loads(args.json)
    for kv in args.arg:
        key, _, value = kv.partition("=")
        try:
            arguments[key] = json.loads(value)
        except json.JSONDecodeError:
            arguments[key] = value

    result = call_tool(args.tool, arguments, timeout=args.timeout)
    text = json.dumps(result, indent=1, ensure_ascii=False)
    limit = args.max_output
    print(text if limit <= 0 else text[:limit])


if __name__ == "__main__":
    main()
