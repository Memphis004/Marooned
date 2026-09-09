import json, sys, urllib.request
URL = "http://localhost:24394/p/3a283544"
def rpc(body, sid=None, timeout=60):
    req = urllib.request.Request(URL, data=json.dumps(body).encode(), method="POST")
    req.add_header("Content-Type", "application/json"); req.add_header("Accept", "application/json, text/event-stream")
    if sid: req.add_header("Mcp-Session-Id", sid)
    resp = urllib.request.urlopen(req, timeout=timeout)
    s = resp.headers.get("Mcp-Session-Id")
    text = resp.read().decode("utf-8", errors="replace")
    for line in text.splitlines():
        if line.startswith("data: "): return json.loads(line[6:]), s
    return None, s
_, sid = rpc({"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"probe","version":"0.1"}}})
rpc({"jsonrpc":"2.0","method":"notifications/initialized"}, sid)
call = {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"reflection-method-call","arguments":{
    "filter": {"namespace":"Marooned.EditorTools","typeName":"NpcGroundTruthProbe","methodName":"ForceGather","inputParameters":[]},
    "knownNamespace": True, "typeNameMatchLevel": 5, "methodNameMatchLevel": 5, "parametersMatchLevel": 1,
    "inputParameters": [], "executeInMainThread": True}}}
res, _ = rpc(call, sid, timeout=60)
if res and "error" not in res:
    r = res["result"]
    val = r.get("structuredContent", {}).get("result", {}).get("value")
    print(val if val is not None else json.dumps(r)[:200])
else:
    print("FAILED:", json.dumps(res)[:200] if res else "no response")
