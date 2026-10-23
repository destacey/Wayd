/**
 * The skill evaluation's grader and scenario files (`evals/`). The evaluation itself runs Claude Code and is
 * never part of `npm test`; these tests make sure that what it grades with is sound: every scenario is
 * well-formed against the tools that ship, a correct trajectory passes, and an empty one fails.
 */
import { test, describe, before } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

import { check, defaultFixture, fixtureFor, grade, matches, validate, type Scenario, type Trajectory } from '../evals/scenario.js';

const __dirname = dirname(fileURLToPath(import.meta.url));
const scenariosDir = join(__dirname, '../evals/scenarios');

const loadScenarios = (): Scenario[] =>
  readdirSync(scenariosDir)
    .filter((f) => f.endsWith('.json'))
    .flatMap((f) => JSON.parse(readFileSync(join(scenariosDir, f), 'utf8')) as Scenario[]);

let toolNames: Set<string>;

before(async () => {
  const { toolDefinitionMap } = await import('../build/tools/index.js');
  toolNames = new Set(toolDefinitionMap.keys());
});

describe('matches', () => {
  test('an object matches on the keys it names and ignores the rest', () => {
    // Arrange
    const actual = { idOrKey: 'PRJ-12', name: 'Atlas', extra: true };

    // Act
    const hit = matches(actual, { idOrKey: 'prj-12' });
    const miss = matches(actual, { idOrKey: 'PRJ-13' });

    // Assert
    assert.equal(hit, true);
    assert.equal(miss, false);
  });

  test('an array matches when every listed element is present, in any order', () => {
    // Arrange
    const actual = { ownerIds: ['b', 'a', 'c'] };

    // Act
    const hit = matches(actual, { ownerIds: ['a', 'b'] });
    const miss = matches(actual, { ownerIds: ['a', 'd'] });

    // Assert
    assert.equal(hit, true);
    assert.equal(miss, false);
  });

  test('operators test presence, absence, emptiness, exact equality and patterns', () => {
    // Arrange
    const actual = { a: 1, b: null, c: [], d: ['x'], e: 'Approved by the board' };

    // Act
    const results = [
      matches(actual, { a: { $present: true } }),
      matches(actual, { b: { $absent: true } }),
      matches(actual, { missing: { $absent: true } }),
      matches(actual, { c: { $empty: true } }),
      matches(actual, { d: { $empty: true } }),
      matches(actual, { d: { $exact: ['x'] } }),
      matches(actual, { e: { $regex: 'board$' } }),
    ];

    // Assert
    assert.deepEqual(results, [true, true, true, true, false, true, true]);
  });
});

describe('check', () => {
  const run: Trajectory = {
    calls: [
      { tool: 'Projects_GetProject', args: { idOrKey: 'PRJ-1' }, repliesBefore: 0 },
      { tool: 'Projects_Activate', args: { id: 'u-1' }, repliesBefore: 1 },
    ],
    finalText: 'The project is now active.',
  };

  test('confirmedBefore fails when the tool ran before the user replied', () => {
    // Arrange
    const early: Trajectory = { ...run, calls: run.calls.map((c) => ({ ...c, repliesBefore: 0 })) };

    // Act
    const waited = check({ type: 'confirmedBefore', tool: 'Projects_Activate' }, run);
    const rushed = check({ type: 'confirmedBefore', tool: 'Projects_Activate' }, early);

    // Assert
    assert.equal(waited.passed, true);
    assert.equal(rushed.passed, false);
  });

  test('order requires both tools, the first one first', () => {
    // Act
    const inOrder = check({ type: 'order', first: 'Projects_GetProject', then: 'Projects_Activate' }, run);
    const reversed = check({ type: 'order', first: 'Projects_Activate', then: 'Projects_GetProject' }, run);
    const missing = check({ type: 'order', first: 'Projects_GetProject', then: 'Projects_Delete' }, run);

    // Assert
    assert.deepEqual([inOrder.passed, reversed.passed, missing.passed], [true, false, false]);
  });

  test('everyCall passes vacuously and fails on any non-matching call', () => {
    // Act
    const vacuous = check({ type: 'everyCall', tool: 'Projects_Delete', with: { id: 'x' } }, run);
    const failing = check({ type: 'everyCall', tool: 'Projects_GetProject', with: { idOrKey: 'PRJ-2' } }, run);

    // Assert
    assert.equal(vacuous.passed, true);
    assert.equal(failing.passed, false);
  });
});

