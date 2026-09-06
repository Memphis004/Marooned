#!/usr/bin/env python3
"""Take a Unity screenshot via MCP and save the returned PNG to disk."""
import base64
import json
import sys

sys.path.insert(0, "Tools")
from mcp_call import call_tool  # noqa: E402


def main():
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    out_path = sys.argv[1]
    args = json.loads(sys.argv[2])
    result = call_tool("screenshot-isolated", args, timeout=180)
    content = result["result"]["content"]
    for item in content:
        if item.get("type") == "image":
            data = base64.b64decode(item["data"])
            with open(out_path, "wb") as f:
                f.write(data)
            print(f"SAVED {out_path} ({len(data)} bytes, {item.get('mimeType')})")
            return
    # no image — dump text content for debugging
    for item in content:
        print(str(item)[:800])
    print("NO-IMAGE-IN-RESPONSE")


if __name__ == "__main__":
    main()
