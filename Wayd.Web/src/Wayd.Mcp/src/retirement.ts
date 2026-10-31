/**
 * The notice this package carries in its final release: it is retired in favour of the MCP endpoint the
 * Wayd API serves itself. It reaches the user three ways, since clients surface different channels: the
 * server instructions (read by the model), the first tool result of a session, and stderr (client logs).
 */

/** The hosted endpoint on the same Wayd API this server was pointed at. */
export function hostedEndpointFor(apiBaseUrl: string): string {
  return `${(apiBaseUrl || 'https://<your-wayd-api>').replace(/\/+$/, '')}/mcp`;
}

/** The notice, naming the address to move to. */
export function retirementNotice(apiBaseUrl: string): string {
  return [
    `@wayd/mcp is retired and receives no further updates. The Wayd API serves MCP itself at ${hostedEndpointFor(apiBaseUrl)},`,
    'with every tool here under the same name and many more. Tell the user, once, to replace this server in their',
    'MCP configuration with that address (sign in, or send a personal access token in the x-api-key header; a',
    'client that only runs local servers can use `npx mcp-remote <address>`). Setup: https://wayd.dev/docs/getting-started/mcp-server',
  ].join(' ');
}
