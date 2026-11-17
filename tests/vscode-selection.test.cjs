'use strict';
const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readSelection, signature, copyTerminalSelection } = require('../src/YiKe.VSCode/selection');
const now = 100000;
function fixture(text = 'Selected text', ranges) {
  let reads = 0;
  ranges ||= [{ start: { line: 0, character: 0 }, end: { line: 0, character: text.length }, isEmpty: false }];
  const editor = { selections: ranges, document: { version: 1, offsetAt: p => p.character,
    getText: range => { assert.ok(range); reads++; return text.slice(range.start.character, range.end.character); } } };
  const vscode = { window: { state: { focused: true }, activeTextEditor: editor } };
  const state = { editor, version: 1, signature: signature(editor), changedAt: now - 300 };
  const request = { version: 1, nonce: 'a'.repeat(32), started: now - 500, released: now - 250 };
  return { vscode, state, request, editor, reads: () => reads, read: () => readSelection(vscode, state, request, now) };
}
test('matching mouse gesture reads only the selected range', () => {
  const f = fixture('before selected after', [{ start: { line: 0, character: 7 }, end: { line: 0, character: 15 }, isEmpty: false }]);
  assert.deepEqual(f.read(), { version: 1, nonce: f.request.nonce, status: 'Success', text: 'selected' }); assert.equal(f.reads(), 1);
});
test('unfocused window, another editor, changed version and changed selection never read text', () => {
  for (const change of [f => f.vscode.window.state.focused = false, f => f.vscode.window.activeTextEditor = {},
    f => f.editor.document.version++, f => f.editor.selections[0].end.character--]) {
    const f = fixture(); change(f); assert.equal(f.read().status, 'Unsupported'); assert.equal(f.reads(), 0);
  }
});
test('missing mouse event, malformed nonce and expired request never read text', () => {
  const f = fixture(); assert.equal(readSelection(f.vscode, undefined, f.request, now).status, 'Unsupported');
  for (const patch of [{ nonce: 42 }, { nonce: '' }, { version: 2 }, { released: now - 4000 }, { started: now + 1000 }, { released: now + 1000 }, { started: NaN }]) {
    assert.equal(readSelection(f.vscode, f.state, { ...f.request, ...patch }, now).status, 'Unsupported');
  }
  assert.equal(f.reads(), 0);
});
test('old mouse selection or an event after the request cannot be reused', () => {
  for (const changedAt of [now - 1000, now - 501, now + 1000]) { const f = fixture(); f.state.changedAt = changedAt; assert.equal(f.read().status, 'Unsupported'); assert.equal(f.reads(), 0); }
});
test('a long held drag remains valid when its last selection event is inside the press/release interval', () => {
  const f = fixture(); f.request.started = now - 10000; f.state.changedAt = now - 5000; assert.equal(f.read().status, 'Success');
});
test('empty and whitespace ranges do not become a translation', () => {
  for (const text of ['', ' \r\n\t']) { const f = fixture(text); assert.equal(f.read().status, 'NoSelection'); }
});
test('2000 Unicode scalars succeed; 2001 fail without returning text', () => {
  for (const [text, status] of [['😀'.repeat(2000), 'Success'], ['😀'.repeat(2001), 'TooLong']]) {
    const f = fixture(text); const r = f.read(); assert.equal(r.status, status); if (status === 'TooLong') assert.equal(r.text, undefined);
  }
});
test('oversized selection is rejected before asking the editor for text', () => {
  const f = fixture('x'.repeat(9000)); assert.equal(f.read().status, 'TooLong'); assert.equal(f.reads(), 0);
});
test('multiple selections join in editor order; excess range count is bounded', () => {
  const ranges = [0, 3].map(n => ({ start: { line: 0, character: n }, end: { line: 0, character: n + 2 }, isEmpty: false }));
  assert.equal(fixture('ab cd ef', ranges).read().text, 'ab\ncd');
  const f = fixture('ab', Array(33).fill(ranges[0])); assert.equal(f.read().status, 'Unsupported'); assert.equal(f.reads(), 0);
});

