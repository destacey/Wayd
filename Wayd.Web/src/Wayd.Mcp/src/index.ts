#!/usr/bin/env node
import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
  type Tool,
  type CallToolResult,
  type CallToolRequest,
} from '@modelcontextprotocol/sdk/types.js';

import { SERVER_NAME, SERVER_VERSION, API_BASE_URL } from './config.js';
import { toolDefinitionMap } from './tools/index.js';
import { executeApiTool, securitySchemes } from './executor.js';

const server = new Server(
  { name: SERVER_NAME, version: SERVER_VERSION },
  { capabilities: { tools: {} } }
);

server.setRequestHandler(ListToolsRequestSchema, async () => {
  const toolsForClient: Tool[] = Array.from(toolDefinitionMap.values()).map(def => ({
    name: def.name,
    description: def.description,
    inputSchema: def.inputSchema,
    // A tool without explicit annotations is derived from its HTTP method: GET
    // only ever reads. Anything that writes must opt in deliberately, so a new
    // write tool is never silently advertised as safe.
    annotations: def.annotations ?? (def.method.toLowerCase() === 'get' ? { readOnlyHint: true } : undefined),
  }));
  return { tools: toolsForClient };
});

server.setRequestHandler(CallToolRequestSchema, async (request: CallToolRequest): Promise<CallToolResult> => {
  const { name: toolName, arguments: toolArgs } = request.params;
  const toolDefinition = toolDefinitionMap.get(toolName);
  if (!toolDefinition) {
    console.error(`Error: Unknown tool requested: ${toolName}`);
    return { content: [{ type: 'text', text: `Error: Unknown tool requested: ${toolName}` }], isError: true };
  }
  return await executeApiTool(toolName, toolDefinition, toolArgs ?? {}, securitySchemes);
});

async function main() {
  if (!API_BASE_URL) {
    console.error('Error: WAYD_API_BASE_URL environment variable or --base-url argument is required.');
    process.exit(1);
  }

  try {
    const transport = new StdioServerTransport();
    await server.connect(transport);
    console.error(`${SERVER_NAME} MCP Server (v${SERVER_VERSION}) running on stdio${API_BASE_URL ? `, proxying API at ${API_BASE_URL}` : ''}`);
  } catch (error) {
    console.error('Error during server startup:', error);
    process.exit(1);
  }
}

async function cleanup() {
  console.error('Shutting down MCP server...');
  process.exit(0);
}

process.on('SIGINT', cleanup);
process.on('SIGTERM', cleanup);

main().catch((error) => {
  console.error('Fatal error in main execution:', error);
  process.exit(1);
});