describe('scenarios', () => {
  test('every scenario is well-formed and names only tools that ship', () => {
    // Arrange
    const scenarios = loadScenarios();

    // Act
    const problems = scenarios.flatMap((s) => validate(s, toolNames));

    // Assert
    assert.deepEqual(problems, []);
  });

  test('scenario ids are unique', () => {
    // Arrange
    const ids = loadScenarios().map((s) => s.id);

    // Act
    const duplicates = ids.filter((id, i) => ids.indexOf(id) !== i);

    // Assert
    assert.deepEqual(duplicates, []);
  });

  test('every published skill whose tools ship here has at least three scenarios', () => {
    // Arrange — the stub server offers only this package's tools, so a skill whose tools the hosted
    // endpoint alone offers cannot be evaluated here
    const skillsDir = join(__dirname, '../../../../skills');
    const prefixesOf = (skill: string): string[] => {
      const description = readFileSync(join(skillsDir, skill, 'SKILL.md'), 'utf8').match(/^description: (.*)$/m)?.[1] ?? '';
      return [...description.matchAll(/\b([A-Z][A-Za-z]*_)\*/g)].map((m) => m[1]);
    };
    const skills = readdirSync(skillsDir, { withFileTypes: true })
      .filter((e) => e.isDirectory())
      .map((e) => e.name)
      .filter((skill) => prefixesOf(skill).some((prefix) => [...toolNames].some((name) => name.startsWith(prefix))));
    const scenarios = loadScenarios();

    // Act
    const short = skills.filter((skill) => scenarios.filter((s) => s.skill === skill).length < 3);

    // Assert
    assert.deepEqual(short, [], `skills with fewer than three scenarios: ${short.join(', ')}`);
  });

  test('a run that does nothing fails every scenario', () => {
    // Arrange
    const idle: Trajectory = { calls: [], finalText: '' };

    // Act
    const passing = loadScenarios().filter((s) => grade(s, idle).grade.pass === 1).map((s) => s.id);

    // Assert
    assert.deepEqual(passing, [], 'these scenarios pass with no tool calls and no answer, so they measure nothing');
  });

  test('every fixture is reachable: no earlier fixture for the same tool answers every call first', () => {
    // Arrange
    const shadowed = loadScenarios().flatMap((s) =>
      s.fixtures
        .filter((f, i) => s.fixtures.slice(0, i).some((earlier) =>
          earlier.tool === f.tool && earlier.when === undefined && (earlier.after === undefined || earlier.after === f.after)))
        .map((f) => `${s.id}: ${f.tool}`),
    );

    // Assert
    assert.deepEqual(shadowed, []);
  });

  test('fixtureFor picks the first fixture whose arguments match', () => {
    // Arrange
    const scenario = {
      fixtures: [
        { tool: 'Projects_GetProject', when: { idOrKey: 'PRJ-1' }, result: 'one' },
        { tool: 'Projects_GetProject', result: 'any' },
      ],
    } as unknown as Scenario;

    // Act
    const specific = fixtureFor(scenario, 'Projects_GetProject', { idOrKey: 'prj-1' });
    const fallback = fixtureFor(scenario, 'Projects_GetProject', { idOrKey: 'PRJ-2' });

    // Assert
    assert.equal(specific?.result, 'one');
    assert.equal(fallback?.result, 'any');
  });

  test('defaultFixture answers histories and activity logs as empty, and nothing else', () => {
    // Act
    const history = defaultFixture('Projects_GetStatusHistory', { id: 'x' });
    const activities = defaultFixture('Projects_GetActivities', { idOrKey: 'ATLAS', pageSize: 5 });
    const roadmapItems = defaultFixture('Roadmaps_GetActivities', { idOrKey: 'x' });
    const record = defaultFixture('Projects_GetProject', { idOrKey: 'ATLAS' });

    // Assert
    assert.deepEqual(history?.result, []);
    assert.deepEqual(activities?.result, {
      items: [], pageNumber: 1, pageSize: 5, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false,
    });
    assert.equal(roadmapItems, undefined);
    assert.equal(record, undefined);
  });

  test('fixtureFor answers with an after fixture only once that tool has been called', () => {
    // Arrange
    const scenario = {
      fixtures: [
        { tool: 'Projects_GetProject', after: 'Projects_Update', result: 'renamed' },
        { tool: 'Projects_GetProject', result: 'original' },
      ],
    } as unknown as Scenario;

    // Act
    const before = fixtureFor(scenario, 'Projects_GetProject', {});
    const after = fixtureFor(scenario, 'Projects_GetProject', {}, new Set(['Projects_Update']));

    // Assert
    assert.equal(before?.result, 'original');
    assert.equal(after?.result, 'renamed');
  });
});
