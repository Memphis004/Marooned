import json, sys, urllib.request
URL = "http://localhost:24394/p/3a283544"
def rpc(body, sid=None, timeout=60):
    req = urllib.request.Request(URL, data=json.dumps(body).encode(), method="POST")
    req.add_header("Content-Type", "application/json"); req.add_header("Accept", "application/json, text/event-stream")
    if sid: req.add_header("Mcp-Session-Id", sid)
    resp = urllib.request.urlopen(req, timeout=timeout)
    text = resp.read().decode("utf-8", errors="replace")
    for line in text.splitlines():
        if line.startswith("data: "): return json.loads(line[6:])
    return None
init = {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"probe","version":"0.1"}}}
r = rpc(init); sid = None
import urllib.request as u
# need session header: redo with headers captured
def rpc2(body, sid=None, timeout=60):
    req = u.Request(URL, data=json.dumps(body).encode(), method="POST")
    req.add_header("Content-Type", "application/json"); req.add_header("Accept", "application/json, text/event-stream")
    if sid: req.add_header("Mcp-Session-Id", sid)
    resp = u.urlopen(req, timeout=timeout)
    s = resp.headers.get("Mcp-Session-Id")
    text = resp.read().decode("utf-8", errors="replace")
    for line in text.splitlines():
        if line.startswith("data: "): return json.loads(line[6:]), s
    return None, s
_, sid = rpc2(init)
rpc2({"jsonrpc":"2.0","method":"notifications/initialized"}, sid)
call = {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"reflection-method-find","arguments":{
    "filter": {"typeName":"NpcGroundTruthProbe"}, "typeNameMatchLevel": 6, "methodNameMatchLevel": 0, "parametersMatchLevel": 0}}}
res, _ = rpc2(call, sid, timeout=60)
if res and "error" in res: print("RPC_ERROR:", json.dumps(res["error"])[:300])
else:
    txt = json.dumps(res.get("result",{}).get("structuredContent",{}))
    print(txt[:800].encode("utf-8", errors="replace").decode("utf-8"))
