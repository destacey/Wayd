import {
  browserTrace,
  installBrowserTraceCapture,
  rememberSameOriginTrace,
  resetBrowserTraceCapture,
} from './browser-traceparent'

const pageOrigin = 'https://wayd.example'

describe('browserTrace', () => {
  beforeEach(() => {
    resetBrowserTraceCapture()
  })

  it('uses the document traceparent when no navigation has been captured', () => {
    // Arrange
    const meta = {
      traceparent: '00-document-span-01',
      tracestate: 'dd=1',
    }

    // Act
    const trace = browserTrace(meta)

    // Assert
    expect(trace).toEqual({
      traceparent: '00-document-span-01',
      tracestate: 'dd=1',
    })
  })

  it('prefers the trace from the latest same-origin response', () => {
    // Arrange
    rememberSameOriginTrace(
      '/organizations/team-of-teams/1?_rsc=abc',
      '00-navigation-span-01',
      pageOrigin,
    )

    // Act
    const trace = browserTrace({
      traceparent: '00-document-span-01',
      tracestate: null,
    })

    // Assert
    expect(trace).toEqual({ traceparent: '00-navigation-span-01' })
  })

  it('ignores a traceresponse from another origin', () => {
    // Arrange
    rememberSameOriginTrace(
      'https://wayd-api.example/api/organization/teams',
      '00-api-span-01',
      pageOrigin,
    )

    // Act
    const trace = browserTrace({
      traceparent: '00-document-span-01',
      tracestate: null,
    })

    // Assert
    expect(trace?.traceparent).toBe('00-document-span-01')
  })
})

describe('installBrowserTraceCapture', () => {
  const originalFetch = window.fetch

  beforeEach(() => {
    resetBrowserTraceCapture()
    window.fetch = originalFetch
    delete (window as Window & { __waydTraceCapture?: boolean }).__waydTraceCapture
  })

  afterEach(() => {
    window.fetch = originalFetch
  })

  it('records traceresponse from a same-origin fetch', async () => {
    // Arrange
    window.fetch = jest.fn().mockResolvedValue({
      headers: {
        get: (name: string) =>
          name === 'traceresponse' ? '00-fetched-span-01' : null,
      },
    })
    installBrowserTraceCapture()

    // Act
    await window.fetch('/organizations/teams')
    const trace = browserTrace({ traceparent: null, tracestate: null })

    // Assert
    expect(trace).toEqual({ traceparent: '00-fetched-span-01' })
  })
})
