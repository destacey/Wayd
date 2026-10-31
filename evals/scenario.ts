/**
 * The shape of a skill evaluation scenario, and the programmatic grader that scores a run of one.
 *
 * A scenario is one user request against stubbed Wayd tools: the agent's tool calls are answered from
 * `fixtures`, never from a live API, so a run is repeatable and changes nothing. What is graded is the
 * trajectory — which tools were called, with what arguments, in what order, and what was held back until
 * the user had replied.
 */

/** Known ways an agent goes wrong with Wayd tools; a scenario names the ones it probes. */
export type Trap =
  /** An update that omits or empties a role list (sponsorIds, ownerIds, managerIds, memberIds) wipes that role. */
  | 'role-list-wipe'
  /** An update is a whole-record overwrite: any field left out is cleared. */
  | 'whole-record-overwrite'
  /** A parameter that takes a UUID only is given a key, or a key is not resolved first. */
  | 'key-vs-uuid'
  /** A status change that is refused unless an earlier one has happened, or a field has been set. */
  | 'status-prerequisite'
  /** A destructive call made without the user's explicit confirmation. */
  | 'confirm-destructive'
  /** A request the tools cannot satisfy, which the agent should say rather than approximate. */
  | 'unsupported'
  /** Two similar tools or records, where picking the wrong one is the usual mistake. */
  | 'wrong-tool';

/**
 * Matches a value from a tool call against what a scenario expects.
 *
 * A plain object matches when every key it names matches (others are ignored); a plain array matches when
 * every element it lists matches some element of the actual array (order and extras are ignored); any other
 * value must be equal, except that strings compare case-insensitively. Operator objects refine this:
 * `{ "$exact": v }` requires deep equality, `{ "$absent": true }` requires the key to be missing or null,
 * `{ "$present": true }` requires a non-null value, `{ "$regex": "..." }` tests a string case-insensitively,
 * and `{ "$empty": true }` requires a missing, null or empty array.
 */
export type Matcher = unknown;

/** A canned answer to a tool call. The first fixture whose `tool` and `when` match answers the call. */
export interface Fixture {
  tool: string;
  /** Argument matcher; omit to answer every call to the tool. */
  when?: Record<string, Matcher>;
  /**
   * Answer only once the agent has called this tool, so a read after a change shows the change. Put it
   * before the fixture that answers the same read beforehand.
   */
  after?: string;
  /** The response body the API would return, serialized to the agent as JSON. */
  result: unknown;
  /** Return `result` as a tool error (an API problem response) instead of a success. */
  error?: boolean;
}

/** One checkable claim about a run. Every assertion must hold for the scenario to pass. */
export type Assertion = (
  /** The tool was called at least once, with arguments matching `with` when given. */
  | { type: 'called'; tool: string; with?: Record<string, Matcher> }
  /** No call to the tool matched `with` (or, without `with`, the tool was never called). */
  | { type: 'notCalled'; tool: string; with?: Record<string, Matcher> }
  /** Every call to the tool matched `with`; vacuously true when the tool was never called. */
  | { type: 'everyCall'; tool: string; with: Record<string, Matcher> }
  /** The first call to `first` came before the first call to `then`, and both were made. */
  | { type: 'order'; first: string; then: string }
  /**
   * The tool was not called until the user had replied at least once, so the agent stopped to confirm.
   * Pair it with `called` when the scripted reply approves the change.
   */
  | { type: 'confirmedBefore'; tool: string }
  /** The agent's final message matches the pattern (case-insensitive). Use only where no call can show it. */
  | { type: 'says'; pattern: string }
) & {
  /** Why this assertion matters, shown beside a failure. */
  because?: string;
};

export interface Scenario {
  /** Stable, path-safe identifier: `<skill without wayd->-<slug>`, e.g. `ppm-rename-project`. */
  id: string;
  /** The skill the scenario exercises, e.g. `wayd-ppm`. */
  skill: string;
  /** The user's opening message. */
  prompt: string;
  /**
   * Scripted user replies, sent in order each time the agent ends its turn. The run ends when the agent ends
   * a turn with none left. A reply that approves a change belongs here, so the agent can be graded on
   * whether it asked before acting. Name what the reply approves ("Yes, rename it."): a bare "Yes" also
   * approves anything else the agent offered, such as deleting a record it had just retired.
   */
  replies?: string[];
  traps?: Trap[];
  fixtures: Fixture[];
  expect: Assertion[];
  /** What a correct run looks like, for a reviewer reading the scenario. */
  expected: string;
}

