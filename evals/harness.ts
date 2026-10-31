/**
 * Runs one scenario through Claude Code (`claude -p`) against the stub server, and grades the result.
 *
 * Each run gets a fresh directory outside the repository, so no CLAUDE.md, settings or memory from this repo
 * reaches the agent. The `baseline` variant installs no skills; every other variant copies the repo's
 * `skills/` into the directory's `.claude/skills/`, so the agent has to choose and load a skill itself, as a
 * user's Claude Code would. In every variant the stub's `Skills_Get` serves the same skills, as the hosted
 * server does. Scripted replies continue the same session with `--resume`.
 */
import { execFileSync, spawn } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { homedir, tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

import { catalogPath, loadCatalog } from './catalog.js';
import { grade, validate, type RecordedCall, type Scenario } from './scenario.js';

const evalsDir = dirname(fileURLToPath(import.meta.url));
const scenariosDir = join(evalsDir, 'scenarios');
const repoRoot = join(evalsDir, '..');
const skillsDir = join(repoRoot, 'skills');
const stubServer = join(evalsDir, 'stub-server.ts');

/** One scenario as the runner sees it: `id`, `prompt` and `tags` are what the report shows. */
export interface Case {
  id: string;
  prompt: string;
  tags: string[];
  scenario: Scenario;
  scenarioFile: string;
}

/**
 * How a variant offers the skills: `none`; `available`, where the agent must choose one itself; or
 * `preloaded`, where the opening message invokes the scenario's skill (`/wayd-ppm …`) as a user can.
 * `available` measures whether a skill is chosen and helps; `preloaded` measures what its content does.
 */
type SkillMode = 'none' | 'available' | 'preloaded';

interface RunContext {
  model?: string;
  variant: string;
  /** `ref` pins a variant's skills to a commit, so an older version can run beside a newer one. */
  state: { effort?: string; variants?: Record<string, { skills?: SkillMode; ref?: string }> };
}

/** Writes the repository's `skills/` as of `ref` (or as on disk) into `target`. */
function installSkills(target: string, ref: string | undefined) {
  if (!ref) {
    cpSync(skillsDir, target, { recursive: true });
    return;
  }
  const git = (...args: string[]) => execFileSync('git', args, { cwd: repoRoot, maxBuffer: 64 * 1024 * 1024 });
  for (const path of git('ls-tree', '-r', '--name-only', ref, 'skills').toString().split('\n').filter(Boolean)) {
    const file = join(target, path.slice('skills/'.length));
    mkdirSync(dirname(file), { recursive: true });
    writeFileSync(file, git('show', `${ref}:${path}`));
  }
}

/** The variant's skill mode from `_state.json`; without an entry, `baseline` has none and others are available. */
const skillMode = (ctx: RunContext): SkillMode =>
  ctx.state.variants?.[ctx.variant]?.skills ?? (ctx.variant === 'baseline' ? 'none' : 'available');

/** A call as the stub server logged it. */
interface LoggedCall extends RecordedCall {
  fixtured: boolean;
  reachedApi: boolean;
  isError?: boolean;
}

/** A turn in the report's transcript format (see the claude-api eval report SCHEMA). */
interface Turn {
  role: 'user' | 'assistant' | 'tool_call' | 'tool_result';
  content: string;
  name?: string;
  thinking?: string;
}

/**
 * Every scenario under `scenarios/`, validated against the tools in the saved catalogue. `WAYD_EVAL_ONLY` narrows
 * the set to a comma-separated list of scenario ids or skill names, for a pilot or a single skill.
 */
export async function loadCases(): Promise<Case[]> {
  const toolNames = new Set(loadCatalog().tools.map((t) => t.name));
  const only = process.env.WAYD_EVAL_ONLY?.split(',').map((s) => s.trim()).filter(Boolean);

  const cases: Case[] = [];
  const problems: string[] = [];
  for (const file of readdirSync(scenariosDir).filter((f) => f.endsWith('.json')).sort()) {
    const scenarioFile = join(scenariosDir, file);
    for (const scenario of JSON.parse(readFileSync(scenarioFile, 'utf8')) as Scenario[]) {
      problems.push(...validate(scenario, toolNames));
      if (only && !only.includes(scenario.id) && !only.includes(scenario.skill)) continue;
      cases.push({ id: scenario.id, prompt: scenario.prompt, tags: [scenario.skill, ...(scenario.traps ?? [])], scenario, scenarioFile });
    }
  }
  if (problems.length) throw new Error(`invalid scenarios:\n  ${problems.join('\n  ')}`);
  return cases;
}

/** Runs `claude` with the prompt on stdin, resolving with its stream-json events. */
function claude(args: string[], prompt: string, cwd: string): Promise<any[]> {
  return new Promise((resolve, reject) => {
    // On Windows `claude` is a .cmd shim, which only a shell can start, so it gets one command line; every
    // argument is a flag, a model id or a path, and the prompt goes on stdin, so quoting spaces is enough.
    const child = process.platform === 'win32'
      ? spawn(['claude', ...args.map((a) => (/\s/.test(a) ? `"${a}"` : a))].join(' '), { cwd, shell: true, stdio: ['pipe', 'pipe', 'pipe'] })
      : spawn('claude', args, { cwd, stdio: ['pipe', 'pipe', 'pipe'] });
    let stdout = '';
    let stderr = '';
    child.stdout.on('data', (d) => (stdout += d));
    child.stderr.on('data', (d) => (stderr += d));
    child.on('error', reject);
    child.on('close', (code) => {
      const events = stdout.split('\n').filter((l) => l.trim().startsWith('{')).map((l) => JSON.parse(l));
      if (!events.some((e) => e.type === 'result')) {
        reject(new Error(`claude exited ${code} without a result: ${stderr.trim().slice(0, 500)}`));
      } else if (stderr.includes('unrecognized_model')) {
        // An older Claude Code runs a model it has no catalog entry for with guessed settings, so the run
        // would measure that CLI as much as the skills.
        reject(new Error(`this Claude Code does not recognize the model; update it: ${stderr.trim().slice(0, 300)}`));
      } else {
        resolve(events);
      }
    });
    child.stdin.end(prompt);
  });
}

/**
 * The session Claude Code saved for this run. Its folder is named for the run's temporary directory, so it
 * holds nothing else and is removed with it.
 */
function sessionFile(sessionId: string): string | undefined {
  const projects = join(homedir(), '.claude', 'projects');
  if (!existsSync(projects)) return undefined;
  return readdirSync(projects)
    .map((dir) => join(projects, dir, `${sessionId}.jsonl`))
    .find((file) => existsSync(file));
}

const text = (content: unknown): string =>
  typeof content === 'string'
    ? content
    : Array.isArray(content)
      ? content.map((b) => (b?.type === 'text' ? b.text : '')).join('')
      : JSON.stringify(content);

/** Runs a scenario to completion: the opening prompt, then each scripted reply in the same session. */
export async function runCase(c: Case, ctx: RunContext) {
  const mode = skillMode(ctx);
  const work = mkdtempSync(join(tmpdir(), 'wayd-eval-'));
  // Outside the run directory, so a variant without skills cannot find them except through Skills_Get.
  const served = mkdtempSync(join(tmpdir(), 'wayd-eval-skills-'));
  try {
    installSkills(served, ctx.state.variants?.[ctx.variant]?.ref);
    if (mode !== 'none') installSkills(join(work, '.claude', 'skills'), ctx.state.variants?.[ctx.variant]?.ref);
    const callLog = join(work, 'calls.jsonl');
    const sessionId = randomUUID();
    const opening = mode === 'preloaded' ? `/${c.scenario.skill} ${c.scenario.prompt}` : c.scenario.prompt;
    const messages = [opening, ...(c.scenario.replies ?? [])];
    const transcript: Turn[] = [];
    const usage = { input_tokens: 0, output_tokens: 0, cache_read_input_tokens: 0, cache_creation_input_tokens: 0 };
    let model: string | undefined;
    let claudeCodeVersion: string | undefined;
    let finalText = '';
    let stopReason: string | undefined;
    let costUsd = 0;
    let numTurns = 0;
    const skillsLoaded = new Set<string>();
    const toolNames = new Map<string, string>();

    for (const [replies, message] of messages.entries()) {
      const mcpConfig = join(work, `mcp-${replies}.json`);
      writeFileSync(mcpConfig, JSON.stringify({
        mcpServers: {
          wayd: {
            command: process.execPath,
            args: ['--import', import.meta.resolve('tsx'), stubServer],
            env: {
              WAYD_EVAL_SCENARIOS: c.scenarioFile,
              WAYD_EVAL_SCENARIO_ID: c.id,
              WAYD_EVAL_CALL_LOG: callLog,
              WAYD_EVAL_REPLIES: String(replies),
              WAYD_EVAL_SKILLS: served,
              WAYD_EVAL_CATALOG: catalogPath,
            },
          },
        },
      }));
      const events = await claude([
        '-p', '--output-format', 'stream-json', '--verbose',
        ...(ctx.model ? ['--model', ctx.model] : []),
        ...(ctx.state.effort ? ['--effort', ctx.state.effort] : []),
        // Project settings only: the run directory has none, so the user's plugins, personal skills, output
        // style and hooks stay out, while project skills in .claude/skills still load. (--restricted would
        // hide those skills too.)
        '--setting-sources', 'project', '--strict-mcp-config', '--mcp-config', mcpConfig,
        '--tools', 'Skill,Read',
        '--allowedTools', 'mcp__wayd',
        '--permission-prompts', 'none',
        ...(replies === 0 ? ['--session-id', sessionId] : ['--resume', sessionId]),
      ], message, work);

      transcript.push({ role: 'user', content: message });
      for (const e of events) {
        if (e.type === 'system' && e.subtype === 'init') {
          model ??= e.model;
          claudeCodeVersion ??= e.claude_code_version;
          // A run whose condition did not take effect would score as a result; fail it as a harness error.
          const waydSkills = (e.skills ?? []).filter((s: string) => s.startsWith('wayd-')).length;
          if ((mode === 'none') !== (waydSkills === 0)) {
            throw new Error(`variant ${ctx.variant} (skills: ${mode}) started with ${waydSkills} Wayd skills available`);
          }
          const wayd = e.mcp_servers?.find((s: { name: string }) => s.name === 'wayd');
          if (wayd && wayd.status !== 'connected') throw new Error(`stub server status: ${wayd.status}`);
        } else if (e.type === 'assistant') {
          for (const block of e.message?.content ?? []) {
            if (block.type === 'text' && block.text.trim()) transcript.push({ role: 'assistant', content: block.text });
            if (block.type === 'tool_use') {
              toolNames.set(block.id, block.name);
              transcript.push({ role: 'tool_call', name: block.name, content: JSON.stringify(block.input, null, 2) });
              if (block.name === 'Skill') skillsLoaded.add(String(block.input?.skill ?? block.input?.command ?? ''));
            }
          }
        } else if (e.type === 'user') {
          for (const block of e.message?.content ?? []) {
            // Named for its call, since parallel calls' results arrive after all of the calls.
            if (block.type === 'tool_result') {
              transcript.push({ role: 'tool_result', name: toolNames.get(block.tool_use_id), content: text(block.content) });
            }
          }
        } else if (e.type === 'result') {
          finalText = e.result ?? '';
          stopReason = e.stop_reason ?? e.subtype;
          costUsd += e.total_cost_usd ?? 0;
          numTurns += e.num_turns ?? 0;
          for (const k of Object.keys(usage) as (keyof typeof usage)[]) usage[k] += e.usage?.[k] ?? 0;
        }
      }
    }

    // A slash command is expanded before the model sees it and never appears in the stream; the saved
    // session shows it as the skill's text, which only appears when the skill really loaded.
    const session = sessionFile(sessionId);
    const loadedLine = new RegExp(`Base directory for this skill: [^"]*[\\\\/]${c.scenario.skill}\\b`);
    if (mode === 'preloaded' && session && loadedLine.test(readFileSync(session, 'utf8'))) {
      skillsLoaded.add(c.scenario.skill);
    }
    if (session) rmSync(dirname(session), { recursive: true, force: true });

    const calls: LoggedCall[] = existsSync(callLog)
      ? readFileSync(callLog, 'utf8').split('\n').filter(Boolean).map((l) => JSON.parse(l))
      : [];
    return {
      output: finalText,
      transcript,
      model,
      usage,
      stop_reason: stopReason,
      calls,
      skillsLoaded: [...skillsLoaded],
      claudeCodeVersion,
      costUsd,
      numTurns,
    };
  } finally {
    rmSync(work, { recursive: true, force: true });
    rmSync(served, { recursive: true, force: true });
  }
}

type Run = Awaited<ReturnType<typeof runCase>>;

export async function gradeCase(c: Case, run: Run) {
  const { grade: g, explanation } = grade(c.scenario, { calls: run.calls, finalText: run.output });
  return { grade: g, explanation };
}

/**
 * Side-channel measures for the report. `api_equiv_usd` is what Claude Code reports the run would cost at API
 * prices; runs on a subscription are not billed per token, so it is a size measure, not a charge.
 */
export function perfFrom(c: Case, run: Run) {
  return {
    tool_calls: run.calls.length,
    unfixtured_calls: run.calls.filter((call) => call.reachedApi && !call.fixtured).length,
    skill_loaded: run.skillsLoaded.includes(c.scenario.skill) ? 1 : 0,
    skills: run.skillsLoaded,
    num_turns: run.numTurns,
    api_equiv_usd: Math.round(run.costUsd * 10000) / 10000,
    // What the grader read, kept on the row so `regrade.ts` can rescore it after a scenario's assertions change.
    // Runs are only comparable on the same Claude Code, which shapes the agent as much as the skills do.
    claude_code_version: run.claudeCodeVersion,
    calls: run.calls.map(({ tool, args, repliesBefore }) => ({ tool, args, repliesBefore })),
    final_text: run.output,
  };
}
