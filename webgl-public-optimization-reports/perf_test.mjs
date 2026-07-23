#!/usr/bin/env node
// Stage 8 performance harness. Drives headless Chrome via CDP against the
// local Brotli server. Measures load timing, transfer size, JS heap, and FPS.
// Usage: node perf_test.mjs <url> <outJson> [--throttle-mbps N] [--latency-ms N] [--label L]
// Chrome must run with --remote-debugging-port=9222. Each run opens a fresh tab.

const args = process.argv.slice(2);
const pageUrl = args[0];
const outPath = args[1];
function opt(name, dflt) {
  const i = args.indexOf(name);
  return i >= 0 ? args[i + 1] : dflt;
}
const throttleMbps = parseFloat(opt("--throttle-mbps", "0"));
const latencyMs = parseFloat(opt("--latency-ms", "0"));
const label = opt("--label", "run");
const timeoutMs = parseFloat(opt("--timeout-sec", "900")) * 1000;
const fs = await import("node:fs");

const res = await fetch("http://127.0.0.1:9222/json/new?about:blank", { method: "PUT" });
const target = await res.json();
const ws = new WebSocket(target.webSocketDebuggerUrl);
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
  label, pageUrl, throttleMbps, latencyMs, startedAt: new Date().toISOString(),
  timeToLoadingBarVisibleSec: null, timeToInteractiveSec: null,
  bytesTransferred: 0, requests: 0, failures: [], exceptions: [], notable: [],
  jsHeapUsedMB: null, jsHeapTotalMB: null, fps: null, frameTimeP95Ms: null,
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
  if (msg.method === "Network.loadingFinished") {
    result.bytesTransferred += p.encodedDataLength || 0;
    result.requests++;
  } else if (msg.method === "Network.loadingFailed" && !p.canceled) {
    result.failures.push(p.errorText);
  } else if (msg.method === "Runtime.exceptionThrown") {
    result.exceptions.push((p.exceptionDetails.exception?.description || p.exceptionDetails.text || "").slice(0, 400));
  } else if (msg.method === "Runtime.consoleAPICalled" && (p.type === "error" || p.type === "warning")) {
    if (result.notable.length < 50)
      result.notable.push(p.type + ": " + p.args.map(a => a.value ?? a.description ?? "").join(" ").slice(0, 300));
  }
};

await new Promise((r, j) => { ws.onopen = r; ws.onerror = j; });
await send("Network.enable");
await send("Runtime.enable");
await send("Page.enable");
await send("Performance.enable");
if (throttleMbps > 0) {
  await send("Network.emulateNetworkConditions", {
    offline: false,
    latency: latencyMs,
    downloadThroughput: throttleMbps * 125000,
    uploadThroughput: throttleMbps * 125000 / 4,
  });
}

async function evalJs(expr, awaitPromise = false) {
  try {
    const r = await send("Runtime.evaluate", { expression: expr, returnByValue: true, awaitPromise });
    return r.result?.value;
  } catch { return undefined; }
}

const t0 = Date.now();
await send("Page.navigate", { url: pageUrl });

let barSeen = false;
while (Date.now() - t0 < timeoutMs) {
  await new Promise(r => setTimeout(r, 1000));
  const state = await evalJs(`(() => {
    const bar = document.querySelector('#unity-loading-bar');
    return { hasBar: !!bar, barHidden: bar ? getComputedStyle(bar).display === 'none' : null };
  })()`);
  if (state?.hasBar && !barSeen && state.barHidden === false) {
    barSeen = true;
    result.timeToLoadingBarVisibleSec = (Date.now() - t0) / 1000;
  }
  if (state?.hasBar && state.barHidden === true) {
    result.timeToInteractiveSec = (Date.now() - t0) / 1000;
    result.outcome = "loaded";
    break;
  }
  if (result.exceptions.length > 3 && Date.now() - t0 > 90000) { result.outcome = "error"; break; }
}

if (result.outcome === "loaded") {
  // settle, then measure FPS over 10 s inside the page
  await new Promise(r => setTimeout(r, 8000));
  const fps = await evalJs(`new Promise(resolve => {
    const times = [];
    let last = performance.now();
    function tick(t) {
      times.push(t - last); last = t;
      if (times.length >= 600 || (times.length > 30 && t - start > 10000)) {
        const avg = 1000 / (times.reduce((a,b)=>a+b,0) / times.length);
        const sorted = [...times].sort((a,b)=>a-b);
        resolve({ fps: avg, p95: sorted[Math.floor(sorted.length*0.95)] });
        return;
      }
      requestAnimationFrame(tick);
    }
    const start = performance.now();
    requestAnimationFrame(tick);
  })`, true);
  if (fps) { result.fps = fps.fps; result.frameTimeP95Ms = fps.p95; }

  const metrics = await send("Performance.getMetrics");
  for (const m of metrics.metrics) {
    if (m.name === "JSHeapUsedSize") result.jsHeapUsedMB = m.value / 1e6;
    if (m.name === "JSHeapTotalSize") result.jsHeapTotalMB = m.value / 1e6;
  }
}

result.elapsedSec = (Date.now() - t0) / 1000;
result.finishedAt = new Date().toISOString();
fs.writeFileSync(outPath, JSON.stringify(result, null, 2));
console.log(`${label}: outcome=${result.outcome} interactive=${result.timeToInteractiveSec}s ` +
  `transferred=${(result.bytesTransferred/1e6).toFixed(1)}MB heapUsed=${result.jsHeapUsedMB?.toFixed(0)}MB ` +
  `fps=${result.fps?.toFixed(1)} failures=${result.failures.length} exceptions=${result.exceptions.length}`);
try { await send("Page.close"); } catch {}
process.exit(0);