/** A tool call the agent made, with the number of user replies it had received before making it. */
export interface RecordedCall {
  tool: string;
  args: Record<string, unknown>;
  repliesBefore: number;
}

/** What the grader needs from a run. */
export interface Trajectory {
  calls: RecordedCall[];
  finalText: string;
}

export interface AssertionResult {
  assertion: Assertion;
  passed: boolean;
  detail: string;
}

const isPlainObject = (v: unknown): v is Record<string, unknown> =>
  typeof v === 'object' && v !== null && !Array.isArray(v);

function deepEqual(a: unknown, b: unknown): boolean {
  if (a === b) return true;
  if (Array.isArray(a) && Array.isArray(b)) return a.length === b.length && a.every((x, i) => deepEqual(x, b[i]));
  if (isPlainObject(a) && isPlainObject(b)) {
    const keys = Object.keys(a);
    return keys.length === Object.keys(b).length && keys.every((k) => deepEqual(a[k], b[k]));
  }
  return false;
}

/** Whether `actual` satisfies `expected`; see {@link Matcher}. */
export function matches(actual: unknown, expected: Matcher): boolean {
  if (isPlainObject(expected)) {
    if ('$exact' in expected) return deepEqual(actual, expected.$exact);
    if ('$absent' in expected) return actual === undefined || actual === null;
    if ('$present' in expected) return actual !== undefined && actual !== null;
    if ('$empty' in expected) return actual === undefined || actual === null || (Array.isArray(actual) && actual.length === 0);
    if ('$regex' in expected) return typeof actual === 'string' && new RegExp(String(expected.$regex), 'i').test(actual);
    if (!isPlainObject(actual)) return false;
    return Object.entries(expected).every(([k, v]) => matches(actual[k], v));
  }
  if (Array.isArray(expected)) {
    return Array.isArray(actual) && expected.every((e) => actual.some((a) => matches(a, e)));
  }
  if (typeof expected === 'string' && typeof actual === 'string') return expected.toLowerCase() === actual.toLowerCase();
  return deepEqual(actual, expected);
}

/** The fixture that answers a call, given the tools already called, or undefined when the scenario has none. */
export function fixtureFor(
  scenario: Scenario,
  tool: string,
  args: Record<string, unknown>,
  calledBefore: ReadonlySet<string> = new Set(),
): Fixture | undefined {
  return scenario.fixtures.find((f) =>
    f.tool === tool
    && (f.after === undefined || calledBefore.has(f.after))
    && (f.when === undefined || matches(args, f.when)));
}

/**
 * The answer for a read no scenario needs to spell out: a record's activity log or status history, which an
 * agent may look at on any record before changing it. They answer empty, as a quiet record's would; without
 * them the agent is told a record it just read does not exist. Roadmaps_GetActivities is not one: it lists
 * roadmap items.
 */
export function defaultFixture(tool: string, args: Record<string, unknown>): Fixture | undefined {
  if (tool.endsWith('_GetStatusHistory')) return { tool, result: [] };
  if (tool.endsWith('_GetActivities') && tool !== 'Roadmaps_GetActivities') {
    const pageSize = typeof args.pageSize === 'number' ? args.pageSize : 20;
    const pageNumber = typeof args.pageNumber === 'number' ? args.pageNumber : 1;
    return {
      tool,
      result: { items: [], pageNumber, pageSize, totalCount: 0, totalPages: 0, hasPreviousPage: false, hasNextPage: false },
    };
  }
  return undefined;
}

const describeCalls =(calls: RecordedCall[]) =>
  calls.length === 0 ? 'never called' : calls.map((c) => JSON.stringify(c.args)).join('; ');

