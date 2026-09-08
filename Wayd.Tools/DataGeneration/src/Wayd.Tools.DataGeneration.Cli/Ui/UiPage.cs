namespace Wayd.Tools.DataGeneration.Cli.Ui;

/// <summary>
/// The page, inline.
/// </summary>
/// <remarks>
/// One string rather than a static-files folder, because the tool is a single executable people run from
/// anywhere — a page that only works when its assets happen to sit beside the binary is a page that stops
/// working the first time someone publishes it.
/// <para>
/// The form is built from the recipe schema at run time. Hand-written fields would drift the first time an
/// area was added to the recipe format, and the whole reason the schema is published is so a second front
/// end can be generated from it rather than maintained alongside it.
/// </para>
/// </remarks>
internal static class UiPage
{
    internal const string Html = """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>wayd-data</title>
<link rel="icon" href="data:image/svg+xml,<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 16 16'><text y='13' font-size='13'>&#9881;</text></svg>">
<style>
  :root {
    color-scheme: dark;
    --bg: #14161b; --panel: #1c1f26; --ink: #e8eaef; --muted: #969ca8;
    --line: #2a2e37; --accent: #7e97ff; --accent-ink: #10131a; --code-bg: #101218;
    --error: #ff8b7d;
    /* A control needs its own edge. The divider line is deliberately faint, and reusing it here left
       fields as unmarked rectangles on a panel of nearly the same colour. */
    --field-bg: #0f1117; --field-line: #667084;
  }
  * { box-sizing: border-box; }
  body {
    margin: 0; background: var(--bg); color: var(--ink);
    font: 14px/1.5 ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif;
  }
  header { padding: 24px 28px 8px; }
  h1 { margin: 0; font-size: 19px; letter-spacing: -0.01em; }
  header p { margin: 6px 0 0; color: var(--muted); max-width: 60ch; }
  main { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 20px; padding: 20px 28px 40px; align-items: start; }
  @media (max-width: 900px) { main { grid-template-columns: minmax(0, 1fr); } }
  section { background: var(--panel); border: 1px solid var(--line); border-radius: 10px; padding: 18px 20px; }
  h2 { margin: 0 0 14px; font-size: 12px; text-transform: uppercase; letter-spacing: 0.07em; color: var(--muted); font-weight: 600; }
  fieldset { border: 0; border-top: 1px solid var(--line); margin: 18px 0 0; padding: 14px 0 0; }
  fieldset:first-of-type { border-top: 0; margin-top: 0; padding-top: 0; }
  legend { padding: 0; font-weight: 600; font-size: 13px; }
  .hint { color: var(--muted); font-size: 12.5px; margin: 2px 0 10px; }
  .row { display: grid; grid-template-columns: 1fr 130px; gap: 10px; align-items: center; padding: 5px 0; }
  .row label { font-size: 13px; }
  .row .desc { color: var(--muted); font-size: 12px; }
  input, select { width: 100%; padding: 6px 8px; border: 1px solid var(--field-line); border-radius: 6px; background: var(--field-bg); color: var(--ink); font: inherit; }
  input::placeholder { color: var(--muted); }
  input:focus-visible, select:focus-visible, button:focus-visible { outline: 2px solid var(--accent); outline-offset: 1px; }
  input[type=checkbox] { width: auto; }
  button { font: inherit; padding: 7px 14px; border-radius: 7px; border: 1px solid var(--line); background: var(--panel); color: var(--ink); cursor: pointer; }
  button.primary { background: var(--accent); border-color: var(--accent); color: var(--accent-ink); font-weight: 600; }
  button:disabled { opacity: 0.55; cursor: default; }
  .actions { display: flex; gap: 8px; align-items: center; margin-top: 16px; flex-wrap: wrap; }
  pre { background: var(--code-bg); border: 1px solid var(--line); border-radius: 8px; padding: 12px; margin: 0; font: 12.5px/1.5 ui-monospace, "Cascadia Code", Consolas, monospace; white-space: pre-wrap; overflow-wrap: anywhere; }
  .stack > * + * { margin-top: 8px; }
  .head { display: flex; justify-content: space-between; align-items: center; gap: 8px; margin-bottom: 6px; }
  .head h3 { margin: 0; font-size: 12.5px; font-weight: 600; color: var(--muted); }
  table { width: 100%; border-collapse: collapse; }
  td { padding: 4px 0; border-bottom: 1px solid var(--line); }
  td:last-child { text-align: right; font-variant-numeric: tabular-nums; font-weight: 600; }
  .error { color: var(--error); white-space: pre-wrap; }
</style>
</head>
<body>
<header>
  <h1>wayd-data</h1>
  <p>Build a recipe, see what it generates, and take the command away with you. Seeding an environment
     stays on the command line — that needs a token, which belongs in a shell rather than a browser form.</p>
</header>

<main>
  <section>
    <h2>Recipe</h2>
    <div class="row">
      <label for="builtin">Start from</label>
      <select id="builtin"></select>
    </div>
    <p class="hint" id="builtin-desc"></p>
    <div id="form"></div>
    <div class="actions">
      <button class="primary" id="generate">Write CSVs</button>
      <button id="preview">Preview only</button>
      <input id="out" placeholder="output folder" style="flex:1;min-width:160px">
    </div>
    <p class="error" id="error"></p>
  </section>

  <section class="stack">
    <h2>Result</h2>
    <div id="counts"></div>

    <div>
      <div class="head"><h3>Command</h3><button data-copy="cli">Copy</button></div>
      <pre id="cli">wayd-data generate</pre>
    </div>

    <div>
      <div class="head"><h3>Recipe</h3><button data-copy="json">Copy</button></div>
      <pre id="json">{}</pre>
    </div>
  </section>
</main>

<script>
const token = new URLSearchParams(location.search).get('t');
// Taken out of the address bar immediately: the page keeps it in memory and sends it as a header, so it
// stops appearing in the URL, in history, and in anything a screenshot catches.
history.replaceState(null, '', location.pathname);

const api = async (path, options = {}) => {
  const res = await fetch(path, {
    ...options,
    headers: { 'X-Wayd-Ui-Token': token, 'Content-Type': 'application/json', ...(options.headers || {}) },
  });
  const body = await res.json().catch(() => null);
  if (!res.ok) {
    // A body-binding failure never reaches the endpoint, so it arrives with no body and no reason.
    // Saying "the server rejected this" beats echoing a bare status line back at someone.
    throw new Error(body?.error
      || `The tool rejected that request (${res.status}). This is a bug in wayd-data rather than in what you entered.`);
  }
  return body;
};

const el = id => document.getElementById(id);
let schema = null, recipe = {}, seed = null;

// Every control on the page comes from the schema. Adding an area to the recipe format puts it here with
// no change to this file, which is the only way a second front end stays in step with the first.
function buildForm() {
  const areas = Object.entries(schema.properties)
    .filter(([, spec]) => spec.type === 'object');

  el('form').innerHTML = '';
  for (const [area, spec] of areas) {
    const set = document.createElement('fieldset');
    const legend = document.createElement('legend');
    legend.textContent = humanise(area);
    set.append(legend);

    if (spec.description) {
      const hint = document.createElement('p');
      hint.className = 'hint';
      hint.textContent = spec.description;
      set.append(hint);
    }

    for (const [field, fieldSpec] of Object.entries(spec.properties)) {
      set.append(control(area, field, fieldSpec));
    }
    el('form').append(set);
  }
}

function control(area, field, spec) {
  const row = document.createElement('div');
  row.className = 'row';

  const label = document.createElement('label');
  label.htmlFor = `${area}.${field}`;
  label.textContent = humanise(field);
  if (spec.description) {
    const desc = document.createElement('div');
    desc.className = 'desc';
    desc.textContent = spec.description;
    label.append(desc);
  }

  const input = document.createElement(spec.enum ? 'select' : 'input');
  input.id = `${area}.${field}`;
  if (spec.enum) {
    for (const option of spec.enum) input.append(new Option(option, option));
  } else if (spec.type === 'boolean' || (Array.isArray(spec.type) && spec.type.includes('boolean'))) {
    input.type = 'checkbox';
  } else if (spec.type === 'integer' || spec.type === 'number') {
    input.type = 'number';
    if (spec.minimum !== undefined) input.min = spec.minimum;
    if (spec.maximum !== undefined) input.max = spec.maximum;
    if (spec.type === 'number') input.step = '0.01';
  } else if (spec.format === 'date') {
    input.type = 'date';
  } else {
    input.type = 'text';
  }
  input.addEventListener('change', () => { read(); render(); });

  row.append(label, input);
  return row;
}

// The form to a recipe. A control left empty states nothing, which is what lets the layer beneath show
// through — the same rule the file format follows.
function read() {
  recipe = {};
  for (const [area, spec] of Object.entries(schema.properties)) {
    if (spec.type !== 'object') continue;
    const block = {};
    for (const field of Object.keys(spec.properties)) {
      const input = el(`${area}.${field}`);
      if (!input) continue;
      if (input.type === 'checkbox') block[field] = input.checked;
      else if (input.value === '') continue;
      else if (input.type === 'number') block[field] = Number(input.value);
      else block[field] = input.value;
    }
    if (Object.keys(block).length) recipe[area] = block;
  }
}

function fill(values) {
  for (const [area, spec] of Object.entries(schema.properties)) {
    if (spec.type !== 'object') continue;
    for (const field of Object.keys(spec.properties)) {
      const input = el(`${area}.${field}`);
      if (!input) continue;
      const value = values?.[area]?.[field];
      if (input.type === 'checkbox') {
        input.checked = value !== false;
      } else {
        input.value = value ?? '';
        // Emptying a control does not mean zero, it means "not stated" — and what shows through is the
        // shipped default, not this recipe's value. Saying so beats a silent jump in scale.
        if (value !== undefined && value !== null) input.placeholder = `default (${value})`;
      }
    }
  }
}

function render() {
  el('json').textContent = JSON.stringify(recipe, null, 2);

  const parts = ['wayd-data generate'];
  // Stated, not truthy. A zero is a value someone typed, and dropping it from the command while leaving
  // it in the recipe means the command no longer reproduces what is on screen.
  const stated = value => value !== undefined && value !== null && value !== '';

  const chosen = el('builtin').value;
  if (chosen) parts.push(`--recipe ${chosen}`);
  if (seed !== null) parts.push(`--random-seed ${seed}`);
  if (stated(recipe.timeline?.asOf)) parts.push(`--as-of ${recipe.timeline.asOf}`);
  if (stated(recipe.organization?.teams)) parts.push(`--teams ${recipe.organization.teams}`);
  if (stated(recipe.organization?.valueStreams)) parts.push(`--value-streams ${recipe.organization.valueStreams}`);
  if (recipe.ppm?.enabled === false) parts.push('--skip-ppm');
  if (recipe.users?.enabled === false) parts.push('--skip-users');
  if (stated(el('out').value)) parts.push(`--out ${el('out').value}`);
  el('cli').textContent = parts.join(' ');
}

// camelCase to words, with the acronyms this domain actually uses left alone.
function humanise(name) {
  const spaced = name.replace(/([a-z0-9])([A-Z])/g, '$1 $2').toLowerCase();
  return spaced.replace(/^./, c => c.toUpperCase()).replace(/\bppm\b/gi, 'PPM').replace(/\bart\b/gi, 'ART');
}

function showCounts(result) {
  const rows = Object.entries(result.counts)
    .filter(([, value]) => value > 0)
    .map(([name, value]) => `<tr><td>${humanise(name)}</td><td>${value.toLocaleString()}</td></tr>`);
  el('counts').innerHTML = `<table>${rows.join('')}</table>` +
    (result.path ? `<p class="hint">Written to ${result.path}</p>` : '');
}

async function run(path) {
  el('error').textContent = '';
  read();
  try {
    const result = await api(path, {
      method: 'POST',
      body: JSON.stringify({ recipe, seed, out: el('out').value || null }),
    });
    // The seed a run actually used, so the printed command reproduces exactly what is on screen.
    seed = result.seed;
    showCounts(result);
    render();
  } catch (err) {
    el('error').textContent = err.message;
  }
}

document.addEventListener('click', async event => {
  const target = event.target.closest('[data-copy]');
  if (!target) return;
  await navigator.clipboard.writeText(el(target.dataset.copy).textContent);
  const original = target.textContent;
  target.textContent = 'Copied';
  setTimeout(() => { target.textContent = original; }, 1200);
});

el('generate').addEventListener('click', () => run('/api/generate'));
el('preview').addEventListener('click', () => run('/api/preview'));
el('out').addEventListener('change', render);

el('builtin').addEventListener('change', async () => {
  const name = el('builtin').value;
  const resolved = await api(`/api/recipes/${name}`);
  el('builtin-desc').textContent = resolved.description || '';
  fill(resolved);
  read();
  render();
});

(async () => {
  schema = await api('/api/schema');
  buildForm();

  const recipes = await api('/api/recipes');
  for (const { name } of recipes) el('builtin').append(new Option(name, name));
  el('builtin').value = 'default';
  el('builtin').dispatchEvent(new Event('change'));
})();
</script>
</body>
</html>
""";
}
