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

/**
 * Skills ship from the repo root and are listed on the same page. Everything
 * there is installed for end users by `npx skills add`; skills for working on
 * this repository belong in `.agents/skills/`, and this test fails if one
 * lands here instead.
 */
const skillsDir = join(__dirname, '../../../../skills');

/** Matches a tool name wherever it appears — a table cell, backticks, or prose. */
const toolNamePattern = /\b[A-Z][A-Za-z]*_[A-Za-z][A-Za-z0-9]*\b/g;

/**
 * The page's section for tools only the hosted endpoint (`/mcp` on the API) offers. Its names are
 * expected to be absent from this package, so the stale check skips them; the API's own tests
 * (`McpToolCatalogTests`) own whether they exist.
 */
const hostedOnlyHeading = /^### Hosted-Only Tools\s*$/m;

let toolNames: string[];
let docText: string;
/** Every tool name the page mentions, parsed once and matched whole. */
let documented: Set<string>;
/** The tool names in the hosted-only section. */
let hostedOnly: Set<string>;

/** The text of the section under `heading`, up to the next heading of the same or a higher level. */
function sectionText(text: string, heading: RegExp): string {
  const start = text.search(heading);
  if (start < 0) return '';
  const body = text.slice(start).replace(heading, '');
  const end = body.search(/^#{1,3} /m);
  return end < 0 ? body : body.slice(0, end);
}

before(async () => {
  const { toolDefinitionMap } = await import('../build/tools/index.js');
  toolNames = [...toolDefinitionMap.keys()];
  docText = readFileSync(docsPath, 'utf8');
  documented = new Set(docText.match(toolNamePattern) ?? []);
  hostedOnly = new Set(sectionText(docText, hostedOnlyHeading).match(toolNamePattern) ?? []);
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

    const stale = [...documented]
      .filter((name) => !shipped.has(name) && !hostedOnly.has(name))
      .sort();

    assert.deepEqual(
      stale,
      [],
      `${stale.length} tool name(s) in docs/getting-started/mcp-server.mdx do not exist. ` +
        `Rename or remove them:\n  ${stale.join('\n  ')}`,
    );
  });

  test('the hosted-only section names no tool this package ships', () => {
    const shipped = new Set(toolNames);

    const misplaced = [...hostedOnly].filter((name) => shipped.has(name)).sort();

    assert.deepEqual(
      misplaced,
      [],
      `${misplaced.length} tool(s) listed under Hosted-Only Tools also ship in @wayd/mcp. ` +
        `Move them to the matching section of Available Tools:\n  ${misplaced.join('\n  ')}`,
    );
  });

  test('every user-facing skill is listed, and the stated count matches', () => {
    const shipped = readdirSync(skillsDir, { withFileTypes: true })
      .filter((entry) => entry.isDirectory())
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
      'eight', 'nine', 'ten', 'eleven', 'twelve', 'thirteen', 'fourteen', 'fifteen',
      'sixteen', 'seventeen', 'eighteen', 'nineteen', 'twenty'];
    const stated = docText.match(/(\w+) pre-built skills/)?.[1];

    assert.equal(
      stated,
      words[shipped.length],
      `The Agent Skills intro says "${stated} pre-built skills" but ${shipped.length} ship.`,
    );
  });
});
