/**
 * Rescores recorded runs against the scenarios as they are now, without running anything again. Use it after
 * fixing an assertion: a run's calls do not depend on the assertions, so its grade can be recomputed.
 *
 *   npm run regrade -- results/claude-sonnet-5-5 [more result directories…]
 *
 * A row carries its calls and final message. Rows written before they did are rebuilt from the transcript,
 * which records every call in order and every scripted reply. A scenario's fixtures cannot be fixed this way:
 * they shape what the agent did, so changing one means running the scenario again.
 */
import { existsSync, readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

import { grade, type RecordedCall, type Scenario } from './scenario.js';
import { loadCases } from './harness.js';

interface Turn { role: string; name?: string; content: string }

/** The calls and final message of a run, rebuilt from its transcript. */
function fromTranscript(turns: Turn[]): { calls: RecordedCall[]; finalText: string } {
  const calls: RecordedCall[] = [];
  let users = 0;
  for (const turn of turns) {
    if (turn.role === 'user') users++;
    if (turn.role === 'tool_call' && turn.name?.startsWith('mcp__wayd__')) {
      calls.push({ tool: turn.name.slice('mcp__wayd__'.length), args: JSON.parse(turn.content), repliesBefore: users - 1 });
    }
  }
  const finalText = turns.filter((t) => t.role === 'assistant').at(-1)?.content ?? '';
  return { calls, finalText };
}

const scenarios = new Map<string, Scenario>((await loadCases()).map((c) => [c.id, c.scenario]));

for (const flow of process.argv.slice(2)) {
  for (const variant of readdirSync(flow).filter((d) => /^(baseline|v\d+)$/.test(d))) {
    const resultsPath = join(flow, variant, 'results.jsonl');
    if (!existsSync(resultsPath)) continue;
    let changed = 0;
    const rows = readFileSync(resultsPath, 'utf8').split('\n').filter(Boolean).map((line) => {
      const row = JSON.parse(line);
      const scenario = scenarios.get(row.prompt_id);
      if (!scenario) return row;
      let run = row.calls ? { calls: row.calls as RecordedCall[], finalText: String(row.final_text ?? '') } : undefined;
      if (!run) {
        const trace = join(flow, variant, 'traces', `${row.prompt_id}_rep${row.rep}.json`);
        if (!existsSync(trace)) {
          console.error(`${flow}/${variant} ${row.prompt_id}: no calls on the row and no transcript; left as it was`);
          return row;
        }
        run = fromTranscript(JSON.parse(readFileSync(trace, 'utf8')));
      }
      const { grade: g, explanation } = grade(scenario, run);
      if (g.pass !== row.grade?.pass || g.score !== row.grade?.score) {
        changed++;
        console.error(`${flow}/${variant} ${row.prompt_id}: pass ${row.grade?.pass} → ${g.pass}, score ${row.grade?.score} → ${g.score}`);
      }
      return { ...row, grade: g, explanation };
    });
    writeFileSync(resultsPath, rows.map((r) => JSON.stringify(r)).join('\n') + '\n');
    console.error(`${flow}/${variant}: ${rows.length} rows, ${changed} regraded`);
  }
}
