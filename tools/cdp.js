// Drives a browser that was started with --remote-debugging-port, for testing a lab page with
// real mouse events. Usage: node Tools/cdp.js <port> <steps.json>
// Steps: {"clickIn": [selector, fx, fy]} {"moveIn": [selector, fx, fy]} (fractions of the element's box)
// Steps: {"go": url} {"wait": ms} {"click": [x, y]} {"drag": [x0, y0, x1, y1]} {"move": [x, y]}
//        {"until": "js that turns true", "timeout": ms}
//        {"eval": "js"} {"shot": "file.png"} {"type": "text"} {"size": [w, h], "dpr": 2 (sharper pictures)}
//        {"reload": true} {"pdf": "file.pdf"} {"mobile": [w, h, scale]} {"tap": [x, y]}
//        {"media": "print"} (or "" to go back to the screen) {"fullshot": "whole-page.png"}
//        {"press": [x, y]} {"hold": [x, y]} (move with the button down) {"release": [x, y]},
//        for a drag that stops part way, to look at it
//        {"shot": "part.png", "clip": "#css-selector", "pad": 8} a picture of one part of the page only
//        {"init": "js"} a script that runs before each page's own scripts (for a stand-in, say)
//        {"pressJs": "js"} {"holdJs": "js"} {"moveJs": "js"} {"releaseJs": "js"}: as press, hold, move and
//        release, at the [x, y] the script gives (it may return a promise), worked out by the page at the time
const fs = require('fs');
const port = process.argv[2], steps = JSON.parse(fs.readFileSync(process.argv[3], 'utf8'));
const sleep = ms => new Promise(r => setTimeout(r, ms));
(async () => {
  const targets = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
  const page = targets.find(t => t.type === 'page');
  const ws = new WebSocket(page.webSocketDebuggerUrl);
  await new Promise(r => ws.addEventListener('open', r));
  let next = 1; const waiting = new Map();
  ws.addEventListener('message', e => {
    const m = JSON.parse(e.data);
    if (m.id && waiting.has(m.id)) { waiting.get(m.id)(m); waiting.delete(m.id); }
    if (m.method === 'Runtime.exceptionThrown') console.log('PAGE ERROR', JSON.stringify(m.params.exceptionDetails).slice(0, 400));
    if (m.method === 'Runtime.consoleAPICalled' && m.params.type === 'error') console.log('CONSOLE ERROR', m.params.args.map(a => a.value || a.description).join(' ').slice(0, 300));
  });
  const send = (method, params = {}) => new Promise(r => { const id = next++; waiting.set(id, r); ws.send(JSON.stringify({ id, method, params })); });
  await send('Runtime.enable'); await send('Page.enable');
  // always fetch the page's files afresh, so a test never runs against a cached copy
  await send('Network.enable'); await send('Network.setCacheDisabled', { cacheDisabled: true });
  let last = [0, 0];
  const mouse = (type, x, y, extra = {}) => { last = [x, y]; return send('Input.dispatchMouseEvent', Object.assign({ type, x, y, button: 'left', buttons: type === 'mouseReleased' ? 0 : 1, clickCount: 1, pointerType: 'mouse' }, extra)); };
  for (const s of steps) {
    if (s.go) { await send('Page.navigate', { url: s.go }); }
    else if (s.init) await send('Page.addScriptToEvaluateOnNewDocument', { source: s.init });
    else if (s.size) await send('Emulation.setDeviceMetricsOverride', { width: s.size[0], height: s.size[1], deviceScaleFactor: s.dpr || 1, mobile: false });
    else if (s.reload) await send('Page.reload', {});
    else if (s.media !== undefined) await send('Emulation.setEmulatedMedia', { media: s.media });
    else if (s.fullshot) {
      const m = await send('Page.getLayoutMetrics');
      const size = m.result.cssContentSize || m.result.contentSize;
      const r = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: true, clip: { x: 0, y: 0, width: size.width, height: size.height, scale: 1 } });
      fs.writeFileSync(s.fullshot, Buffer.from(r.result.data, 'base64')); console.log('SHOT', s.fullshot, Math.round(size.width) + 'x' + Math.round(size.height));
    }
    else if (s.mobile) {
      await send('Emulation.setDeviceMetricsOverride', { width: s.mobile[0], height: s.mobile[1], deviceScaleFactor: s.mobile[2] || 2, mobile: true });
      await send('Emulation.setTouchEmulationEnabled', { enabled: true, maxTouchPoints: 5 });
      // (Dive Game copy) a phone's user agent too, so pages and Unity treat the browser as a phone; set it before "go"
      await send('Emulation.setUserAgentOverride', { userAgent: s.ua ||
        'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36' });
    } else if (s.tap) {
      await send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: s.tap[0], y: s.tap[1] }] });
      await sleep(80);
      await send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
    } else if (s.pdf) {
      const r = await send('Page.printToPDF', { printBackground: true, preferCSSPageSize: true });
      if (!r.result) console.log('PDF FAILED', JSON.stringify(r.error || r));
      else { fs.writeFileSync(s.pdf, Buffer.from(r.result.data, 'base64')); console.log('PDF', s.pdf); }
    }
    else if (s.wait) await sleep(s.wait);
    else if (s.until) {
      const t0 = Date.now();
      for (;;) {
        const r = await send('Runtime.evaluate', { expression: s.until, returnByValue: true });
        if (r.result && r.result.result && r.result.result.value) break;
        if (Date.now() - t0 > (s.timeout || 120000)) { console.log('TIMEOUT', s.until); break; }
        await sleep(100);
      }
    }
    else if (s.pressJs || s.holdJs || s.moveJs || s.releaseJs) {
      const expr = s.pressJs || s.holdJs || s.moveJs || s.releaseJs;
      const r = await send('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise: true });
      const p = r.result && r.result.result && r.result.result.value;
      if (!Array.isArray(p)) { console.log('NO POINT', expr, JSON.stringify(r).slice(0, 300)); continue; }
      const [x, y] = p.map(Math.round);
      if (s.pressJs) { await mouse('mouseMoved', x, y, { button: 'none', buttons: 0 }); await sleep(120); await mouse('mousePressed', x, y); }
      else if (s.holdJs) {
        // in a few steps, as a hand would move
        const from = last.slice();
        for (let i = 1; i <= 8; i++) { await mouse('mouseMoved', Math.round(from[0] + (x - from[0]) * i / 8), Math.round(from[1] + (y - from[1]) * i / 8)); await sleep(40); }
      }
      else if (s.moveJs) await mouse('mouseMoved', x, y, { button: 'none', buttons: 0 });
      else await mouse('mouseReleased', x, y);
      console.log('AT', x, y);
    }
    else if (s.move) await mouse('mouseMoved', s.move[0], s.move[1], { button: 'none', buttons: 0 });
    else if (s.clickIn || s.moveIn) {
      // a point given as a fraction of an element's box ([selector, fx, fy]), measured now, so a page whose layout shifts still gets it right
      const [sel, fx, fy] = s.clickIn || s.moveIn;
      const r = await send('Runtime.evaluate', { expression: "(function(){var b=document.querySelector(" + JSON.stringify(sel) + ").getBoundingClientRect();return [b.left,b.top,b.width,b.height];})()", returnByValue: true });
      const [L, T, W, H] = r.result.result.value, x = Math.round(L + fx * W), y = Math.round(T + fy * H);
      await mouse('mouseMoved', x, y, { button: 'none', buttons: 0 }); await sleep(120);
      if (s.clickIn) { await mouse('mousePressed', x, y); await sleep(90); await mouse('mouseReleased', x, y); }
    }
    else if (s.click) {
      await mouse('mouseMoved', s.click[0], s.click[1], { button: 'none', buttons: 0 }); await sleep(120);
      await mouse('mousePressed', s.click[0], s.click[1]); await sleep(90); await mouse('mouseReleased', s.click[0], s.click[1]);
    } else if (s.drag) {
      const [x0, y0, x1, y1] = s.drag;
      await mouse('mouseMoved', x0, y0, { button: 'none', buttons: 0 }); await sleep(120);
      await mouse('mousePressed', x0, y0);
      for (let i = 1; i <= 12; i++) { await mouse('mouseMoved', x0 + (x1 - x0) * i / 12, y0 + (y1 - y0) * i / 12); await sleep(40); }
      await sleep(250); await mouse('mouseReleased', x1, y1);
    } else if (s.press) await mouse('mousePressed', s.press[0], s.press[1]);
    else if (s.hold) await mouse('mouseMoved', s.hold[0], s.hold[1]);     // a move with the button still down
    else if (s.release) await mouse('mouseReleased', s.release[0], s.release[1]);
    else if (s.type) await send('Input.insertText', { text: s.type });
    // real key presses, one character at a time (a page that swallows key events shows up here; "type" slips past it)
    // (Dive Game copy: each press carries its code and key number too, which Unity's Input System reads)
    else if (s.keys) {
      for (const ch of s.keys) {
        const up = ch.toUpperCase();
        const code = /[a-z]/i.test(ch) ? 'Key' + up : /[0-9]/.test(ch) ? 'Digit' + ch : ch === ' ' ? 'Space' : ch === '.' ? 'Period' : '';
        const vk = /[a-z0-9 ]/i.test(ch) ? up.charCodeAt(0) : ch === '.' ? 190 : 0;
        await send('Input.dispatchKeyEvent', { type: 'keyDown', key: ch, code, text: ch, unmodifiedText: ch, windowsVirtualKeyCode: vk, nativeVirtualKeyCode: vk });
        await send('Input.dispatchKeyEvent', { type: 'keyUp', key: ch, code, windowsVirtualKeyCode: vk, nativeVirtualKeyCode: vk });
        await sleep(40);
      }
    }
    // a named key: {"key": "Enter"}, "Tab", "Space", "Backspace", "Escape" ("shift": true for Shift+key)
    else if (s.key) {
      const names = { Enter: [13, 'Enter', '\r'], Tab: [9, 'Tab', ''], Space: [32, ' ', ' '], Backspace: [8, 'Backspace', ''], Escape: [27, 'Escape', ''] };
      const [vk, key, text] = names[s.key];
      const modifiers = s.shift ? 8 : 0;
      if (s.shift) await send('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Shift', code: 'ShiftLeft', windowsVirtualKeyCode: 16, modifiers });
      await send('Input.dispatchKeyEvent', { type: text ? 'keyDown' : 'rawKeyDown', key, code: s.key, text, windowsVirtualKeyCode: vk, nativeVirtualKeyCode: vk, modifiers });
      await sleep(60);
      await send('Input.dispatchKeyEvent', { type: 'keyUp', key, code: s.key, windowsVirtualKeyCode: vk, nativeVirtualKeyCode: vk, modifiers });
      if (s.shift) await send('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Shift', code: 'ShiftLeft', windowsVirtualKeyCode: 16 });
      await sleep(60);
    }
    else if (s.eval) {
      const r = await send('Runtime.evaluate', { expression: s.eval, returnByValue: true, awaitPromise: true });
      console.log('EVAL', String(JSON.stringify(r.result && r.result.result ? r.result.result.value : r)).slice(0, 1500));
    } else if (s.shot && s.clip) {
      // the element's place on the whole page, with a margin round it
      // scrolled into view first: capturing beyond the window would stretch the page for a moment,
      // which moves things sideways (the scrollbar goes) and upsets clicks worked out just before
      const q = JSON.stringify(s.clip), pad = s.pad || 0;
      const at = await send('Runtime.evaluate', { returnByValue: true, expression:
        `(function(){ var e = document.querySelector(${q}); if (!e) return null; var r = e.getBoundingClientRect();
          if (r.top < 0 || r.bottom > window.innerHeight) { e.scrollIntoView({ block: r.height < window.innerHeight ? 'center' : 'start' }); r = e.getBoundingClientRect(); }
          return { x: r.left + window.scrollX, y: r.top + window.scrollY, w: r.width, h: r.height, fits: r.top >= 0 && r.bottom <= window.innerHeight }; })()` });
      const b = at.result && at.result.result && at.result.result.value;
      if (!b) { console.log('NO ELEMENT', s.clip); continue; }
      await sleep(150);
      const r = await send('Page.captureScreenshot', { format: 'png', captureBeyondViewport: !b.fits, clip: { x: Math.max(0, b.x - pad), y: Math.max(0, b.y - pad), width: b.w + 2 * pad, height: b.h + 2 * pad, scale: s.scale || 1 } });
      fs.writeFileSync(s.shot, Buffer.from(r.result.data, 'base64')); console.log('SHOT', s.shot, Math.round(b.w) + 'x' + Math.round(b.h));
    } else if (s.shot) {
      const r = await send('Page.captureScreenshot', { format: 'png' });
      fs.writeFileSync(s.shot, Buffer.from(r.result.data, 'base64')); console.log('SHOT', s.shot);
    }
  }
  ws.close();
})().catch(e => { console.error(e); process.exit(1); });
