'use strict';

// Metadata only until a matching, local mouse-gesture request arrives.
function signature(editor) {
  return editor.selections.map(s => `${s.start.line}:${s.start.character}-${s.end.line}:${s.end.character}`).join(';');
}

function validRequest(request, now = Date.now()) {
  return request && request.version === 1 && /^[a-f0-9]{32}$/.test(request.nonce || '') &&
      Number.isSafeInteger(request.started) && Number.isSafeInteger(request.released) &&
      request.started <= request.released && request.released <= now + 200 && now - request.released <= 3000;
}
function readSelection(vscode, state, request, now = Date.now()) {
  const reply = status => ({ version: 1, nonce: request?.nonce, status });
  if (!validRequest(request, now) || (request.operation && request.operation !== 'editor')) return reply('Unsupported');
  const editor = vscode.window.activeTextEditor;
  if (!vscode.window.state.focused || !editor || !state || state.editor !== editor || editor.selections.length > 32 ||
      state.version !== editor.document.version || state.signature !== signature(editor) ||
      state.changedAt < request.started || state.changedAt > request.released + 200) return reply('Unsupported');
  const parts = [];
  let units = 0;
  for (const range of editor.selections) {
    if (range.isEmpty) continue;
    // offsetAt reads positions, not the full document. Bound before getText(range).
    units += editor.document.offsetAt(range.end) - editor.document.offsetAt(range.start) + (parts.length ? 1 : 0);
    if (units > 8002) return reply('TooLong');
    const text = editor.document.getText(range);
    if (text.trim()) parts.push(text);
  }
  const text = parts.join('\n');
  if (!text.trim()) return reply('NoSelection');
  if ([...text].length > 2000) return reply('TooLong');
  return { ...reply('Success'), text };
}
// Explicit desktop hotkey only. This command copies the current terminal selection;
// the desktop verifies a fresh clipboard sequence and Code ownership before reading.
// No clipboard reads, terminal input, buffer/history reads or editor fallback here.
async function copyTerminalSelection(vscode, request, now = Date.now()) {
  const reply = status => ({ version: 1, nonce: request?.nonce, status });
  if (!validRequest(request, now) || request.operation !== 'terminalCopy' ||
      !vscode.window.state.focused || !vscode.window.activeTerminal) return reply('Unsupported');
  const terminal = vscode.window.activeTerminal;
  await vscode.commands.executeCommand('workbench.action.terminal.copySelection');
  if (!vscode.window.state.focused || vscode.window.activeTerminal !== terminal) return reply('Unsupported');
  return reply('CopyRequested');
}
module.exports = { signature, readSelection, copyTerminalSelection };
