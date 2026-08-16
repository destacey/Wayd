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

Six self-contained skills are available:

| Skill | Trigger |
| --- | --- |
| `wayd-ppm` | Portfolios, programs, projects — lookup, plans, health checks, task management |
| `wayd-pi` | Planning intervals, iterations, objectives, health reports, risks |
| `wayd-roadmaps` | Roadmap exploration — activities, timeboxes, milestones |
| `wayd-story-maps` | Story maps — analyze, create, and manage goals, steps, tasks, swim lanes, personas |
| `wayd-teams` | Team lookup — resolve a team name to an ID |
| `wayd-users` | User lookup — resolve a user name to a UUID for assignees and project roles |

### Installing the skills

From your project root:

```bash
npx skills add destacey/Wayd
```

Once installed, activate a skill in Claude Code with `/wayd-ppm`, `/wayd-pi`, `/wayd-roadmaps`, `/wayd-story-maps`, `/wayd-teams`, or `/wayd-users`.

## Available Tools

### Project Portfolio Management

| Category | Operations |
| --- | --- |
| **Portfolios** | List, get details, get programs, get projects, get strategic initiatives, get ranking scoreboard |
| **Strategic Initiatives** | List, get details, get statuses, get linked projects. KPIs: list, get details, get checkpoints, get checkpoint plan, list measurements, add measurement, remove measurement |
| **Programs** | List, get details, get projects |
| **Project Lifecycles** | List (with state filter), get details |
| **Projects** | List (with role filter), get details, get status history, get my involvement summary, get my task metrics, get team, get phases, get phase details, get plan tree, get plan summary (single and batch), list health checks, get health check, create health check, get scoring context, list scores, get score |
| **Tasks** | List, get details, get critical path, get types/statuses/priorities, create, update, delete, add/remove dependencies |

### Planning

| Category | Operations |
| --- | --- |
| **Planning Intervals** | List, get details, calendar, predictability, teams, iterations, objectives, risks, objective health check history, get/create objective health check |
| **Roadmaps** | List, get details, get items and activities |
| **Story Maps** | List, get full map. Create, update, archive, delete maps. Manage goals, steps, tasks, checklists, swim lanes, personas, and work item links |

### Organization

| Category | Operations |
| --- | --- |
| **Teams** | List, get details |
| **Users** | List, get details |

## Links

- [Wayd documentation](https://wayd.dev)
- [GitHub repository](https://github.com/destacey/Wayd)
- [Report an issue](https://github.com/destacey/Wayd/issues)
