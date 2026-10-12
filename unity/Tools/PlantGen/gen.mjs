import { writeFileSync } from 'node:fs';
import { MeshoptSimplifier } from 'meshoptimizer';
import { Tree } from './package/src/lib/tree.js';
import { loadPreset } from './package/src/lib/presets/index.js';
await MeshoptSimplifier.ready;

const jobs = JSON.parse(process.argv[2]);
const out = {};
for (const j of jobs) {
  const t = new Tree(); const opt = loadPreset(j.preset); opt.seed = j.seed ?? opt.seed;
  if (j.leafScale) opt.leaves.size *= j.leafScale;
  t.loadFromJson(opt);
  const bg = t.branchesMesh.geometry, lg = t.leavesMesh.geometry;
  const arr = a => Array.from(a.array);
  const B = { pos: arr(bg.attributes.position), nrm: arr(bg.attributes.normal), uv: arr(bg.attributes.uv), idx: Array.from(bg.index.array) };
  const L = { pos: arr(lg.attributes.position), nrm: arr(lg.attributes.normal), uv: arr(lg.attributes.uv), idx: Array.from(lg.index.array) };
  // branch LODs by simplification (attribute-aware enough for bark: keep borders)
  const simp = (ratio) => {
    if (ratio >= 1) return B.idx;
    const target = Math.max(36, Math.floor(B.idx.length * ratio / 3) * 3);
    const [res] = ratio < 0.2 ? [MeshoptSimplifier.simplifySloppy(new Uint32Array(B.idx), new Float32Array(B.pos), 3, null, target, 0.2)[0]] : MeshoptSimplifier.simplify(new Uint32Array(B.idx), new Float32Array(B.pos), 3, target, 0.08, []);
    return Array.from(res);
  };
  out[j.name] = { bark: opt.bark.type, leaf: opt.leaves.type, texScale: opt.bark.textureScale, B, L, lodB: j.lods.map(r => simp(r)) };
  console.error(j.name, j.preset, 'branch tris', B.idx.length / 3, 'leaf quads', L.idx.length / 6, 'lods', out[j.name].lodB.map(x => x.length / 3));
}
writeFileSync('trees.json', JSON.stringify(out));
