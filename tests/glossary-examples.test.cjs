const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

// Execute only these trusted, bundled JavaScript snippets, never user selections or command examples.
const source = fs.readFileSync(path.join(__dirname, '../src/YiKe/Translation/GlossaryExamples.cs'), 'utf8');
function example(key) {
  const line = source.split('\n').find(line => line.trimStart().startsWith(`["${key}"] = new(`));
  assert.ok(line, `Missing example ${key}`);
  const match = line.match(/= new\(("(?:\\.|[^"\\])*"),/);
  assert.ok(match);
  return JSON.parse(match[1]);
}
function run(code) { return vm.runInNewContext(code, {}, { timeout: 1000 }); }

test('Bundled const example changes object properties while its binding stays fixed', () => {
  const code = example('const').replace(/^JavaScript：/, '');
  assert.equal(run(code + '; user.name'), 'Wang');
  assert.throws(() => run(code + '; user = {}'), error => error.name === 'TypeError');
});
test('Bundled let example can update the declared number', () => {
  const code = example('let').split('\n')[0].replace(/^JavaScript：/, '');
  assert.equal(run(code + '; count'), 2);
});
test('Bundled var example is not confined to its if block', () => {
  const code = example('var').split('\n').find(line => line.startsWith('JavaScript：')).replace(/^JavaScript：/, '');
  assert.equal(run(code + '; count'), 3);
});
test('Bundled closure example retains its counter between calls', () => {
  const code = example('closure').replace(/^JavaScript：/, '');
  assert.deepEqual(Array.from(run(code + '; const next = makeCounter(); [next(), next()]')), [1, 2]);
});
test('Bundled async example returns a promise resolving to its value', async () => {
  const code = example('async').replace(/^JavaScript：/, '');
  const result = run(code + '; load()');
  assert.equal(typeof result.then, 'function');
  assert.equal(await result, 42);
});
test('Bundled array example grows the existing array', () => {
  const code = example('array').replace(/^JavaScript：/, '');
  assert.equal(run(code + "; values.join(',')"), '10,20,30');
});
