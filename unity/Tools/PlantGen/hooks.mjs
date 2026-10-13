import { readFileSync } from 'node:fs';
export async function resolve(spec, ctx, next) {
  if (/\.(jpg|png)$/.test(spec)) return { url: 'data:text/javascript,export default ""', shortCircuit: true };
  if (spec === './textures' || spec.endsWith('/textures.js')) return { url: new URL('./texstub.mjs', import.meta.url).href, shortCircuit: true };
  if (spec.startsWith('.') && !/\.(m?js|json)$/.test(spec)) spec += '.js';
  return next(spec, ctx);
}
export async function load(url, ctx, next) {
  if (url.endsWith('.json')) return { format: 'module', source: 'export default ' + readFileSync(new URL(url), 'utf8'), shortCircuit: true };
  return next(url, ctx);
}
