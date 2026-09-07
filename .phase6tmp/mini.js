const { spawn } = require('child_process');
const proc = spawn('dotnet', ['bin/Debug/net8.0/McpBridge.dll'], { cwd: 'McpBridge' });
proc.stdout.on('data', d => process.stdout.write('[OUT] ' + d.toString().trim().slice(0,300) + '\n'));
proc.stderr.on('data', d => { const s = d.toString(); if (/error|exception|fail/i.test(s)) process.stdout.write('[ERR] ' + s.trim().split('\n')[0].slice(0,300) + '\n'); });
const send = o => proc.stdin.write(JSON.stringify(o) + '\n');
setTimeout(() => send({jsonrpc:'2.0',id:1,method:'initialize',params:{protocolVersion:'2024-11-05',capabilities:{},clientInfo:{name:'x',version:'1'}}}), 2000);
setTimeout(() => send({jsonrpc:'2.0',method:'notifications/initialized'}), 4200);
setTimeout(() => send({jsonrpc:'2.0',id:3,method:'tools/call',params:{name:'get_game_state',arguments:{}}}), 5000);
setTimeout(() => { proc.kill(); process.exit(0); }, 22000);
