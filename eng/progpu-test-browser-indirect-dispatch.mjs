// Execute the actual browser packet decoder without starting .NET or a GPU.
// This is transport validation, not browser device/runtime qualification.
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import vm from 'node:vm';

const context = vm.createContext({
  TextDecoder,
  MessageChannel: class { port1 = {}; port2 = {}; },
  addEventListener() {}
});
vm.runInContext('globalThis.WorkerGlobalScope = class { static [Symbol.hasInstance]() { return true; } };', context);
const source = await readFile(new URL('../src/ProGPU.Browser/BrowserAssets/progpu-browser.js', import.meta.url), 'utf8');
const module = new vm.SourceTextModule(source + '\nexport { state, dispatchPacket };', { context });
await module.link(specifier => {
  const names = specifier.endsWith('/dotnet.js') ? ['dotnet'] :
    ['measurePhysicalCanvas', 'requestProGpuWebGpuDevice', 'updateProGpuVisualViewport'];
  return new vm.SyntheticModule(names, function () {
    for (const name of names) this.setExport(name, () => { throw new Error('Unexpected browser startup'); });
  }, { context });
});
await module.evaluate();
const { state, dispatchPacket } = module.namespace;
state.device = {};
const calls = [];
const buffer = {};
state.resources.set(17, buffer);
state.resources.set(23, { dispatchWorkgroupsIndirect(...args) { calls.push(args); } });
function packet(offset, payloadLength = 16, bufferHandle = 17) {
  const bytes = new Uint8Array(16 + ((8 + payloadLength + 7) & ~7));
  const view = new DataView(bytes.buffer);
  view.setUint32(0, 0x55504750, true);
  view.setUint16(4, 1, true);
  view.setUint32(8, bytes.length, true);
  view.setUint32(12, 1, true);
  view.setUint16(16, 54, true);
  view.setUint32(20, 8 + payloadLength, true);
  view.setUint32(24, 23, true);
  view.setUint32(28, bufferHandle, true);
  if (payloadLength >= 16) view.setBigUint64(32, offset, true);
  return bytes;
}
function dispatch(bytes) { dispatchPacket(bytes, 0, bytes.length); }
for (const offset of [0n, 16n, 4294967300n, 9007199254740988n]) {
  dispatch(packet(offset));
  const call = calls.pop();
  assert.equal(call[0], buffer);
  assert.equal(BigInt(call[1]), offset);
}
assert.throws(() => dispatch(packet(9007199254740992n)), /not exactly representable/);
assert.throws(() => dispatch(packet(18446744073709551615n)), /not exactly representable/);
assert.throws(() => dispatch(packet(0n, 8)), /Invalid indirect compute dispatch payload/);
assert.throws(() => dispatch(packet(0n, 24)), /Invalid indirect compute dispatch payload/);
assert.throws(() => dispatch(packet(0n, 16, 99)), /Stale or unknown WebGPU handle/);
assert.equal(calls.length, 0, 'Rejected packets must not dispatch');
console.log('Browser indirect dispatch: 4 exact forwards and 5 rejection checks passed.');
