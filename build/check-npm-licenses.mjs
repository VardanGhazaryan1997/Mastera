// Fails when any npm package in the web workspace uses a license outside build/allowed-licenses.json.
// Usage (from web/): pnpm licenses
import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

export function isAllowed(expression, allowed) {
  const expr = String(expression).trim().replace(/^\((.*)\)$/, '$1');
  if (/\sOR\s/i.test(expr)) return expr.split(/\s+OR\s+/i).some((part) => isAllowed(part, allowed));
  if (/\sAND\s/i.test(expr)) return expr.split(/\s+AND\s+/i).every((part) => isAllowed(part, allowed));
  return allowed.includes(expr);
}

export function findViolations(report, allowed) {
  return Object.entries(report)
    .filter(([license]) => !isAllowed(license, allowed))
    .flatMap(([license, packages]) => packages.map(({ name, versions }) => ({ name, versions, license })));
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  const allowed = JSON.parse(readFileSync(new URL('./allowed-licenses.json', import.meta.url))).spdx;
  const report = JSON.parse(
    execFileSync('pnpm', ['licenses', 'list', '--recursive', '--json'], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 }),
  );
  const violations = findViolations(report, allowed);
  if (violations.length) {
    console.error('Packages with licenses that are not on the allow-list:');
    for (const v of violations) console.error(`  ${v.name}@${v.versions.join(',')}  ->  ${v.license}`);
    process.exit(1);
  }
  const total = Object.values(report).reduce((n, p) => n + p.length, 0);
  console.log(`License check passed: ${total} packages, all on the allow-list.`);
}
