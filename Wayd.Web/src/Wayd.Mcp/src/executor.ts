import { z, ZodError } from 'zod';
import axios, { type AxiosRequestConfig, type AxiosError } from 'axios';
import { zodSchemas } from './generated/zod-schemas.js';
import type { CallToolResult } from '@modelcontextprotocol/sdk/types.js';
import { API_BASE_URL, WAYD_API_KEY } from './config.js';
import type { McpToolDefinition, JsonObject } from './types.js';

export const securitySchemes = {
  ApiKey: {
    type: 'apiKey',
    description: 'Personal Access Token - Enter your PAT directly without any prefix',
    name: 'x-api-key',
    in: 'header',
  },
} as const;

/**
 * Executes an API tool with the provided arguments.
 */
export async function executeApiTool(
  toolName: string,
  definition: McpToolDefinition,
  toolArgs: JsonObject,
  allSecuritySchemes: Record<string, any>
): Promise<CallToolResult> {
  try {
    // Validate arguments against the input schema
    let validatedArgs: JsonObject;
    try {
      const zodSchema = zodSchemas.get(toolName) ?? z.looseObject({});
      const argsToParse = (typeof toolArgs === 'object' && toolArgs !== null) ? toolArgs : {};
      // zod 4 types parse() as `unknown`; tool arguments are JSON objects by contract.
      validatedArgs = zodSchema.parse(argsToParse) as JsonObject;
    } catch (error: unknown) {
      if (error instanceof ZodError) {
        const msg = `Invalid arguments for tool '${toolName}': ${error.issues.map(e => `${e.path.join('.')} (${e.code}): ${e.message}`).join(', ')}`;
        return { content: [{ type: 'text', text: msg }], isError: true };
      }
      const msg = error instanceof Error ? error.message : String(error);
      return { content: [{ type: 'text', text: `Internal error during validation setup: ${msg}` }], isError: true };
    }

    if (definition.localHandler) {
      return definition.localHandler(validatedArgs);
    }

    // Prepare URL, query parameters, headers, and request body
    let urlPath = definition.pathTemplate;
    if (definition.pathSelector) {
      const key = String(validatedArgs[definition.pathSelector.parameter]);
      const selected = definition.pathSelector.paths[key];
      if (!selected) {
        throw new Error(`'${key}' is not a valid ${definition.pathSelector.parameter}.`);
      }
      urlPath = selected;
    }
    const queryParams: Record<string, any> = {};
    const headers: Record<string, string> = { Accept: 'application/json' };
    let requestBodyData: any = undefined;
    let formData: FormData | undefined = undefined;

    // Apply parameters to the URL path, query, or headers
    definition.executionParameters.forEach((param) => {
      const value = validatedArgs[param.name];
      if (typeof value !== 'undefined' && value !== null) {
        if (param.in === 'path') {
          urlPath = urlPath.replace(`{${param.name}}`, encodeURIComponent(String(value)));
        } else if (param.in === 'query') {
          queryParams[param.name] = value;
        } else if (param.in === 'header') {
          headers[param.name.toLowerCase()] = String(value);
        } else if (param.in === 'formFile') {
          formData ??= new FormData();
          formData.append(param.name, new Blob([String(value)], { type: 'text/csv' }), `${param.name}.csv`);
        }
      }
    });

    // Ensure all path parameters are resolved
    if (urlPath.includes('{')) {
      throw new Error(`Failed to resolve path parameters: ${urlPath}`);
    }

    // Construct the full URL
    const requestUrl = API_BASE_URL ? `${API_BASE_URL}${urlPath}` : urlPath;

    // Applied after the arguments, so a caller can never override one.
    Object.assign(queryParams, definition.fixedQuery);

    // Handle request body if needed
    if (formData) {
      // No content-type header: axios sets multipart/form-data with the boundary the body needs.
      requestBodyData = formData;
    } else if (definition.requestBodyContentType && typeof validatedArgs['requestBody'] !== 'undefined') {
      requestBodyData = validatedArgs['requestBody'];
      headers['content-type'] = definition.requestBodyContentType;
    }

    // Apply API key security if available
    for (const req of (definition.securityRequirements ?? [])) {
      for (const [schemeName] of Object.entries(req)) {
        const scheme = allSecuritySchemes[schemeName];
        if (scheme?.type !== 'apiKey') continue;

        const apiKey = WAYD_API_KEY;
        if (!apiKey) continue;

        if (scheme.in === 'header') {
          headers[scheme.name.toLowerCase()] = apiKey;
          console.error(`Applied API key '${schemeName}' in header '${scheme.name}'`);
        } else if (scheme.in === 'query') {
          queryParams[scheme.name] = apiKey;
          console.error(`Applied API key '${schemeName}' in query parameter '${scheme.name}'`);
        } else if (scheme.in === 'cookie') {
          headers['cookie'] = `${scheme.name}=${apiKey}${headers['cookie'] ? `; ${headers['cookie']}` : ''}`;
          console.error(`Applied API key '${schemeName}' in cookie '${scheme.name}'`);
        }
      }
    }

    // Prepare and execute the request
    const config: AxiosRequestConfig = {
      method: definition.method.toUpperCase(),
      url: requestUrl,
      params: queryParams,
      // ASP.NET binds array query parameters from repeated bare keys (`status=1&status=2`).
      // Axios defaults to bracket syntax (`status[]=1`), which the model binder ignores —
      // silently dropping every array filter. This matches the generated NSwag client.
      paramsSerializer: { indexes: null },
      headers,
      ...(requestBodyData !== undefined && { data: requestBodyData }),
    };

    if (definition.precondition) {
      const refusal = await checkPrecondition(definition, validatedArgs, headers);
      if (refusal) {
        return { content: [{ type: 'text', text: refusal }], isError: true };
      }
    }

    console.error(`Executing tool "${toolName}": ${config.method} ${config.url}`);
    const response = await axios(config);

    // Format the response
    let responseText = '';
    const rawContentType = response.headers['content-type'];
    const contentType = typeof rawContentType === 'string' ? rawContentType.toLowerCase() : '';

    if (contentType.includes('application/json') && typeof response.data === 'object' && response.data !== null) {
      try { responseText = JSON.stringify(response.data, null, 2); } catch { responseText = '[Stringify Error]'; }
    } else if (typeof response.data === 'string') {
      responseText = response.data;
    } else if (response.data !== undefined && response.data !== null) {
      responseText = String(response.data);
    }
    // empty body on success (e.g. 204) — return nothing, the model doesn't need noise

    return {
      content: [{ type: 'text', text: responseText }],
    };

  } catch (error: unknown) {
    let errorMessage: string;
    if (axios.isAxiosError(error)) {
      errorMessage = formatApiError(error);
    } else if (error instanceof Error) {
      errorMessage = error.message;
    } else {
      errorMessage = 'Unexpected error: ' + String(error);
    }

    console.error(`Error during execution of tool '${toolName}':`, errorMessage);
    return { content: [{ type: 'text', text: errorMessage }], isError: true };
  }
}