function activatedExtension() {
  const f = fixture(), events = {};
  const bind = name => listener => { events[name] = listener; return { dispose() {} }; };
  Object.assign(f.vscode.window, { onDidChangeTextEditorSelection: bind('selection'), onDidChangeActiveTextEditor: bind('editor'), onDidChangeWindowState: bind('focus') });
  f.vscode.workspace = { onDidChangeTextDocument: bind('document') };
  f.vscode.TextEditorSelectionChangeKind = { Mouse: 2 };
  let accept, closed = false;
  const server = { on() {}, listen() {}, close() { closed = true; } };
  const net = { createServer(callback) { accept = callback; return server; } };
  const module = { exports: {} };
  const source = require('node:fs').readFileSync(require.resolve('../src/YiKe.VSCode/extension.js'), 'utf8');
  require('node:vm').runInNewContext(source, { module, Buffer, process: { platform: 'win32', pid: 123 },
    require(name) { return name === 'vscode' ? f.vscode : name === 'node:net' ? net : require('../src/YiKe.VSCode/selection'); } });
  const context = { subscriptions: [] }; module.exports.activate(context);
  const mouse = () => events.selection({ kind: 2, textEditor: f.editor });
  function connect(chunks) {
    const handlers = {}, socket = { on(name, cb) { handlers[name] = cb; }, setTimeout() {}, destroy() { this.destroyed = true; }, end(text) { this.reply = JSON.parse(text); } };
    accept(socket); for (const chunk of chunks) handlers.data(Buffer.from(chunk)); return socket;
  }
  function query() { const time = Date.now(); return connect([JSON.stringify({ ...f.request, started: time - 100, released: time }) + '\n']); }
  return { f, events, mouse, query, connect, dispose() { context.subscriptions.forEach(s => s.dispose()); assert.ok(closed); } };
}
test('extension invalidates mouse metadata on keyboard, command, editor, focus and document changes', () => {
  const a = activatedExtension(); a.mouse(); assert.equal(a.query().reply.status, 'Success');
  for (const invalidate of [() => a.events.selection({ kind: 1 }), () => a.events.selection({ kind: 3 }), () => a.events.selection({}), a.events.editor, () => a.events.focus({ focused: false }), a.events.document]) {
    a.mouse(); const reads = a.f.reads(); invalidate(); assert.equal(a.query().reply.status, 'Unsupported'); assert.equal(a.f.reads(), reads);
  }
  a.dispose();
});
test('focus gain keeps a current gesture while focus loss invalidates the previous gesture', () => {
  const a = activatedExtension(); a.mouse(); a.events.focus({ focused: true }); assert.equal(a.query().reply.status, 'Success');
  a.events.focus({ focused: false }); assert.equal(a.query().reply.status, 'Unsupported'); a.dispose();
});
test('excess cursor count is rejected before capturing selection signatures', () => {
  const a = activatedExtension(); a.f.editor.selections = Array(100000); a.mouse();
  assert.equal(a.query().reply.status, 'Unsupported'); assert.equal(a.f.reads(), 0); a.dispose();
});
test('local pipe request handles fragmented JSON and rejects malformed or oversized frames', () => {
  const a = activatedExtension(); a.mouse(); const time = Date.now();
  const request = JSON.stringify({ ...a.f.request, started: time - 100, released: time }) + '\n';
  assert.equal(a.connect([request.slice(0, 10), request.slice(10)]).reply.status, 'Success');
  for (const chunks of [['{broken}\n'], ['x'.repeat(1025)]]) { const before = a.f.reads(); assert.ok(a.connect(chunks).destroyed); assert.equal(a.f.reads(), before); }
  a.dispose();
});
test('terminal copy is explicit, uses only the native copy-selection command and never reads editor or clipboard', async () => {
  const f = fixture(); let calls = 0;
  f.vscode.window.activeTerminal = {};
  f.vscode.commands = { async executeCommand(command) { assert.equal(command, 'workbench.action.terminal.copySelection'); calls++; } };
  const result = await copyTerminalSelection(f.vscode, { ...f.request, operation: 'terminalCopy' }, now);
  assert.equal(result.status, 'CopyRequested'); assert.equal(calls, 1); assert.equal(f.reads(), 0); assert.equal(result.text, undefined);
  assert.equal(f.read().status, 'Success');
});
test('terminal copy rejects stale requests, unknown operations, missing terminals and unfocused windows before any command', async () => {
  for (const patch of [f => f.vscode.window.state.focused = false, f => f.vscode.window.activeTerminal = undefined,
    f => f.request.nonce = '', f => f.request.released = now - 4000, f => f.request.operation = 'editor']) {
    const f = fixture(); f.vscode.window.activeTerminal = {}; f.request.operation = 'terminalCopy';
    f.vscode.commands = { executeCommand() { throw new Error('Must not copy'); } }; patch(f);
    assert.equal((await copyTerminalSelection(f.vscode, f.request, now)).status, 'Unsupported'); assert.equal(f.reads(), 0);
  }
});
test('editor reads cannot be used as a terminal-copy acknowledgement', () => {
  const f = fixture(); f.request.operation = 'terminalCopy'; assert.equal(f.read().status, 'Unsupported'); assert.equal(f.reads(), 0);
});
test('changing the active terminal during copying invalidates the acknowledgement', async () => {
  const f = fixture(); f.vscode.window.activeTerminal = {};
  f.vscode.commands = { async executeCommand() { f.vscode.window.activeTerminal = {}; } };
  assert.equal((await copyTerminalSelection(f.vscode, { ...f.request, operation: 'terminalCopy' }, now)).status, 'Unsupported');
});
