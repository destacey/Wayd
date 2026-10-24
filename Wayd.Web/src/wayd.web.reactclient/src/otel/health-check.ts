const HEALTH_PATH = '/api/health'

/**
 * The path a span was started for. Next names the request span with only the
 * method and puts the URL on `http.target`; the route is applied later, after
 * sampling.
 */
function pathOf(value: string): string {
  const withoutQuery = value.split('?')[0]
  const space = withoutQuery.lastIndexOf(' ')
  return space === -1 ? withoutQuery : withoutQuery.slice(space + 1)
}

function isHealthPath(path: string): boolean {
  return path === HEALTH_PATH || path === `${HEALTH_PATH}/`
}

export function isHealthCheckSpan(
  spanName: string,
  attributes: Record<string, unknown> | undefined,
): boolean {
  const candidates = [
    spanName,
    attributes?.['http.target'],
    attributes?.['http.route'],
    attributes?.['next.route'],
    attributes?.['url.path'],
  ]
  return candidates.some(
    (value) => typeof value === 'string' && isHealthPath(pathOf(value)),
  )
}
