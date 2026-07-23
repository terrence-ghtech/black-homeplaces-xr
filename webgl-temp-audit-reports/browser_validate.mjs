#!/usr/bin/env node
// Dependency-free CDP harness: drives headless Chrome against the local
// Unity WebGL server, records network responses, console output, exceptions,
// and Unity startup progress. Usage:
//   node browser_validate.mjs <pageUrl> <outJsonPath> [timeoutSec]
// Chrome must already be running with --remote-debugging-port=9222.

const [pageUrl, outPath, timeoutSecArg] = process.argv.slice(2);
const timeoutMs = (parseInt(timeoutSecArg || "600", 10)) * 1000;
const fs = await import("node:fs");

async function getWsUrl() {
  for (let i = 0; i < 30; i++) {
    try {
      const res = await fetch("http://127.0.0.1:9222/json/new?about:blank", { method: "PUT" });
      if (res.ok) return (await res.json()).webSocketDebuggerUrl;
    } catch {}
    await new Promise(r => setTimeout(r, 500));
  }
  throw new Error("cannot reach Chrome debugging port");
}

const ws = new WebSocket(await getWsUrl());
let msgId = 0;
const pending = new Map();
function send(method, params = {}) {
  return new Promise((resolve, reject) => {
    const id = ++msgId;
    pending.set(id, { resolve, reject });
    ws.send(JSON.stringify({ id, method, params }));
  });
}

const result = {
  pageUrl, startedAt: new Date().toISOString(),
  responses: [], failures: [], console: [], exceptions: [],
  unity: { loadingBarHidden: false, canvasPresent: false, secondsToLoad: null },
  outcome: "timeout",
};

ws.onmessage = (ev) => {
  const msg = JSON.parse(ev.data);
  if (msg.id && pending.has(msg.id)) {
    const { resolve, reject } = pending.get(msg.id);
    pending.delete(msg.id);
    msg.error ? reject(new Error(JSON.stringify(msg.error))) : resolve(msg.result);
    return;
  }
  const p = msg.params;
  switch (msg.method) {
    case "Network.responseReceived":
      result.responses.push({
        url: p.response.url, status: p.response.status,
        mimeType: p.response.mimeType,
        contentEncoding: p.response.headers["Content-Encoding"] || p.response.headers["content-encoding"] || null,
      });
      break;
    case "Network.loadingFailed":
      result.failures.push({ requestId: p.requestId, errorText: p.errorText, canceled: p.canceled });
      break;
    case "Runtime.consoleAPICalled":
      if (result.console.length < 400)
        result.console.push({
          type: p.type,
          text: p.args.map(a => a.value !== undefined ? String(a.value) : (a.description || a.type)).join(" ").slice(0, 500),
        });
      break;
    case "Runtime.exceptionThrown":
      result.exceptions.push({
        text: (p.exceptionDetails.exception?.description || p.exceptionDetails.text || "").slice(0, 1000),
      });
      break;
    case "Log.entryAdded":
      if (result.console.length < 400)
        result.console.push({ type: "log." + p.entry.level, text: (p.entry.text || "").slice(0, 500) });
      break;
  }
};

await new Promise((res, rej) => { ws.onopen = res; ws.onerror = rej; });
await send("Network.enable");
await send("Runtime.enable");
await send("Log.enable");
await send("Page.enable");

const t0 = Date.now();
await send("Page.navigate", { url: pageUrl });

async function evalJs(expr) {
  try {
    const r = await send("Runtime.evaluate", { expression: expr, returnByValue: true });
    return r.result?.value;
  } catch { return undefined; }
}

while (Date.now() - t0 < timeoutMs) {
  await new Promise(r => setTimeout(r, 5000));
  const state = await evalJs(`(() => {
    const bar = document.querySelector('#unity-loading-bar');
    const canvas = document.querySelector('#unity-canvas');
    return {
      barHidden: bar ? getComputedStyle(bar).display === 'none' : null,
      canvas: !!canvas,
      progressWidth: document.querySelector('#unity-progress-bar-full')?.style?.width || null,
    };
  })()`);
  if (state) {
    result.unity.canvasPresent = state.canvas;
    result.unity.progressWidth = state.progressWidth;
    if (state.barHidden === true) {
      result.unity.loadingBarHidden = true;
      result.unity.secondsToLoad = (Date.now() - t0) / 1000;
      result.outcome = "loaded";
      break;
    }
  }
  const fatal = result.exceptions.length > 0 ||
    result.console.some(c => /RuntimeError|abort|out of memory|failed to load|could not be loaded/i.test(c.text));
  if (fatal && Date.now() - t0 > 60000) { result.outcome = "error"; break; }
}

// settle time for post-load console/network activity
await new Promise(r => setTimeout(r, 10000));
result.finishedAt = new Date().toISOString();
result.elapsedSeconds = (Date.now() - t0) / 1000;
fs.writeFileSync(outPath, JSON.stringify(result, null, 2));
console.log("outcome=" + result.outcome + " loadSeconds=" + result.unity.secondsToLoad +
  " responses=" + result.responses.length + " failures=" + result.failures.length +
  " exceptions=" + result.exceptions.length);
process.exit(0);
