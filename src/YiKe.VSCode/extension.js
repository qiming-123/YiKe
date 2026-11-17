'use strict';
const vscode = require('vscode');
const net = require('node:net');
const { signature, readSelection, copyTerminalSelection } = require('./selection');

function activate(context) {
  if (process.platform !== 'win32') return;
  let state;
  const clear = () => { state = undefined; };
  context.subscriptions.push(
    vscode.window.onDidChangeTextEditorSelection(event => {
      state = event.kind === vscode.TextEditorSelectionChangeKind.Mouse && event.textEditor.selections.length <= 32 ? {
        editor: event.textEditor, version: event.textEditor.document.version,
        signature: signature(event.textEditor), changedAt: Date.now()
      } : undefined;
    }),
    vscode.window.onDidChangeActiveTextEditor(clear),
    vscode.window.onDidChangeWindowState(event => { if (!event.focused) clear(); }),
    vscode.workspace.onDidChangeTextDocument(clear)
  );
  const sockets = new Set();
  const server = net.createServer(socket => {
    sockets.add(socket);
    socket.on('close', () => sockets.delete(socket));
    socket.on('error', () => {});
    socket.setTimeout(500, () => socket.destroy());
    let input = Buffer.alloc(0), answered = false;
    socket.on('data', async chunk => {
      if (answered) return;
      if (input.length + chunk.length > 1024) { socket.destroy(); return; }
      input = Buffer.concat([input, chunk]);
      const end = input.indexOf(10);
      if (end < 0) return;
      answered = true;
      try {
        const request = JSON.parse(input.subarray(0, end).toString('utf8'));
        const response = request?.operation === 'terminalCopy'
          ? await copyTerminalSelection(vscode, request) : readSelection(vscode, state, request);
        socket.end(JSON.stringify(response) + '\n');
      } catch { socket.destroy(); }
    });
  });
  server.maxConnections = 8;
  // A separate endpoint for each local extension host avoids selecting another Code window.
  server.on('error', () => {});
  server.listen({ path: `\\\\.\\pipe\\YiKe.VSCode.Selection.v1.${process.pid}`, readableAll: false, writableAll: false });
  context.subscriptions.push({ dispose() { clear(); for (const socket of sockets) socket.destroy(); server.close(); } });
}
module.exports = { activate };
