import json, sys, time, base64, urllib.request
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
def call(name, args, timeout=60):
    res, _ = rpc({"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":name,"arguments":args}}, sid, timeout=timeout)
    return res["result"] if res and "error" not in res else {"isError":True}
def refl(method):
    r = call("reflection-method-call", {"filter":{"namespace":"Marooned.EditorTools","typeName":"NpcGroundTruthProbe","methodName":method,"inputParameters":[]},
        "knownNamespace":True,"typeNameMatchLevel":5,"methodNameMatchLevel":5,"parametersMatchLevel":1,"inputParameters":[],"executeInMainThread":True})
    return r.get("structuredContent",{}).get("result",{}).get("value") if not r.get("isError") else "ERR:"+json.dumps(r)[:150]

for attempt in range(12):
    dump = refl("Dump")
    line = next((l for l in dump.splitlines() if "chibi@" in l), None)
    print("attempt", attempt, "|", line or "(no chibi)")
    if line and "act=Traveling" in line and "anim=Walking" in line:
        shot = call("screenshot-game-view", {})
        if not shot.get("isError"):
            for item in shot.get("content", []):
                if item.get("type")=="image":
                    png = base64.b64decode(item["data"])
                    open("./TestEvidence/step3_testC_spine_walk.png","wb").write(png)
                    print("SAVED walking shot:", len(png), "bytes")
                    sys.exit(0)
    time.sleep(1.5)
print("NOT_CAUGHT_WALKING")
