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
import { annotationsFor } from './annotations.js';
import { SERVER_INSTRUCTIONS } from './instructions.js';
import { retirementNotice } from './retirement.js';

const RETIREMENT_NOTICE = retirementNotice(API_BASE_URL);

const server = new Server(
  { name: SERVER_NAME, version: SERVER_VERSION },
  { capabilities: { tools: {} }, instructions: `${RETIREMENT_NOTICE}

${SERVER_INSTRUCTIONS}` }
);

server.setRequestHandler(ListToolsRequestSchema, async () => {
  const toolsForClient: Tool[] = Array.from(toolDefinitionMap.values()).map(def => ({
    name: def.name,
    description: def.description,
    inputSchema: def.inputSchema,
    annotations: annotationsFor(def),
  }));
  return { tools: toolsForClient };
});

// Some clients never show the server instructions, so the first result of a session carries the notice too.
let noticeSent = false;
function withRetirementNotice(result: CallToolResult): CallToolResult {
  if (noticeSent) return result;
  noticeSent = true;
  return { ...result, content: [...result.content, { type: 'text', text: RETIREMENT_NOTICE }] };
}

server.setRequestHandler(CallToolRequestSchema, async (request: CallToolRequest): Promise<CallToolResult> => {
  const { name: toolName, arguments: toolArgs } = request.params;
  const toolDefinition = toolDefinitionMap.get(toolName);
  if (!toolDefinition) {
    console.error(`Error: Unknown tool requested: ${toolName}`);
    return withRetirementNotice({ content: [{ type: 'text', text: `Error: Unknown tool requested: ${toolName}` }], isError: true });
  }
  return withRetirementNotice(await executeApiTool(toolName, toolDefinition, toolArgs ?? {}, securitySchemes));
});

async function main() {
  if (!API_BASE_URL) {
    console.error('Error: WAYD_API_BASE_URL environment variable or --base-url argument is required.');
    console.error(`Warning: ${RETIREMENT_NOTICE}`);
    process.exit(1);
  }

  try {
    const transport = new StdioServerTransport();
    await server.connect(transport);
    console.error(`${SERVER_NAME} MCP Server (v${SERVER_VERSION}) running on stdio${API_BASE_URL ? `, proxying API at ${API_BASE_URL}` : ''}`);
    console.error(`Warning: ${RETIREMENT_NOTICE}`);
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
