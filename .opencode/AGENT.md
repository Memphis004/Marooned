# AI LLM WIKI LITE — Operating Manual for Claude

This folder is a personal wiki, focused initially on **LLMs and Andrej Karpathy's teaching material**. It is designed to be:

- **Plain markdown** — every note is a `.md` file under `notes/`. You can read or edit them anywhere.
- **Obsidian-style** — notes use `[[wikilinks]]`. The HTML viewer renders a graph of how they connect.
- **Zero install, zero pick** — the user double-clicks `wiki.html` and the wiki appears with the graph view first. No folder picker, no setup.
- **Self-contained** — this file is the *only* artifact you need to recreate the wiki. `wiki.html` (§7.3), the `notes-bundle.js` regen procedure (§7.4), and the seed templates (§7.2) all live verbatim inside this document. Drop `AGENT.md` into an empty Cowork folder, ask Claude to bootstrap, and the entire system rebuilds itself from §7.

---

## 0. Bootstrap protocol (read this FIRST in any new instance)

**On your very first turn in this folder, before responding to anything else:**

1. Check whether `wiki.html`, `notes/`, `notes/index.md`, `notes/karpathy.md`, and `notes-bundle.js` all exist. The `ls` shell command works.
2. If **any** of them are missing, you are in a fresh instance. Run the [§7 Bootstrap checklist](#7-bootstrap) immediately to rebuild the missing pieces from this file. Do not ask the user for permission — they expect this to "just work."
3. Then handle their actual request.

If everything is present, skip bootstrap and go straight to the request.

This protocol is what makes the wiki **portable**: copy `CLAUDE.md` alone into a fresh Cowork folder and the next Claude instance reconstructs `wiki.html`, the seed notes, and the bundle from §7 — no human steps.

---

## 1. How the user uses this

1. The user double-clicks `wiki.html`. It opens in their default browser.
2. The viewer auto-loads notes from the sibling `notes-bundle.js` and **boots into Graph view** by default. There is **no folder picker** — `wiki.html` is hard-wired to load the bundle next to it.
3. The user clicks any node in the graph → that note opens in **Note view**. The header `Graph` / `Note` toggle moves between the two.
4. To add knowledge: paste a link, drop a file, or describe a topic in chat. Claude reads section [§5](#5-workflow-when-the-user-shares-something), updates `notes/`, and **regenerates `notes-bundle.js`**. The viewer **auto-reloads within ~3 seconds** — the user does not need to refresh.

The user does **not** need to manually edit `.md` files, pick a folder, or refresh the page. Their job is to feed sources; Claude's job is to atomize, link, integrate, and keep the bundle in sync.

**How auto-reload works.** Each `notes-bundle.js` carries a `window.WIKI_NOTES_STAMP` (seconds-since-epoch). The viewer polls the bundle every 3 seconds by injecting a fresh `<script>` tag (this works on `file://` URLs where `fetch()` is blocked). When the stamp changes, the viewer saves the current note + view mode to `localStorage` and calls `location.reload()`. On the next load, that session is restored, so the reload is nearly invisible. **This requires §7.4 to write the stamp** — don't skip it.

---

## 2. Folder layout

```
AI LLM WIKI LITE/
├── AGENT.md         ← this file (the brain — everything regenerates from here)
├── wiki.html          ← single-file viewer (regenerable from §7.3)
├── notes-bundle.js    ← auto-generated; the viewer loads this on open (see §7.4)
└── notes/
    ├── index.md       ← root Map-of-Content (MOC). Always exists.
    ├── karpathy.md    ← MOC for Karpathy material
    └── *.md           ← one concept per file, flat directory
```

**Flat is intentional.** Don't create subfolders inside `notes/`. Discoverability comes from wikilinks, tags, and MOCs — not nesting.

`notes-bundle.js` is **derived** — it always reflects the current state of `notes/`. Treat it as a build artifact, not a source of truth.

---

## 3. Note format

Every note is markdown with optional YAML frontmatter:

```markdown
---
title: Self-Attention
tags: [transformer, architecture, core]
source: https://www.youtube.com/watch?v=...
date: 2026-05-10
---

A short one-paragraph definition. What it is, in plain language, in 2–4 sentences.

## Key idea

Then the explanation, with [[wikilinks]] to every related concept.
Wikilinks are how the graph forms — be generous with them.

## Why it matters

Connect to [[Transformer]], compare to [[RNN]], note prerequisites like [[Softmax]].

## Sources
- [[Karpathy — Let's build GPT]] (timestamp 23:14)
- https://arxiv.org/abs/1706.03762
```

### Conventions

- **Filename:** `kebab-case-title.md`. The viewer slugifies titles to match, so `[[Self-Attention]]` resolves to `self-attention.md`.
- **Title:** human-readable, capitalized normally. Set explicitly in frontmatter when filename and title differ.
- **One concept per note.** If a note grows beyond ~400 words and covers two ideas, split it.
- **Wikilinks `[[Title]]`** for any internal concept; reserve plain markdown links for external URLs.
- **Pipe form `[[Self-Attention|attention]]`** when the display text should differ from the target.
- **Tags** in frontmatter (`tags: [a, b]`) and/or inline as `#tag`. Both are picked up.
- **Sources** go in a `Sources` section at the bottom, plus `source:` in frontmatter for the primary one.
- Don't write a "Backlinks" section — the viewer renders backlinks automatically.

---

## 4. Tag taxonomy (use sparingly)

Pick tags from this controlled list when relevant; coin new ones only when none fit.

- **Layer / role:** `core`, `architecture`, `training`, `inference`, `data`, `evaluation`, `deployment`, `safety`, `hardware`
- **Topic:** `transformer`, `tokenization`, `attention`, `optimization`, `rl`, `rlhf`, `scaling`, `agents`, `multimodal`
- **Source type:** `karpathy`, `paper`, `talk`, `repo`, `blog`, `course`
- **Status:** `stub` (note exists but is thin), `moc` (Map of Content / index page)

A note typically has 2–4 tags. Avoid tag soup.

---

## 5. Workflow when the user shares something

When the user pastes a link, drops a file, or describes a concept, follow this loop:

### 5.1 Read & atomize
Read the source fully (use WebFetch / Read tools). List the **atomic concepts** it contains — each thing that deserves its own note. A 1-hour Karpathy lecture might atomize into 10–20 concepts.

### 5.2 Diff against existing wiki
For each atomic concept, decide:
- **New note** — concept doesn't exist yet → create `notes/<slug>.md`.
- **Augment** — note exists but the source adds new info → edit the existing note, keep prose tight, add a citation.
- **Skip** — already well covered.

### 5.3 Write notes
For each new/updated note:
- Frontmatter with `title`, `tags`, `source` (URL or `[[Source Note]]`), `date`.
- Open with a 2–4 sentence definition that would satisfy a smart beginner.
- Add `[[wikilinks]]` to every related concept that exists OR should exist. Broken wikilinks are visible in the graph (red, dashed) and are a feature — they tell the user what to add next.
- Keep notes short. If you're tempted to write 600 words, split.

### 5.4 Create a source note
For substantive sources (a video, paper, repo, blog post), create a dedicated `notes/<source-slug>.md` with `tags: [source-type, ...]`. Concept notes link to the source note via `[[wikilink]]`. This is how the wiki tracks provenance.

### 5.5 Update MOCs
- Always add new concept notes to a relevant section in `notes/index.md`.
- If the concept fits an existing topic MOC (e.g. `karpathy.md`, `transformer.md`), add it there too.
- MOCs are curated lists — group notes by sub-topic, not alphabetically.

### 5.6 Report back to the user
After integrating a source, post a short summary in chat:
- N notes created, M notes updated.
- The 3–5 most interesting links added (broken wikilinks worth filling in next).
- Don't dump file contents — link them: `[Self-Attention](computer:///Users/sippanonwichiramala/Desktop/UP AI/AI LLM WIKI LITE/notes/self-attention.md)`.
- No need to ask the user to refresh — the viewer auto-reloads within ~3 seconds of `notes-bundle.js` changing (see §1).

### 5.7 ⭐ Regenerate `notes-bundle.js` (mandatory after every note change)
**Whenever you create, edit, rename, or delete any file under `notes/`, immediately regenerate `notes-bundle.js`** so the viewer stays in sync. Use the procedure in §7.4. The viewer is polling the bundle every 3 seconds and will auto-reload as soon as the new stamp lands. Skipping this step means the user is staring at stale data — which silently breaks the whole loop.

### 5.8 Don't touch
- Never edit `wiki.html` unless the user explicitly asks for a viewer change. It's regenerable from §7.3 and changes there will be lost on the next bootstrap.
- Never restructure existing notes wholesale. Augment in place.
- Never edit `notes-bundle.js` by hand. Always regenerate it from `notes/` (§7.4).

---

## 6. Karpathy seed topics

When `notes/index.md` is being seeded for the first time, pre-populate links (as broken wikilinks — they become real notes as content arrives) for these clusters. This gives the user a visible scaffold in the graph from day one.

- **Foundations:** [[Neural Network]], [[Backpropagation]], [[Gradient Descent]], [[Softmax]], [[Cross-Entropy Loss]], [[Embedding]]
- **Architectures:** [[Transformer]], [[Self-Attention]], [[Multi-Head Attention]], [[Positional Encoding]], [[Layer Norm]], [[Residual Connection]], [[MLP Block]], [[RNN]], [[LSTM]]
- **Tokenization:** [[BPE]], [[Tokenizer]], [[Vocabulary]], [[SentencePiece]]
- **Training:** [[Pretraining]], [[Fine-Tuning]], [[Loss Curve]], [[Learning Rate Schedule]], [[Adam]], [[AdamW]], [[Mixed Precision]], [[Distributed Training]]
- **Alignment:** [[SFT]], [[RLHF]], [[DPO]], [[Reward Model]], [[Constitutional AI]]
- **Inference:** [[Sampling]], [[Temperature]], [[Top-k Sampling]], [[Top-p Sampling]], [[KV Cache]], [[Speculative Decoding]]
- **Scaling:** [[Scaling Laws]], [[Chinchilla]], [[Parameter Count]], [[Compute Optimal]]
- **Karpathy artifacts:** [[micrograd]], [[makemore]], [[nanoGPT]], [[nanochat]], [[LLM101n]], [[Software 2.0]], [[Software 3.0]]
- **Karpathy lectures:** [[Let's build GPT]], [[Let's build the GPT Tokenizer]], [[Let's reproduce GPT-2]], [[Intro to LLMs]], [[State of GPT]], [[Neural Networks Zero to Hero]]

---

## 7. Bootstrap

On first run in a fresh Cowork instance, check whether the system is intact and rebuild missing pieces.

### 7.1 Checklist
Run these in order. Each step is idempotent — if the file already exists, skip it.

1. Does `wiki.html` exist? If not, write the contents of [§7.3](#73-wikihtml-source) **verbatim** to `wiki.html`.
2. Does `notes/` exist? If not, create the directory.
3. Does `notes/index.md` exist? If not, create it from the **`index.md` template** in [§7.2](#72-seed-templates). Replace `<today>` with today's date in `YYYY-MM-DD`.
4. Does `notes/karpathy.md` exist? If not, create it from the **`karpathy.md` template** in [§7.2](#72-seed-templates). Replace `<today>` with today's date.
5. Does `notes-bundle.js` exist? Run [§7.4](#74-notes-bundlejs-regeneration-procedure) to (re)generate it from whatever's in `notes/`. Always do this last so the bundle reflects steps 3–4.
6. Tell the user, in one line: *"Wiki bootstrapped. Open `wiki.html` and it'll boot straight into the graph view."*

After bootstrap, the user can immediately start feeding sources (§5). The seed graph will be mostly red (broken wikilinks) — that's the intended scaffold.

### 7.2 Seed templates

Two files. Replace `<today>` with today's date in `YYYY-MM-DD` format when writing them.

#### `notes/index.md`

```markdown
---
title: Index
tags: [moc]
date: <today>
---

The root map of this wiki. Every concept note should appear under one section here.

Most links below are intentionally **broken** (shown red in the viewer) — they're a roadmap of what to learn next. Each turns into a real note when you feed Claude a source about that topic.

## Foundations
[[Neural Network]] · [[Backpropagation]] · [[Gradient Descent]] · [[Softmax]] · [[Cross-Entropy Loss]] · [[Embedding]]

## Architectures
[[Transformer]] · [[Self-Attention]] · [[Multi-Head Attention]] · [[Positional Encoding]] · [[Layer Norm]] · [[Residual Connection]] · [[MLP Block]] · [[RNN]] · [[LSTM]]

## Tokenization
[[BPE]] · [[Tokenizer]] · [[Vocabulary]] · [[SentencePiece]]

## Training
[[Pretraining]] · [[Fine-Tuning]] · [[Adam]] · [[AdamW]] · [[Learning Rate Schedule]] · [[Mixed Precision]] · [[Distributed Training]]

## Alignment
[[SFT]] · [[RLHF]] · [[DPO]] · [[Reward Model]] · [[Constitutional AI]]

## Inference
[[Sampling]] · [[Temperature]] · [[Top-k Sampling]] · [[Top-p Sampling]] · [[KV Cache]] · [[Speculative Decoding]]

## Scaling
[[Scaling Laws]] · [[Chinchilla]] · [[Compute Optimal]] · [[Parameter Count]]

## Agents
[[Agents]] (MOC) · [[Agentic Systems]] · [[Workflow]] · [[Autonomous Agent]] · [[Augmented LLM]] · [[Tool Use]] · [[Agent-Computer Interface]] · [[Model Context Protocol]]

### Workflow patterns
[[Prompt Chaining]] · [[Routing]] · [[Parallelization]] · [[Orchestrator-Workers]] · [[Evaluator-Optimizer]]

### Agent applications
[[Coding Agent]] · [[Customer Support Agent]]

## People & sources
[[Karpathy]] · [[Building Effective Agents]]
```

#### `notes/karpathy.md`

```markdown
---
title: Karpathy
tags: [moc, karpathy]
source: https://karpathy.ai
date: <today>
---

Andrej Karpathy. Co-founder of OpenAI, former director of AI at Tesla. Now best known for the *Neural Networks: Zero to Hero* lecture series and a string of pedagogically clean codebases that let you build LLMs from first principles.

## Lectures
[[Neural Networks Zero to Hero]] · [[Let's build GPT]] · [[Let's build the GPT Tokenizer]] · [[Let's reproduce GPT-2]] · [[Intro to LLMs]] · [[State of GPT]]

## Codebases
[[micrograd]] · [[makemore]] · [[nanoGPT]] · [[nanochat]] · [[LLM101n]]

## Essays / talks
[[Software 2.0]] · [[Software 3.0]]

## Recurring ideas
- Build the smallest working version, then scale.
- Tokenization is where most LLM weirdness comes from. See [[BPE]].
- A transformer is just *attention + MLP, repeated*. See [[Transformer]].
- The hardest part of training is the data, not the model.

Feed Claude a Karpathy video, repo, or post and it will atomize the concepts into individual notes and link them back here.

## Sources
- https://karpathy.ai
- https://github.com/karpathy
- https://www.youtube.com/@AndrejKarpathy
```

### 7.3 `wiki.html` source

The full source of the viewer follows. Write it verbatim to `wiki.html`. Do not "improve" it during bootstrap — only edit when the user explicitly asks for a viewer change.

````html
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<title>LLM Wiki</title>
<meta name="viewport" content="width=device-width,initial-scale=1">
<style>
  :root{
    --bg:#1e1e1e; --bg2:#252525; --bg3:#2d2d2d;
    --fg:#dcdcdc; --fg-dim:#9a9a9a; --fg-faint:#6a6a6a;
    --accent:#7f9cf5; --accent-2:#a78bfa; --tag:#3b3b3b;
    --border:#333; --hl:#3a3a3a;
    --link:#8ab4f8; --code-bg:#2a2a2a;
  }
  *{box-sizing:border-box}
  html,body{margin:0;height:100%;background:var(--bg);color:var(--fg);font-family:-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Inter,sans-serif;font-size:14px;line-height:1.55}
  a{color:var(--link);text-decoration:none}
  a:hover{text-decoration:underline}
  button{background:var(--bg3);color:var(--fg);border:1px solid var(--border);padding:6px 10px;border-radius:6px;cursor:pointer;font-size:13px}
  button:hover{background:var(--hl)}
  input[type=text]{background:var(--bg2);color:var(--fg);border:1px solid var(--border);border-radius:6px;padding:6px 10px;font-size:13px;outline:none;width:100%}
  input[type=text]:focus{border-color:var(--accent)}
  .seg{display:inline-flex;border:1px solid var(--border);border-radius:6px;overflow:hidden}
  .seg button{border:none;border-radius:0;padding:6px 12px;background:transparent}
  .seg button.on{background:var(--accent);color:#fff}
  .seg button + button{border-left:1px solid var(--border)}
  #app{display:grid;grid-template-rows:48px 1fr;height:100vh}
  header{display:flex;align-items:center;gap:12px;padding:0 14px;border-bottom:1px solid var(--border);background:var(--bg2)}
  header .title{font-weight:600;color:var(--fg);letter-spacing:.3px}
  header .spacer{flex:1}
  header .meta{color:var(--fg-faint);font-size:12px}
  main{display:grid;min-height:0}
  body.mode-graph main{grid-template-columns:260px 1fr}
  body.mode-note  main{grid-template-columns:260px 1fr 360px}
  aside{border-right:1px solid var(--border);overflow:hidden;display:flex;flex-direction:column;background:var(--bg2)}
  aside.right{border-right:none;border-left:1px solid var(--border)}
  body.mode-graph aside.right{display:none}
  .pane-h{font-size:11px;text-transform:uppercase;letter-spacing:.7px;color:var(--fg-faint);padding:10px 12px 6px}
  .search{padding:10px 10px 4px}
  .nlist{overflow:auto;flex:1}
  .nlist .item{padding:6px 12px;cursor:pointer;color:var(--fg);border-left:2px solid transparent;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
  .nlist .item:hover{background:var(--bg3)}
  .nlist .item.active{background:var(--bg3);border-left-color:var(--accent);color:#fff}
  .nlist .item .tagchip{display:inline-block;font-size:10px;color:var(--fg-faint);margin-left:6px}
  .center{overflow:hidden;display:flex;flex-direction:column;min-height:0}
  body.mode-note .center{padding:28px 40px;overflow:auto}
  body.mode-graph .center{padding:0}
  .docWrap{flex:1;overflow:auto}
  body.mode-graph .docWrap{display:none}
  body.mode-note .docWrap{display:block}
  .doc{max-width:760px;margin:0 auto}
  .doc h1{font-size:28px;margin:.2em 0 .4em;border-bottom:1px solid var(--border);padding-bottom:.3em}
  .doc h2{font-size:21px;margin:1.2em 0 .4em}
  .doc h3{font-size:17px;margin:1em 0 .3em}
  .doc p{margin:.6em 0}
  .doc code{background:var(--code-bg);padding:1px 6px;border-radius:4px;font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace;font-size:.9em}
  .doc pre{background:var(--code-bg);padding:12px 14px;border-radius:8px;overflow:auto}
  .doc pre code{background:transparent;padding:0}
  .doc blockquote{border-left:3px solid var(--accent);margin:.6em 0;padding:.2em 12px;color:var(--fg-dim);background:var(--bg2)}
  .doc ul,.doc ol{padding-left:24px;margin:.4em 0}
  .doc hr{border:none;border-top:1px solid var(--border);margin:1.4em 0}
  .doc img{max-width:100%;border-radius:6px}
  .doc table{border-collapse:collapse;margin:.6em 0}
  .doc th,.doc td{border:1px solid var(--border);padding:6px 10px}
  .doc .wl{color:var(--accent-2);cursor:pointer}
  .doc .wl.broken{color:#e07a7a;border-bottom:1px dashed #e07a7a}
  .doc .wl:hover{text-decoration:underline}
  .meta-row{color:var(--fg-faint);font-size:12px;margin-top:6px;display:flex;flex-wrap:wrap;gap:6px;align-items:center}
  .tag{background:var(--tag);color:var(--fg-dim);padding:2px 8px;border-radius:99px;font-size:11px;cursor:pointer}
  .tag:hover{background:var(--hl);color:#fff}
  #centerGraphSlot{flex:1;position:relative;min-height:0}
  body.mode-note #centerGraphSlot{display:none}
  .right .graph-wrap{height:55%;border-bottom:1px solid var(--border);position:relative}
  .right canvas, #centerGraphSlot canvas{width:100%;height:100%;display:block;cursor:grab}
  canvas:active{cursor:grabbing}
  .right .backlinks{height:45%;overflow:auto;padding:6px 0}
  .right .backlinks .bl{padding:6px 12px;cursor:pointer;color:var(--fg-dim);font-size:13px}
  .right .backlinks .bl:hover{background:var(--bg3);color:#fff}
  .right .backlinks .bl small{color:var(--fg-faint);display:block;font-size:11px;margin-top:2px}
  .empty{padding:40px;color:var(--fg-faint);text-align:center;line-height:1.7}
  .empty kbd{background:var(--bg3);border:1px solid var(--border);border-radius:4px;padding:1px 6px;font-size:.9em}
  .filter-bar{display:flex;flex-wrap:wrap;gap:4px;padding:0 10px 8px}
  .filter-bar .tag.on{background:var(--accent);color:#fff}
  .gtip{position:absolute;top:10px;left:14px;font-size:12px;color:var(--fg-faint);pointer-events:none}
  body.mode-note .gtip{display:none}
  @media (max-width:1100px){
    body.mode-graph main{grid-template-columns:220px 1fr}
    body.mode-note  main{grid-template-columns:220px 1fr 300px}
  }
  @media (max-width:820px){
    body.mode-graph main, body.mode-note main{grid-template-columns:1fr}
    aside{display:none}
  }
</style>
</head>
<body class="mode-graph">
<div id="app">
  <header>
    <div class="title">LLM Wiki</div>
    <div class="seg">
      <button id="modeGraph" class="on" title="Graph view">Graph</button>
      <button id="modeNote" title="Note view">Note</button>
    </div>
    <div class="spacer"></div>
    <div class="meta" id="stats"></div>
    <div class="meta" id="liveDot" title="Watching notes-bundle.js for changes" style="color:#5fb37e">●</div>
  </header>
  <main>
    <aside class="left">
      <div class="search"><input id="search" type="text" placeholder="Search notes…"></div>
      <div class="filter-bar" id="tagFilter"></div>
      <div class="pane-h">Notes</div>
      <div class="nlist" id="nlist"></div>
    </aside>
    <section class="center">
      <div id="centerGraphSlot">
        <canvas id="graph"></canvas>
        <div class="gtip">click a node to open · drag to rearrange</div>
      </div>
      <div class="docWrap" id="docWrap">
        <div class="doc" id="doc">
          <div class="empty">
            <p><strong>No notes loaded.</strong></p>
            <p>This wiki auto-loads from <code>notes-bundle.js</code> next to <code>wiki.html</code>.</p>
            <p>If the bundle is missing, ask Claude to <em>regenerate the wiki</em> — it knows how (see <code>CLAUDE.md</code>).</p>
          </div>
        </div>
      </div>
    </section>
    <aside class="right">
      <div class="graph-wrap" id="rightGraphSlot"></div>
      <div class="backlinks" id="backlinks">
        <div class="pane-h">Backlinks</div>
      </div>
    </aside>
  </main>
</div>

<script src="notes-bundle.js" onerror="window.__BUNDLE_FAILED=true"></script>
<script>
"use strict";
const STATE = { notes:new Map(), bySlug:new Map(), byTitle:new Map(), current:null, search:"", tagFilter:new Set() };
const slugify = s => (s||"").toLowerCase().trim().replace(/\.md$/,"").replace(/[^\w\s\-]/g," ").replace(/\s+/g,"-").replace(/-+/g,"-").replace(/^-|-$/g,"");
function escapeHtml(s){return s.replace(/[&<>"]/g,c=>({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;"}[c]));}
function parseFrontmatter(raw){
  const m = raw.match(/^---\s*\r?\n([\s\S]*?)\r?\n---\s*\r?\n?/);
  if(!m) return {data:{}, body:raw};
  const data={}; const lines = m[1].split(/\r?\n/);
  for(const line of lines){
    const km = line.match(/^([A-Za-z0-9_-]+)\s*:\s*(.*)$/); if(!km) continue;
    let v = km[2].trim();
    if(/^\[.*\]$/.test(v)){ v = v.slice(1,-1).split(",").map(x=>x.trim().replace(/^["']|["']$/g,"")).filter(Boolean); }
    else { v = v.replace(/^["']|["']$/g,""); }
    data[km[1]] = v;
  }
  return {data, body: raw.slice(m[0].length)};
}
function mdToHtml(src){
  const lines = src.split(/\r?\n/); let out=[]; let i=0;
  const inline = (t) => {
    let parts = []; let last=0;
    t.replace(/`([^`]+)`/g,(m,g,idx)=>{ parts.push([t.slice(last,idx),false]); parts.push([g,true]); last=idx+m.length; return m; });
    parts.push([t.slice(last),false]);
    return parts.map(([s,isCode])=>{
      if(isCode) return "<code>"+escapeHtml(s)+"</code>";
      let x = escapeHtml(s);
      x = x.replace(/!\[([^\]]*)\]\(([^)]+)\)/g,'<img alt="$1" src="$2">');
      x = x.replace(/\[\[([^\]|]+)(?:\|([^\]]+))?\]\]/g,(m,target,disp)=>{
        const slug = slugify(target);
        const exists = STATE.notes.has(slug) || STATE.byTitle.has(target.trim().toLowerCase());
        const label = (disp || target).trim();
        return `<span class="wl${exists?'':' broken'}" data-slug="${escapeHtml(slug)}" data-title="${escapeHtml(target.trim())}">${escapeHtml(label)}</span>`;
      });
      x = x.replace(/\[([^\]]+)\]\(([^)]+)\)/g,'<a href="$2" target="_blank" rel="noopener">$1</a>');
      x = x.replace(/\*\*([^*]+)\*\*/g,"<strong>$1</strong>");
      x = x.replace(/(^|[^*])\*([^*\n]+)\*/g,"$1<em>$2</em>");
      x = x.replace(/(^|\s)#([A-Za-z0-9_\-\/]+)/g,(m,s,t)=>`${s}<span class="tag">#${t}</span>`);
      return x;
    }).join("");
  };
  while(i<lines.length){
    const ln = lines[i];
    const fm = ln.match(/^```(.*)$/);
    if(fm){ const buf=[]; i++; while(i<lines.length && !/^```/.test(lines[i])){ buf.push(lines[i]); i++; } i++; out.push(`<pre><code>${escapeHtml(buf.join("\n"))}</code></pre>`); continue; }
    const hm = ln.match(/^(#{1,6})\s+(.*)$/);
    if(hm){ const lvl=hm[1].length; out.push(`<h${lvl}>${inline(hm[2])}</h${lvl}>`); i++; continue; }
    if(/^---\s*$/.test(ln)){ out.push("<hr>"); i++; continue; }
    if(/^>\s?/.test(ln)){ const buf=[]; while(i<lines.length && /^>\s?/.test(lines[i])){ buf.push(lines[i].replace(/^>\s?/,"")); i++; } out.push(`<blockquote>${inline(buf.join(" "))}</blockquote>`); continue; }
    if(/^\s*[-*]\s+/.test(ln)){ const buf=[]; while(i<lines.length && /^\s*[-*]\s+/.test(lines[i])){ buf.push(lines[i].replace(/^\s*[-*]\s+/,"")); i++; } out.push("<ul>"+buf.map(b=>"<li>"+inline(b)+"</li>").join("")+"</ul>"); continue; }
    if(/^\s*\d+\.\s+/.test(ln)){ const buf=[]; while(i<lines.length && /^\s*\d+\.\s+/.test(lines[i])){ buf.push(lines[i].replace(/^\s*\d+\.\s+/,"")); i++; } out.push("<ol>"+buf.map(b=>"<li>"+inline(b)+"</li>").join("")+"</ol>"); continue; }
    if(/^\s*$/.test(ln)){ i++; continue; }
    const buf=[ln]; i++;
    while(i<lines.length && !/^\s*$/.test(lines[i]) && !/^(#{1,6}\s|>|```|---\s*$|\s*[-*]\s+|\s*\d+\.\s+)/.test(lines[i])){ buf.push(lines[i]); i++; }
    out.push("<p>"+inline(buf.join(" "))+"</p>");
  }
  return out.join("\n");
}

/* ---------- ingestion ---------- */
function ingestRecords(records){
  STATE.notes.clear(); STATE.bySlug.clear(); STATE.byTitle.clear();
  for(const {path, raw} of records){
    const {data, body} = parseFrontmatter(raw);
    const baseTitle = (data.title) || path.split("/").pop().replace(/\.md$/i,"");
    const slug = slugify(baseTitle);
    let tags = data.tags || []; if(typeof tags === "string") tags = tags.split(/[,\s]+/).filter(Boolean);
    for(const m of body.matchAll(/(?:^|\s)#([A-Za-z0-9_\-\/]+)/g)){ if(!tags.includes(m[1])) tags.push(m[1]); }
    const note = { slug, title:baseTitle, path, raw, body, frontmatter:data, tags, outLinks:new Set(), inLinks:new Set() };
    STATE.notes.set(slug, note); STATE.bySlug.set(slug, note); STATE.byTitle.set(baseTitle.trim().toLowerCase(), note);
  }
  for(const n of STATE.notes.values()){
    for(const m of n.body.matchAll(/\[\[([^\]|]+)(?:\|[^\]]+)?\]\]/g)){
      const target = m[1].trim(); const slug = slugify(target);
      const tgt = STATE.notes.get(slug) || STATE.byTitle.get(target.toLowerCase());
      if(tgt && tgt.slug !== n.slug){ n.outLinks.add(tgt.slug); tgt.inLinks.add(n.slug); }
    }
  }
  document.getElementById("stats").textContent = `${STATE.notes.size} notes`;
  rebuildTagFilter(); renderList();
  initGraph();
  // pick a default current note (so backlinks/active states have something to anchor on)
  const def = STATE.notes.get("index") || STATE.notes.get("karpathy") || STATE.notes.values().next().value;
  if(def){
    if(document.body.classList.contains("mode-note")) openNote(def.slug);
    else { STATE.current = def.slug; renderList(); renderBacklinks(def); drawGraph(); }
  }
}
function ingestBundled(arr){
  const records = arr.map(o => ({ path: "notes/" + (o.name || ""), raw: o.text || "" }));
  ingestRecords(records);
}

/* ---------- list / sidebar ---------- */
function visibleNotes(){
  const q = STATE.search.toLowerCase(); const tags = STATE.tagFilter;
  return [...STATE.notes.values()].filter(n=>{
    if(q && !(n.title.toLowerCase().includes(q) || n.body.toLowerCase().includes(q))) return false;
    if(tags.size){ for(const t of tags) if(!n.tags.includes(t)) return false; }
    return true;
  }).sort((a,b)=>a.title.localeCompare(b.title));
}
function renderList(){
  const el = document.getElementById("nlist"); const items = visibleNotes();
  el.innerHTML = items.map(n=>`<div class="item${STATE.current===n.slug?' active':''}" data-slug="${n.slug}">${escapeHtml(n.title)}${n.tags.length?`<span class="tagchip">${escapeHtml(n.tags.slice(0,2).join(' '))}</span>`:""}</div>`).join("") || `<div class="empty">No notes match.</div>`;
}
function rebuildTagFilter(){
  const tags = new Map();
  for(const n of STATE.notes.values()) for(const t of n.tags) tags.set(t,(tags.get(t)||0)+1);
  const sorted = [...tags.entries()].sort((a,b)=>b[1]-a[1]).slice(0,20);
  const el = document.getElementById("tagFilter");
  el.innerHTML = sorted.map(([t,c])=>`<span class="tag${STATE.tagFilter.has(t)?' on':''}" data-tag="${escapeHtml(t)}">#${escapeHtml(t)} <small>${c}</small></span>`).join("");
}

/* ---------- open / render ---------- */
function openNote(slug){
  const n = STATE.notes.get(slug); if(!n) return;
  STATE.current = slug;
  const tagsHtml = n.tags.map(t=>`<span class="tag" data-tag="${escapeHtml(t)}">#${escapeHtml(t)}</span>`).join("");
  const src = n.frontmatter.source ? `<a href="${escapeHtml(n.frontmatter.source)}" target="_blank" rel="noopener">source</a>` : "";
  const date = n.frontmatter.date ? escapeHtml(n.frontmatter.date) : "";
  const meta = [date, src].filter(Boolean).join(" · ");
  document.getElementById("doc").innerHTML =
    `<h1>${escapeHtml(n.title)}</h1><div class="meta-row">${tagsHtml}${meta?` <span style="color:var(--fg-faint)">${meta}</span>`:""}</div>${mdToHtml(n.body)}`;
  renderBacklinks(n); renderList(); drawGraph();
  const dw = document.getElementById("docWrap"); if(dw) dw.scrollTop=0;
}
function renderBacklinks(n){
  const wrap = document.getElementById("backlinks");
  const list = [...n.inLinks].map(s=>STATE.notes.get(s)).filter(Boolean);
  wrap.innerHTML = `<div class="pane-h">Backlinks (${list.length})</div>` +
    (list.length? list.map(b=>{
      const line = (b.body.split(/\r?\n/).find(l=>l.includes(`[[${n.title}]]`)||l.toLowerCase().includes(`[[${n.title.toLowerCase()}`)||l.includes(`[[${n.slug}`))||"").trim().slice(0,160);
      return `<div class="bl" data-slug="${b.slug}">${escapeHtml(b.title)}<small>${escapeHtml(line)}</small></div>`;
    }).join("") : `<div class="empty" style="padding:14px">No backlinks yet.</div>`);
}

/* ---------- graph ---------- */
const G = { nodes:[], edges:[], byId:new Map(), canvas:null, ctx:null, raf:null, mouse:null, drag:null, hover:null };
function initGraph(){
  G.canvas = document.getElementById("graph"); G.ctx = G.canvas.getContext("2d");
  resizeGraph(); buildGraphData();
  if(!G._listeners){
    G._listeners=true;
    window.addEventListener("resize",()=>{resizeGraph();drawGraph();});
    G.canvas.addEventListener("mousemove",onGraphMove);
    G.canvas.addEventListener("mousedown",onGraphDown);
    window.addEventListener("mouseup",onGraphUp);
    G.canvas.addEventListener("click",onGraphClick);
  }
  if(!G.raf) tick();
}
function resizeGraph(){
  if(!G.canvas) return;
  const r = G.canvas.parentElement.getBoundingClientRect();
  const dpr = window.devicePixelRatio||1;
  G.canvas.width = r.width*dpr; G.canvas.height = r.height*dpr;
  G.canvas.style.width=r.width+"px"; G.canvas.style.height=r.height+"px";
  G.ctx.setTransform(dpr,0,0,dpr,0,0);
  G.W=r.width; G.H=r.height;
}
function buildGraphData(){
  G.nodes = []; G.edges=[]; G.byId.clear();
  const cx = (G.W||300)/2, cy=(G.H||200)/2;
  const links = new Map(); // also include broken wikilinks as ghost nodes
  for(const n of STATE.notes.values()){
    G.byId.set(n.slug, null);
  }
  for(const n of STATE.notes.values()){
    const node = { id:n.slug, title:n.title, real:true, x:cx+(Math.random()-0.5)*200, y:cy+(Math.random()-0.5)*200, vx:0, vy:0, deg:n.inLinks.size+n.outLinks.size };
    G.nodes.push(node); G.byId.set(n.slug,node);
  }
  // collect broken wikilink targets and create ghost nodes
  const ghostSeen = new Set();
  for(const n of STATE.notes.values()){
    for(const m of n.body.matchAll(/\[\[([^\]|]+)(?:\|[^\]]+)?\]\]/g)){
      const target = m[1].trim(); const slug = slugify(target);
      if(STATE.notes.has(slug) || STATE.byTitle.has(target.toLowerCase())) continue;
      if(ghostSeen.has(slug)) continue;
      ghostSeen.add(slug);
      const node = { id:"ghost::"+slug, title:target, real:false, x:cx+(Math.random()-0.5)*300, y:cy+(Math.random()-0.5)*300, vx:0, vy:0, deg:1 };
      G.nodes.push(node); G.byId.set("ghost::"+slug, node);
    }
  }
  const seen = new Set();
  for(const n of STATE.notes.values()){
    for(const o of n.outLinks){
      const key = n.slug<o ? n.slug+"|"+o : o+"|"+n.slug;
      if(seen.has(key)) continue; seen.add(key);
      const a=G.byId.get(n.slug), b=G.byId.get(o);
      if(a&&b) G.edges.push({a,b,real:true});
    }
    for(const m of n.body.matchAll(/\[\[([^\]|]+)(?:\|[^\]]+)?\]\]/g)){
      const target = m[1].trim(); const slug = slugify(target);
      if(STATE.notes.has(slug) || STATE.byTitle.has(target.toLowerCase())) continue;
      const a=G.byId.get(n.slug), b=G.byId.get("ghost::"+slug);
      if(a&&b) G.edges.push({a,b,real:false});
    }
  }
}
function tick(){
  const N=G.nodes; const E=G.edges; const k=0.04, rep=900, damp=0.82;
  for(let i=0;i<N.length;i++){
    const a=N[i];
    for(let j=i+1;j<N.length;j++){
      const b=N[j]; let dx=a.x-b.x, dy=a.y-b.y; let d2=dx*dx+dy*dy+0.01;
      const f = rep/d2; const d = Math.sqrt(d2);
      a.vx += f*dx/d; a.vy += f*dy/d; b.vx -= f*dx/d; b.vy -= f*dy/d;
    }
  }
  for(const {a,b} of E){
    const dx=b.x-a.x, dy=b.y-a.y; const d=Math.sqrt(dx*dx+dy*dy)+0.01;
    const target=80; const f=k*(d-target);
    a.vx += f*dx/d; a.vy += f*dy/d; b.vx -= f*dx/d; b.vy -= f*dy/d;
  }
  const cx=G.W/2, cy=G.H/2;
  for(const n of N){
    n.vx += (cx-n.x)*0.002; n.vy += (cy-n.y)*0.002;
    if(G.drag && G.drag.node===n){ n.x=G.mouse.x; n.y=G.mouse.y; n.vx=0; n.vy=0; continue; }
    n.vx*=damp; n.vy*=damp; n.x+=n.vx; n.y+=n.vy;
  }
  drawGraph(); G.raf = requestAnimationFrame(tick);
}
function drawGraph(){
  if(!G.ctx) return; const ctx=G.ctx; ctx.clearRect(0,0,G.W,G.H);
  ctx.lineWidth=1;
  for(const {a,b,real} of G.edges){
    ctx.strokeStyle = real ? "rgba(160,160,180,0.22)" : "rgba(224,122,122,0.22)";
    ctx.beginPath(); ctx.moveTo(a.x,a.y); ctx.lineTo(b.x,b.y); ctx.stroke();
  }
  const isGraphMode = document.body.classList.contains("mode-graph");
  for(const n of G.nodes){
    const isCur = n.id===STATE.current; const isHov = G.hover===n;
    const r = Math.min(11, 3 + Math.sqrt(n.deg)*1.6);
    ctx.beginPath(); ctx.arc(n.x,n.y,r,0,Math.PI*2);
    if(!n.real){
      ctx.fillStyle="rgba(224,122,122,0.10)"; ctx.fill();
      ctx.strokeStyle = isHov ? "#e07a7a" : "rgba(224,122,122,0.7)";
      ctx.setLineDash([3,2]); ctx.lineWidth=1.3; ctx.stroke(); ctx.setLineDash([]);
    } else {
      ctx.fillStyle = isCur ? "#c4b5fd" : (isHov ? "#a78bfa" : "#7f9cf5");
      ctx.fill();
    }
    if(isCur||isHov||(isGraphMode && n.real)){
      ctx.fillStyle = n.real ? "#dcdcdc" : "#e07a7a";
      ctx.font = (isCur?"bold ":"") + "11px -apple-system,Segoe UI,Roboto,sans-serif";
      ctx.fillText(n.title, n.x+r+4, n.y+3);
    }
  }
}
function nodeAt(x,y){
  for(const n of G.nodes){
    const r = Math.min(11, 3 + Math.sqrt(n.deg)*1.6) + 3;
    if((x-n.x)**2 + (y-n.y)**2 < r*r) return n;
  }
  return null;
}
function onGraphMove(e){
  const rect = G.canvas.getBoundingClientRect();
  G.mouse = { x:e.clientX-rect.left, y:e.clientY-rect.top };
  G.hover = nodeAt(G.mouse.x, G.mouse.y);
  G.canvas.style.cursor = G.hover ? "pointer" : (G.drag?"grabbing":"grab");
}
function onGraphDown(e){
  const rect = G.canvas.getBoundingClientRect();
  const m = { x:e.clientX-rect.left, y:e.clientY-rect.top };
  const n = nodeAt(m.x,m.y);
  if(n){ G.drag = { node:n, t:performance.now() }; G.mouse=m; }
}
function onGraphUp(){ G.drag=null; }
function onGraphClick(e){
  const rect = G.canvas.getBoundingClientRect();
  const n = nodeAt(e.clientX-rect.left, e.clientY-rect.top);
  if(!n) return;
  if(!n.real){
    alert(`"${n.title}" doesn't have a note yet.\nAsk Claude: "create a note for ${n.title}"`);
    return;
  }
  openNote(n.id);
  setMode("note");
}

/* ---------- view modes ---------- */
function setMode(mode){
  document.body.classList.remove("mode-graph","mode-note");
  document.body.classList.add("mode-"+mode);
  const slot = mode==="graph" ? document.getElementById("centerGraphSlot") : document.getElementById("rightGraphSlot");
  const canvas = document.getElementById("graph");
  if(canvas && canvas.parentNode !== slot) slot.appendChild(canvas);
  document.getElementById("modeGraph").classList.toggle("on", mode==="graph");
  document.getElementById("modeNote").classList.toggle("on", mode==="note");
  setTimeout(()=>{ resizeGraph(); drawGraph(); }, 0);
}

/* ---------- events ---------- */
document.getElementById("modeGraph").addEventListener("click", ()=>setMode("graph"));
document.getElementById("modeNote").addEventListener("click", ()=>{
  if(!STATE.notes.size){ setMode("note"); return; }
  if(!STATE.current){
    const def = STATE.notes.get("index") || STATE.notes.values().next().value;
    if(def) openNote(def.slug);
  }
  setMode("note");
});
document.getElementById("search").addEventListener("input", e=>{ STATE.search = e.target.value; renderList(); });
document.getElementById("nlist").addEventListener("click", e=>{ const it = e.target.closest(".item"); if(it){ openNote(it.dataset.slug); setMode("note"); } });
document.getElementById("tagFilter").addEventListener("click", e=>{
  const t = e.target.closest(".tag"); if(!t) return; const tag = t.dataset.tag;
  if(STATE.tagFilter.has(tag)) STATE.tagFilter.delete(tag); else STATE.tagFilter.add(tag);
  rebuildTagFilter(); renderList();
});
document.getElementById("doc").addEventListener("click", e=>{
  const wl = e.target.closest(".wl");
  if(wl){
    const slug = wl.dataset.slug;
    if(STATE.notes.has(slug)) openNote(slug);
    else { const t = wl.dataset.title; const byT = STATE.byTitle.get((t||"").toLowerCase());
      if(byT) openNote(byT.slug); else alert(`Note "${t}" doesn't exist yet.\nAsk Claude: "create a note for ${t}"`); }
    return;
  }
  const tg = e.target.closest(".tag");
  if(tg && tg.dataset.tag){
    if(STATE.tagFilter.has(tg.dataset.tag)) STATE.tagFilter.delete(tg.dataset.tag); else STATE.tagFilter.add(tg.dataset.tag);
    rebuildTagFilter(); renderList();
  }
});
document.getElementById("backlinks").addEventListener("click", e=>{ const it = e.target.closest(".bl"); if(it) openNote(it.dataset.slug); });

/* ---------- state persistence (survives auto-reload) ---------- */
const LS_KEY = "llmwiki:v1";
function saveSession(){
  try{
    const mode = document.body.classList.contains("mode-note") ? "note" : "graph";
    localStorage.setItem(LS_KEY, JSON.stringify({ slug:STATE.current, mode }));
  }catch(e){}
}
function loadSession(){
  try{ return JSON.parse(localStorage.getItem(LS_KEY) || "null"); }catch(e){ return null; }
}

/* ---------- auto-reload: poll notes-bundle.js for stamp changes ---------- */
const INITIAL_STAMP = window.WIKI_NOTES_STAMP || "";
function pollBundle(){
  // Re-inject the bundle script with a cache-busting query string. Works on file:// where fetch() is blocked.
  const s = document.createElement("script");
  s.src = "notes-bundle.js?_=" + Date.now();
  s.onload = () => {
    const stamp = window.WIKI_NOTES_STAMP || "";
    if (stamp && stamp !== INITIAL_STAMP){
      saveSession();
      location.reload();
    }
    s.remove();
  };
  s.onerror = () => s.remove();
  document.head.appendChild(s);
}
setInterval(pollBundle, 3000);

/* ---------- boot ---------- */
window.addEventListener("DOMContentLoaded", () => {
  if (Array.isArray(window.WIKI_NOTES) && window.WIKI_NOTES.length){
    const sess = loadSession();
    setMode(sess && sess.mode === "note" ? "note" : "graph");
    ingestBundled(window.WIKI_NOTES);
    if (sess && sess.slug && STATE.notes.has(sess.slug)){
      STATE.current = sess.slug;
      if (sess.mode === "note") openNote(sess.slug);
      else { renderList(); renderBacklinks(STATE.notes.get(sess.slug)); drawGraph(); }
    }
  } else {
    setMode("note");
  }
});

// Persist session when the user changes view or note.
document.addEventListener("click", () => setTimeout(saveSession, 0));
</script>
</body>
</html>
````

### 7.4 `notes-bundle.js` regeneration procedure

Whenever notes change, regenerate this file. It sets two globals:

- `window.WIKI_NOTES_STAMP` — seconds-since-epoch as a string. The viewer polls this to detect changes and triggers an auto-reload when it differs from the stamp at page-load time. **Must change on every regeneration**, otherwise auto-reload doesn't fire.
- `window.WIKI_NOTES` — JSON array of every note's filename and raw text.

**Format:**

```js
// Auto-generated by Claude. See AGENT.md §5.7 (regenerate after editing notes/).
// Loaded by wiki.html via <script src="notes-bundle.js"> so the wiki opens without picking a folder.
window.WIKI_NOTES_STAMP = "1778405208";
window.WIKI_NOTES = [
  {"name": "index.md",      "text": "---\ntitle: Index\n...full file contents..."},
  {"name": "karpathy.md",   "text": "..."},
  {"name": "transformer.md","text": "..."}
];
```

**One-shot regeneration via bash** (preferred — robust across all note contents):

```bash
cd "<wiki folder>"
python3 - <<'PY'
import json, os, time
records = []
for fn in sorted(os.listdir("notes")):
    if not fn.endswith(".md"): continue
    with open(os.path.join("notes", fn)) as f:
        records.append({"name": fn, "text": f.read()})
stamp = str(int(time.time()))
with open("notes-bundle.js","w") as f:
    f.write("// Auto-generated by Claude. See AGENT.md §5.7.\n")
    f.write(f"window.WIKI_NOTES_STAMP = {json.dumps(stamp)};\n")
    f.write("window.WIKI_NOTES = ")
    f.write(json.dumps(records, indent=2, ensure_ascii=False))
    f.write(";\n")
PY
```

If bash isn't available, write `notes-bundle.js` directly with the Write tool — set the stamp to the current Unix timestamp, then `JSON.stringify` (or hand-escape) each note's text into the array. **Always update the stamp**, even if nothing else changed (otherwise the viewer can't detect the change). Do not skip this step; the user's wiki goes stale instantly without it.

---

## 8. Pitfalls to avoid

- **No subfolders inside `notes/`.** The viewer expects a flat layout.
- **No relative `.md` links.** Use `[[wikilinks]]` for internal references; markdown links are for external URLs.
- **No images in source** unless the user provides them. Don't generate or fetch decorative images.
- **Don't bake content into `wiki.html`.** Notes always live as separate `.md` files; the viewer reads them via `notes-bundle.js`.
- **Don't ask before integrating a source.** If the user pastes a link, just process it and report back.
- **Don't forget §5.7.** Every note edit ends with regenerating `notes-bundle.js`.
- **Do ask** before deleting or renaming notes — those have ripple effects on backlinks.