# @wayd/mcp

A [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server for the [Wayd](https://wayd.dev) work management API. Exposes Wayd's project portfolio management, planning, and work item data to AI assistants.

## Requirements

- Node.js >= 22
- A running Wayd instance with API access
- A Wayd Personal Access Token (PAT)

## Configuration

The server requires two values: the base URL of your Wayd instance and an API key. These can be supplied as **environment variables** or **CLI arguments** — CLI arguments take priority if both are provided.

| | Environment variable | CLI argument |
|---|---|---|
| Base URL | `WAYD_API_BASE_URL` | `--base-url` |
| API key | `WAYD_API_KEY` | `--api-key` |

## Installation

### Claude Desktop

CLI arguments are not supported in Claude Desktop — use environment variables. Add to `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or `%APPDATA%\Claude\claude_desktop_config.json` (Windows):

```json
{
  "mcpServers": {
    "wayd": {
      "command": "npx",
      "args": ["-y", "@wayd/mcp"],
      "env": {
        "WAYD_API_BASE_URL": "https://your-wayd-instance.com",
        "WAYD_API_KEY": "your-personal-access-token"
      }
    }
  }
}
```

### VS Code / Cursor (with `inputs`)

CLI args enable the `inputs` pattern, which prompts for values at connection time instead of hardcoding them. Add to `.vscode/mcp.json` or `.cursor/mcp.json`:

```json
{
  "inputs": [
    {
      "id": "waydBaseUrl",
      "description": "Wayd base URL",
      "type": "promptString"
    },
    {
      "id": "waydApiKey",
      "description": "Wayd API key (Personal Access Token)",
      "type": "promptString",
      "password": true
    }
  ],
  "servers": {
    "wayd": {
      "type": "stdio",
      "command": "npx",
      "args": [
        "-y", "@wayd/mcp",
        "--base-url", "${input:waydBaseUrl}",
        "--api-key",  "${input:waydApiKey}"
      ]
    }
  }
}
```

Or with environment variables directly:

```json
{
  "servers": {
    "wayd": {
      "type": "stdio",
      "command": "npx",
      "args": ["-y", "@wayd/mcp"],
      "env": {
        "WAYD_API_BASE_URL": "https://your-wayd-instance.com",
        "WAYD_API_KEY": "your-personal-access-token"
      }
    }
  }
}
```

### Claude Code

Claude Code doesn't support the `inputs` prompt pattern, so the recommended way to keep your PAT out of config files is to store it in an environment variable in your shell profile and reference it in the MCP config.

Add to `~/.zshrc` / `~/.bashrc` (or equivalent):

```bash
export WAYD_API_BASE_URL="https://your-wayd-instance.com"
export WAYD_API_KEY="your-personal-access-token"
```

Then register the server via the CLI (values are read from your environment at connection time):

```bash
claude mcp add wayd -- npx -y @wayd/mcp
```

Or add it to a project-level `.mcp.json` that reads from the same environment variables:

```json
{
  "mcpServers": {
    "wayd": {
      "command": "npx",
      "args": ["-y", "@wayd/mcp"]
    }
  }
}
```

Because the server reads `WAYD_API_BASE_URL` and `WAYD_API_KEY` from the environment automatically, no credentials appear in any config file.

### Global install

```bash
npm install -g @wayd/mcp
```

Then use `wayd-mcp` as the command instead of `npx -y @wayd/mcp` in any of the configs above.

## Agent Skills (Claude Code)

Skills are prompt files that guide Claude on how to efficiently use the Wayd MCP tools — which tools to call in sequence, how to resolve IDs, and what the entity relationships look like. Without them, agents tend to make redundant calls or miss non-obvious patterns (e.g. project lifecycle transitions use separate action endpoints, not a status field).

Nine self-contained skills are available:

| Skill | Trigger |
| --- | --- |
| `wayd-ppm` | Portfolios, programs, projects — lookup, plans, health checks, task management |
| `wayd-delivery` | Releases, versions, packages, deployments — what was announced, built, shipped together, and deployed where |
| `wayd-products` | The product catalog — the typed tree, dependencies, types, tags, environments, what is running where, and delivery measures |
| `wayd-pi` | Planning intervals, iterations, objectives, health reports, risks |
| `wayd-roadmaps` | Roadmap exploration — activities, timeboxes, milestones |
| `wayd-story-maps` | Story maps — analyze, create, and manage goals, steps, tasks, swim lanes, personas |
| `wayd-teams` | Team lookup — resolve a team name to an ID |
| `wayd-users` | User lookup — resolve a user name to a UUID for assignees and project roles |
| `wayd-imports` | CSV imports — write a file in the right format, preflight it, import the rows it checked, and follow or re-run import runs |

### Installing the skills

From your project root:

```bash
npx skills add destacey/Wayd
```

Once installed, activate a skill in Claude Code with `/wayd-ppm`, `/wayd-delivery`, `/wayd-products`, `/wayd-pi`, `/wayd-roadmaps`, `/wayd-story-maps`, `/wayd-teams`, `/wayd-users`, or `/wayd-imports`.

## Confirmation before status changes

Tools that change a record's published status — activating, completing, cancelling, closing, or archiving a portfolio, program, project, or strategic initiative, or reverting a project to an earlier status — are advertised to clients with the MCP `destructiveHint` annotation, as are the tools that permanently delete something and those that import, stop, resume, or retry an import run. Clients that honour the annotation prompt for confirmation before running them.

Two caveats worth knowing:

- The hint is **advisory**. It tells a client to ask; it cannot force one to. Authorization is still enforced server-side, and PPM mutations additionally require delivery leadership (Owner or Manager on the record or an ancestor) regardless of what any client does.
- A tool with no annotation is treated as read-only **only** when its underlying request is a GET. Any new write tool must opt in explicitly, so it can never be silently advertised as safe.

### Updates overwrite the whole record

Every update tool maps to a `PUT` that rewrites the record from the request body — these are not patches. A field left out of the body is cleared, not preserved, so callers must read the record first and pass back everything that should stay the same.

This matters most for role assignments. `sponsorIds`, `ownerIds`, `managerIds`, and `memberIds` replace the membership of that role, and a list that is **omitted or empty removes everyone currently holding it** — updating only a project's name will strip its entire team unless the existing membership is passed back. The tool descriptions and the `wayd-ppm` skill both spell this out.

## Available Tools

Records that raise domain events expose an **activity history** — every recorded change, newest first, with who made it and the before and after values. A record that predates tracking starts with a `Baseline` entry holding what it looked like when tracking began.

### Project Portfolio Management

| Category | Operations |
| --- | --- |
| **Portfolios** | List, get details, get activity history, get programs, get projects, get strategic initiatives, get ranking scoreboard. Create, update. Status: activate, close, archive |
| **Strategic Initiatives** | List, get details, get activity history, get statuses, get linked projects. KPIs: list, get details, get checkpoints, get checkpoint plan, list measurements, add measurement, remove measurement. Status: approve, activate, complete, cancel |
| **Programs** | List, get details, get activity history, get projects. Create, update. Status: activate, complete, cancel |
| **Project Lifecycles** | List (with state filter), get details |
| **Expenditure Categories** | Get options (for project create/update) |
| **Projects** | List (with role filter), get details, get activity history, get status history, get my involvement summary, get my task metrics, get team, get stages, get stage details, get plan tree, get plan summary (single and batch), list health checks, get health check, create health check, get scoring context, list scores, get score, update/delete health check. Create, update, change program, change key. Status: approve, activate, complete, cancel, revert to an earlier status |
| **Tasks** | List, get details, get critical path, get types/statuses/priorities, create, update, delete, add/remove dependencies |

### Planning

| Category | Operations |
| --- | --- |
| **Planning Intervals** | List, get details, activity history, calendar, predictability, teams, iterations, objectives, objective activity history, risks, objective health check history, get/create objective health check |
| **Roadmaps** | List, get details, get items and activities |
| **Story Maps** | List, get full map. Create, update, archive, delete maps. Manage goals, steps, tasks, checklists, swim lanes, personas, and work item links |

### Product Catalog

| Category | Operations |
| --- | --- |
| **Products** | List (by parent, type, status category, or tags), get details, get activity history, get status history, get status options, get dependencies (rolled up, both directions). Create, update, retype, reparent, change status, link externally, tag, untag, delete. Dependencies: add, reword, change strength, end, remove |
| **Product Types** | List, create, update, activate or deactivate, delete — the types a product can be, and whether each allows versions to be cut against it |
| **Product Tag Categories** | List, create, update, activate or deactivate, delete, reorder — the tag axes and their tags, with whether each axis allows more than one tag. Add, rename, activate or deactivate, delete the tags themselves |
| **Deployment Environments** | List (by active state or category), get what is running in each (rollout), create, update, retire or reinstate, delete |
| **Delivery Metrics** | Get the deployment measures over a window |
| **Delivery Overview** | Get version activity over a window (release frequency, cut-to-released), get recent version and package events |

The catalog is one typed tree, and a product's type carries the flag that decides whether versions can be cut against it. Type, parent and status each have their own tool rather than being fields on the update, because each carries a rule the domain enforces. Two behaviours the `wayd-products` skill covers: tagging a single-value axis **silently replaces** the existing tag rather than refusing, and deleting a product is a **hard delete**, unlike delivery where records are withdrawn and kept.

Types and tag categories are administrator-managed configuration, and two rules run through all of it. **Seeded system records cannot be modified or deleted** — but they *can* be deactivated, so an organization can hide a type it does not use without the seeder recreating it. And **nothing in use can be deleted**; deactivation is the answer there too, stopping new use while leaving existing records resolvable. Two sharp edges: a category's `allowsMany` is **fixed at creation**, and `ProductTypes_Update` **requires `isReleasable`**, so a rename that resends the wrong value silently changes whether versions can be cut against every product of that type.

### Product Delivery

Four records that are deliberately kept apart: a **release** is what was announced to customers (`Wayd 2026.09`), a **version** is one artifact that was built (`Wayd API 4.12.0`), a **package** is what moved through environments together (`WAYD-2026.09.1`), and a **deployment** is one of those reaching one environment.

| Category | Operations |
| --- | --- |
| **Releases** | List (by product, status category, or containing version), get details, get activity history, get status history. Plan, update, set contents, correct dates, move target date. Status: announce, withdraw, revert |
| **Versions** | List (by product or status category), get details, get activity history, get status history. Plan, update, correct dates, move target date. Status: cut, mark released, withdraw, revert |
| **Release Packages** | List (by status category, containing product, or containing version), get details, get activity history, get status history. Assemble with manifest, replace manifest. Status: mark released, withdraw. Delete |
| **Deployments** | List (by version, package, environment, environment category, or start date), get details, get activity history, get status history. Start. Outcome: succeed, fail, roll back. Delete |

Two rules the tools enforce and the `wayd-delivery` skill explains: a version shipping inside one of a release's packages cannot also be carried directly on that release, and a release cannot be announced while anything it carries has not shipped. `Releases_SetContents` and `ReleasePackages_SetManifest` are **whole-set replacements** — read the record first and send back everything it should end up with.

### Organization

| Category | Operations |
| --- | --- |
| **Teams** | List, get details, get activity history |
| **Users** | List, get details |

### Imports

| Category | Operations |
| --- | --- |
| **Import files** | Get a kind of import's file format (columns, required cells, accepted values). Preflight a CSV file |
| **Import runs** | List import types, list runs (by status, type, submitter, or submission group), get a run, get its row outcomes. Stop, resume, retry rejected rows, apply a preflight |

Every CSV file submitted to Wayd becomes a run, whether it came from Settings → Imports or an API call, so these tools follow and control either.

**Files are imported through a preflight.** `Imports_Preflight` always sends `validateOnly=true`, so it checks every row and creates nothing; there is no tool that submits a file for real. The only way in is `Imports_Apply` on the finished preflight, which is annotated destructive, so an agent has seen every row's outcome before a client is asked to confirm. Before posting, it reads `/api/imports/definitions` and sends nothing to a Wayd older than 0.210.0, which ignores `validateOnly` and would import the file for real. Applying the same preflight twice duplicates what the first import created where the import has no natural key.

`Imports_GetFileFormat` is answered from the OpenAPI document this package was built against, without a request, so it describes the columns of the Wayd release the package matches.

## Links

- [Wayd documentation](https://wayd.dev)
- [GitHub repository](https://github.com/destacey/Wayd)
- [Report an issue](https://github.com/destacey/Wayd/issues)