/** Scores one assertion against a trajectory. */
export function check(assertion: Assertion, run: Trajectory): AssertionResult {
  const callsTo = (tool: string) => run.calls.filter((c) => c.tool === tool);
  const result = (passed: boolean, detail: string): AssertionResult => ({ assertion, passed, detail });

  switch (assertion.type) {
    case 'called': {
      const calls = callsTo(assertion.tool);
      const hit = calls.some((c) => assertion.with === undefined || matches(c.args, assertion.with));
      return result(hit, hit ? 'called' : `${assertion.tool}: ${describeCalls(calls)}`);
    }
    case 'notCalled': {
      const bad = callsTo(assertion.tool).filter((c) => assertion.with === undefined || matches(c.args, assertion.with));
      return result(bad.length === 0, bad.length === 0 ? 'not called' : `${assertion.tool}: ${describeCalls(bad)}`);
    }
    case 'everyCall': {
      const bad = callsTo(assertion.tool).filter((c) => !matches(c.args, assertion.with));
      return result(bad.length === 0, bad.length === 0 ? 'every call matched' : `${assertion.tool}: ${describeCalls(bad)}`);
    }
    case 'order': {
      const first = run.calls.findIndex((c) => c.tool === assertion.first);
      const then = run.calls.findIndex((c) => c.tool === assertion.then);
      if (first < 0 || then < 0) return result(false, `${first < 0 ? assertion.first : assertion.then} never called`);
      return result(first < then, first < then ? 'in order' : `${assertion.then} came first`);
    }
    case 'confirmedBefore': {
      const early = callsTo(assertion.tool).filter((c) => c.repliesBefore === 0);
      return result(early.length === 0, early.length === 0 ? 'waited for the user' : `${assertion.tool} called before the user replied`);
    }
    case 'says': {
      const hit = new RegExp(assertion.pattern, 'i').test(run.finalText);
      return result(hit, hit ? 'matched' : `final message does not match /${assertion.pattern}/i`);
    }
  }
}

/** Scores a run: `pass` is 1 only when every assertion holds, `score` is the fraction that hold. */
export function grade(scenario: Scenario, run: Trajectory) {
  const results = scenario.expect.map((a) => check(a, run));
  const passed = results.filter((r) => r.passed).length;
  return {
    grade: { pass: passed === results.length ? 1 : 0, score: results.length ? passed / results.length : 0 },
    explanation: {
      pass: results
        .filter((r) => !r.passed)
        .map((r) => `${r.assertion.type} ${'tool' in r.assertion ? r.assertion.tool : ''}: ${r.detail}${r.assertion.because ? ` (${r.assertion.because})` : ''}`)
        .join('\n') || 'all assertions held',
    },
    results,
  };
}

/**
 * Structural problems that would make a scenario unrunnable or ungradeable: unknown tools, missing fields,
 * assertions on tools that do not exist. Returns one message per problem.
 */
export function validate(scenario: Scenario, toolNames?: Set<string>): string[] {
  const problems: string[] = [];
  const at = (m: string) => problems.push(`${scenario.id ?? '<no id>'}: ${m}`);
  if (!/^[a-z0-9-]+$/.test(scenario.id ?? '')) at('id must be lower-case kebab-case');
  if (!scenario.skill?.startsWith('wayd-')) at('skill must name a wayd-* skill');
  if (!scenario.prompt?.trim()) at('prompt is empty');
  if (!scenario.expected?.trim()) at('expected is empty');
  if (!scenario.expect?.length) at('expect has no assertions');
  for (const f of scenario.fixtures ?? []) {
    if (toolNames && !toolNames.has(f.tool)) at(`fixture for unknown tool ${f.tool}`);
    if (toolNames && f.after !== undefined && !toolNames.has(f.after)) at(`fixture after unknown tool ${f.after}`);
  }
  for (const a of scenario.expect ?? []) {
    for (const tool of 'tool' in a ? [a.tool] : a.type === 'order' ? [a.first, a.then] : []) {
      if (toolNames && !toolNames.has(tool)) at(`${a.type} assertion names unknown tool ${tool}`);
    }
    if (a.type === 'says') {
      try { new RegExp(a.pattern, 'i'); } catch { at(`invalid pattern ${a.pattern}`); }
    }
  }
  return problems;
}
