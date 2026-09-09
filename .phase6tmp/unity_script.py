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

CSHARP = r'''
using System.Text;
using UnityEngine;
using Marooned.Core;
using Marooned.Systems;
using Marooned.Shared;

public class DumpNpcGroundTruth
{
    public static string Main()
    {
        var scope = Object.FindFirstObjectByType<GameLifetimeScope>();
        if (scope == null) return "NO_SCOPE";
        var director = scope.Container.Resolve<NpcDirectorSystem>();
        var data = scope.Container.Resolve<LubanDataService>();
        var player = scope.Container.Resolve<GameStateProvider>().GetPlayer();
        var sb = new StringBuilder();
        sb.Append("player_loc=").Append(player.CurrentLocationId).Append('\n');
        foreach (var npc in director.Npcs.Values)
        {
            data.LocationDefs.TryGetValue(npc.CurrentLocationId, out var locDef);
            sb.Append(npc.Id)
              .Append(" role=").Append(npc.Role)
              .Append(" alive=").Append(npc.IsAlive)
              .Append(" loc=").Append(npc.CurrentLocationId)
              .Append(" pos=(").Append(npc.PositionX.ToString("F2")).Append(',').Append(npc.PositionY.ToString("F2")).Append(')')
              .Append(" tgt=(").Append(npc.TargetX.ToString("F2")).Append(',').Append(npc.TargetY.ToString("F2")).Append(')')
              .Append(" zoneC=(").Append(locDef != null ? locDef.WorldX.ToString("F1") : "?").Append(',').Append(locDef != null ? locDef.WorldY.ToString("F1") : "?").Append(')')
              .Append(" act=").Append(npc.Activity);
            if (npc.Role == NpcRole.Killer)
                sb.Append(" weapon=").Append(npc.Inventory.HasItem("knife_basic") ? "knife_basic" : "NONE");
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
'''

call = {"jsonrpc":"2.0","id":2,"method":"tools/call","params":{"name":"script-execute","arguments":{"csharpCode":CSHARP,"className":"DumpNpcGroundTruth","methodName":"Main","isMethodBody":False}}}
res, _ = rpc(call, sid, timeout=90)
if res is None:
    print("NO_RESPONSE"); sys.exit(1)
if "error" in res:
    print("RPC_ERROR:", json.dumps(res["error"])[:500]); sys.exit(1)
content = res["result"]["content"][0]["text"]
print(content.encode("utf-8", errors="replace").decode("utf-8"))
