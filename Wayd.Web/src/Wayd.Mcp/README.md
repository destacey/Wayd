# @wayd/mcp (retired)

**This package is retired and receives no further updates.** The Wayd API serves MCP itself, at `/mcp` on your Wayd API's address. It offers every tool this package had, under the same names, so your skills and prompts keep working, and many more: work items and sprints, employees, team capacity, risks, intake requests, partial updates and more. It needs no Node.js, and you can sign in with your Wayd account instead of storing a token.

Full setup, including choosing toolsets and read-only mode: [MCP Server](https://wayd.dev/docs/getting-started/mcp-server).

## Moving to the hosted endpoint

An administrator turns the endpoint on with the **MCP Server** feature flag (**Settings > System > Feature Flags**). Then replace the `wayd` server in your MCP configuration. `https://your-wayd-api.com` is the address you passed as `WAYD_API_BASE_URL` or `--base-url`.

### Sign in (no token)

Claude Code:

```bash
claude mcp remove wayd
claude mcp add --transport http wayd https://your-wayd-api.com/mcp
```

Then run `/mcp` in a session, choose **wayd**, and choose **Authenticate**. In `.mcp.json` or `.vscode/mcp.json`, replace the `command`/`args`/`env` entry with:

```json
{
  "type": "http",
  "url": "https://your-wayd-api.com/mcp"
}
```

claude.ai, Claude Desktop, ChatGPT and other hosted assistants: add a custom connector with the `/mcp` address.

### Personal access token

Keep your existing token and send it in the `x-api-key` header:

```json
{
  "type": "http",
  "url": "https://your-wayd-api.com/mcp",
  "headers": {
    "x-api-key": "your-personal-access-token"
  }
}
```

### Clients that only run local servers

A client that can only start a local (stdio) server can reach the endpoint through [`mcp-remote`](https://www.npmjs.com/package/mcp-remote):

```json
{
  "mcpServers": {
    "wayd": {
      "command": "npx",
      "args": ["-y", "mcp-remote", "https://your-wayd-api.com/mcp", "--header", "x-api-key:${WAYD_API_KEY}"],
      "env": {
        "WAYD_API_KEY": "your-personal-access-token"
      }
    }
  }
}
```

## Until you move

The last version still works against the Wayd API it was built for, and tells the assistant and your client's log where to move. It will not track API changes, so tools can fail as the API moves on.

## Links

- [MCP Server documentation](https://wayd.dev/docs/getting-started/mcp-server)
- [GitHub repository](https://github.com/destacey/Wayd)
- [Report an issue](https://github.com/destacey/Wayd/issues)
