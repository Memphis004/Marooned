import json, sys, urllib.request

URL = "http://localhost:24394/p/3a283544"

def rpc(body, sid=None, timeout=60):
    req = urllib.request.Request(URL, data=json.dumps(body).encode(), method="POST")
    req.add_header("Content-Type", "application/json")
    req.add_header("Accept", "application/json, text/event-stream")
    if sid: req.add_header("Mcp-Session-Id", sid)
    resp = urllib.request.urlopen(req, timeout=timeout)
    sid_out = resp.headers.get("Mcp-Session-Id")
    text = resp.read().decode("utf-8", errors="replace")
    for line in text.splitlines():
        if line.startswith("data: "):
            return json.loads(line[6:]), sid_out
    return None, sid_out

init = {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"probe","version":"0.1"}}}
_, sid = rpc(init)
rpc({"jsonrpc":"2.0","method":"notifications/initialized"}, sid)

call = {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"reflection-method-call","arguments":{
    "filter": {"namespace":"Marooned.EditorTools","typeName":"NpcGroundTruthProbe","methodName":"Dump"},
    "knownNamespace": True,
    "typeNameMatchLevel": 6,
    "methodNameMatchLevel": 6,
    "parametersMatchLevel": 2,
    "executeInMainThread": True
}}}
res, _ = rpc(call, sid, timeout=90)
if res is None:
    print("NO_RESPONSE"); sys.exit(1)
if "error" in res:
    print("RPC_ERROR:", json.dumps(res["error"])[:600]); sys.exit(1)
r = res["result"]
if r.get("isError"):
    print("TOOL_ERROR:", json.dumps(r)[:600]); sys.exit(1)
sc = r.get("structuredContent", {})
val = sc.get("result", {}).get("value")
if val is None:
    print("RAW:", json.dumps(sc)[:600])
else:
    print(str(val).encode("utf-8", errors="replace").decode("utf-8"))