/**
 * Sends a definition's precondition GET and returns the refusal message, if any. A 404 is handed to the
 * check as no answer, since an endpoint the API lacks is usually what a precondition exists to detect.
 */
async function checkPrecondition(
  definition: McpToolDefinition,
  args: JsonObject,
  headers: Record<string, string>
): Promise<string | undefined> {
  const { path, refusal } = definition.precondition!;
  const getHeaders = Object.fromEntries(Object.entries(headers).filter(([name]) => name !== 'content-type'));
  const url = API_BASE_URL ? `${API_BASE_URL}${path}` : path;

  console.error(`Checking precondition for tool "${definition.name}": GET ${url}`);
  try {
    const response = await axios({ method: 'GET', url, headers: getHeaders });
    return refusal(response.data, args);
  } catch (error: unknown) {
    if (axios.isAxiosError(error) && error.response?.status === 404) {
      return refusal(undefined, args);
    }
    throw error;
  }
}

/**
 * The readable parts of an RFC 7807 problem details body, or undefined for any other body.
 *
 * Kept whole rather than cut at the generic length: the refusal reason and the per-field
 * validation errors are what a caller needs to correct the request, and the API puts them
 * after the boilerplate title that a short cut would keep instead.
 */
function describeProblem(data: unknown): string | undefined {
  if (typeof data !== 'object' || data === null) return undefined;
  const { title, detail, errors } = data as { title?: unknown; detail?: unknown; errors?: unknown };
  if (typeof title !== 'string' && typeof detail !== 'string' && (typeof errors !== 'object' || errors === null)) {
    return undefined;
  }

  const parts: string[] = [];
  if (typeof title === 'string') parts.push(title);
  if (typeof detail === 'string' && detail !== 'See the errors property for details.') parts.push(detail);
  if (typeof errors === 'object' && errors !== null) {
    for (const [field, messages] of Object.entries(errors)) {
      const text = Array.isArray(messages) ? messages.join(' ') : String(messages);
      parts.push(field ? `${field}: ${text}` : text);
    }
  }

  const MAX_LEN = 4000;
  const joined = parts.join('\n');
  return joined.length > MAX_LEN ? `${joined.substring(0, MAX_LEN)}...` : joined;
}

/**
 * Formats Axios errors for better readability.
 */
export function formatApiError(error: AxiosError): string {
  let message = 'API request failed.';
  if (error.response) {
    message = `API Error: Status ${error.response.status} (${error.response.statusText || 'Status text not available'}). `;
    const responseData = error.response.data;
    const MAX_LEN = 200;
    if (typeof responseData === 'string') {
      message += `Response: ${responseData.substring(0, MAX_LEN)}${responseData.length > MAX_LEN ? '...' : ''}`;
    } else if (responseData) {
      const problem = describeProblem(responseData);
      if (problem) {
        message += problem;
      } else {
        try {
          const jsonString = JSON.stringify(responseData);
          message += `Response: ${jsonString.substring(0, MAX_LEN)}${jsonString.length > MAX_LEN ? '...' : ''}`;
        } catch {
          message += 'Response: [Could not serialize data]';
        }
      }
    } else {
      message += 'No response body received.';
    }
  } else if (error.request) {
    message = 'API Network Error: No response received from server.';
    if (error.code) message += ` (Code: ${error.code})`;
  } else {
    message += `API Request Setup Error: ${error.message}`;
  }
  return message;
}
