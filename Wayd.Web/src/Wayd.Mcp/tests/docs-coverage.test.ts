/**
 * Documentation coverage: every tool the server ships must appear in the
 * user-facing docs, and the docs must not name tools that no longer exist.
 *
 * The guarded failure mode is silent drift. `docs/getting-started/mcp-server.mdx`
 * lists tools in hand-written tables, so a new tool ships documented only if
 * somebody remembers to add a row. Nothing failed when they didn't: at the time
 * this test was written 90 of 220 tools were undocumented, and roughly a quarter
 * of that gap predated the release that prompted it. Lint, typecheck, build and
 * every other test passed throughout.
 *
 * A row may cover several tools at once (`Foo_Create` / `Foo_Update`), which is
 * why this collects tool names appearing anywhere in the file rather than
 * expecting one row per tool. Prose is deliberately left to the author — this
 * only asserts that no tool is missing and none is stale.
 *
 * Names are compared as a parsed set, never with `includes`. Substring matching
 * looks equivalent and is not: 27 of the tool names are a prefix of a longer one
 * (`Products_GetProduct` inside `Products_GetProducts`, and every other
 * singular/plural pair), so a deleted row for the shorter name would still be
 * "found" in the longer one and the guard would pass over its own blind spot.
 */
import { test, describe, before } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const __dirname = dirname(fileURLToPath(import.meta.url));

/** The user-facing tool reference, four levels up from the MCP package. */
const docsPath = join(__dirname, '../../../../docs/getting-started/mcp-server.mdx');

/** Skills ship from the repo root and are listed on the same page. */
const skillsDir = join(__dirname, '../../../../skills');

/**
 * Skills that guide work *on this repository* rather than against a Wayd
 * instance. They are not part of the MCP server's offering, so the user-facing
 * page deliberately omits them.
 */
const contributorSkills = new Set(['wayd-testing']);

/** Matches a tool name wherever it appears — a table cell, backticks, or prose. */
const toolNamePattern = /\b[A-Z][A-Za-z]*_[A-Za-z][A-Za-z0-9]*\b/g;

let toolNames: string[];
let docText: string;
/** Every tool name the page mentions, parsed once and matched whole. */
let documented: Set<string>;

before(async () => {
  const { toolDefinitionMap } = await import('../build/tools/index.js');
  toolNames = [...toolDefinitionMap.keys()];
  docText = readFileSync(docsPath, 'utf8');
  documented = new Set(docText.match(toolNamePattern) ?? []);
});

describe('documentation coverage', () => {
  test('every shipped tool is documented', () => {
    const missing = toolNames.filter((name) => !documented.has(name));

    assert.deepEqual(
      missing,
      [],
      `${missing.length} tool(s) ship but are absent from docs/getting-started/mcp-server.mdx. ` +
        `Add a row under the matching section:\n  ${missing.join('\n  ')}`,
    );
  });

  test('the docs name no tool that no longer ships', () => {
    const shipped = new Set(toolNames);

    const stale = [...documented].filter((name) => !shipped.has(name)).sort();

    assert.deepEqual(
      stale,
      [],
      `${stale.length} tool name(s) in docs/getting-started/mcp-server.mdx do not exist. ` +
        `Rename or remove them:\n  ${stale.join('\n  ')}`,
    );
  });

  test('every user-facing skill is listed, and the stated count matches', () => {
    const shipped = readdirSync(skillsDir, { withFileTypes: true })
      .filter((entry) => entry.isDirectory() && !contributorSkills.has(entry.name))
      .map((entry) => entry.name)
      .sort();

    const missing = shipped.filter((name) => !docText.includes(`\`/${name}\``));

    assert.deepEqual(
      missing,
      [],
      `${missing.length} skill(s) ship but are absent from the Agent Skills list:\n  ${missing.join('\n  ')}`,
    );

    // The prose states a count ("eight pre-built skills"), which drifts as
    // silently as the list itself did.
    const words = ['zero', 'one', 'two', 'three', 'four', 'five', 'six', 'seven',
      'eight', 'nine', 'ten', 'eleven', 'twelve'];
    const stated = docText.match(/(\w+) pre-built skills/)?.[1];

    assert.equal(
      stated,
      words[shipped.length],
      `The Agent Skills intro says "${stated} pre-built skills" but ${shipped.length} ship.`,
    );
  });
});
