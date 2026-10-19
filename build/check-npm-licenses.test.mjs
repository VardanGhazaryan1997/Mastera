// Run with: node --test build/
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { findViolations, isAllowed } from './check-npm-licenses.mjs';

const allowed = ['MIT', 'Apache-2.0', 'CC0-1.0'];

test('a plain allowed SPDX id passes', () => {
  assert.equal(isAllowed('MIT', allowed), true);
});

test('a commercial or unknown license fails', () => {
  assert.equal(isAllowed('SEE LICENSE IN LICENSE.md', allowed), false);
  assert.equal(isAllowed('UNLICENSED', allowed), false);
  assert.equal(isAllowed('Unknown', allowed), false);
});

test('an OR expression passes when any option is allowed', () => {
  assert.equal(isAllowed('(MIT OR GPL-3.0)', allowed), true);
});

test('an AND expression passes only when every part is allowed', () => {
  assert.equal(isAllowed('(MIT AND CC0-1.0)', allowed), true);
  assert.equal(isAllowed('(MIT AND BUSL-1.1)', allowed), false);
});

test('findViolations lists offending packages with their license', () => {
  const report = {
    MIT: [{ name: 'react', versions: ['19.2.9'] }],
    'BUSL-1.1': [{ name: 'some-paid-lib', versions: ['2.0.0'] }],
  };
  assert.deepEqual(findViolations(report, allowed), [
    { name: 'some-paid-lib', versions: ['2.0.0'], license: 'BUSL-1.1' },
  ]);
});
