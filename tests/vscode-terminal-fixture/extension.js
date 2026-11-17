'use strict';
// Own synthetic pseudo-terminal: no shell process, user files or command execution.
const vscode = require('vscode');
const fs = require('node:fs');
const path = require('node:path');
function activate(context) {
  const folder = process.env.YIKE_TERMINAL_FIXTURE;
  if (!folder || path.basename(folder) !== 'yike-terminal-fixture') return;
  const write = new vscode.EventEmitter();
  const terminal = vscode.window.createTerminal({ name: 'YiKe synthetic terminal', pty: {
    onDidWrite: write.event,
    open() { write.fire("grep -n -i 'error' ./demo/app.log\r\n"); }, close() {}
  } });
  const report = stage => fs.writeFileSync(path.join(folder, 'stage.json'), JSON.stringify({ stage, codeVersion: vscode.version }));
  terminal.show();
  let phase = 'starting', busy = false;
  async function selectFirst() {
    if (phase !== 'starting' || !vscode.window.state.focused) return;
    phase = 'selecting'; terminal.show();
    try { await new Promise(resolve => setTimeout(resolve, 500)); await vscode.commands.executeCommand('workbench.action.terminal.selectAll'); phase = 'selected'; report(phase); }
    catch (error) { report('error:' + error.message); }
  }
  const first = setTimeout(selectFirst, 2200);
  context.subscriptions.push(vscode.window.onDidChangeWindowState(event => { if (event.focused) void selectFirst(); }));
  const timer = setInterval(async () => {
    if (busy) return; busy = true;
    try {
      const signal = path.join(folder, 'signal.txt');
      const action = fs.existsSync(signal) ? fs.readFileSync(signal, 'utf8').trim() : '';
      if (phase === 'selected' && action === 'clear') {
        await vscode.commands.executeCommand('workbench.action.terminal.clearSelection'); phase = 'cleared'; report(phase);
      } else if (phase === 'cleared' && action === 'oversize') {
        write.fire('\x1b[2J\x1b[H' + 'x'.repeat(2100) + '\r\n');
        await new Promise(resolve => setTimeout(resolve, 250));
        await vscode.commands.executeCommand('workbench.action.terminal.selectAll'); phase = 'oversize'; report(phase);
      }
    } catch (error) { report('error:' + error.message); }
    finally { busy = false; }
  }, 100);
  context.subscriptions.push(terminal, write, { dispose() { clearTimeout(first); clearInterval(timer); } });
}
module.exports = { activate };
