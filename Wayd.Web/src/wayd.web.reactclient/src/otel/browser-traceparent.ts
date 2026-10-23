export interface BrowserTrace {
  traceparent: string
  tracestate?: string
}

// The latest same-origin response wins over the document meta tag. A client
// navigation is a fetch, and that response's span is the page the API call
// belongs to; the meta tag is only the document that was first loaded.
let captured: BrowserTrace | undefined

export function resetBrowserTraceCapture(): void {
  captured = undefined
}

export function rememberSameOriginTrace(
  requestUrl: string,
  traceresponse: string | null,
  pageOrigin: string,
): void {
  if (!traceresponse) return
  let origin: string
  try {
    origin = new URL(requestUrl, pageOrigin).origin
  } catch {
    return
  }
  if (origin !== pageOrigin) return
  captured = { traceparent: traceresponse }
}

export function browserTrace(meta: {
  traceparent: string | null
  tracestate: string | null
}): BrowserTrace | undefined {
  if (captured) return captured
  if (!meta.traceparent) return undefined
  return {
    traceparent: meta.traceparent,
    tracestate: meta.tracestate ?? undefined,
  }
}

export function installBrowserTraceCapture(): void {
  if (typeof window === 'undefined') return
  const host = window as Window & { __waydTraceCapture?: boolean }
  if (host.__waydTraceCapture) return
  host.__waydTraceCapture = true

  const original = window.fetch.bind(window)
  window.fetch = async (input, init) => {
    const response = await original(input, init)
    const url =
      typeof input === 'string'
        ? input
        : input instanceof URL
          ? input.href
          : input.url
    rememberSameOriginTrace(
      url,
      response.headers.get('traceresponse'),
      window.location.origin,
    )
    return response
  }
}

export function currentBrowserTrace(): BrowserTrace | undefined {
  if (typeof document === 'undefined') return captured
  return browserTrace({
    traceparent: document
      .querySelector('meta[name="traceparent"]')
      ?.getAttribute('content') ?? null,
    tracestate: document
      .querySelector('meta[name="tracestate"]')
      ?.getAttribute('content') ?? null,
  })
}
