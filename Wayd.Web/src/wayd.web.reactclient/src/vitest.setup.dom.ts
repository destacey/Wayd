import crypto from 'crypto'

// Mock MessageChannel for Ant Design v6 components
global.MessageChannel = class MessageChannel {
  port1: MessagePort
  port2: MessagePort

  constructor() {
    this.port1 = {
      postMessage: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      start: vi.fn(),
      close: vi.fn(),
      onmessage: null,
      onmessageerror: null,
      dispatchEvent: vi.fn(() => false),
    } as unknown as MessagePort

    this.port2 = {
      postMessage: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      start: vi.fn(),
      close: vi.fn(),
      onmessage: null,
      onmessageerror: null,
      dispatchEvent: vi.fn(() => false),
    } as unknown as MessagePort
  }
}

// antd's motion library animates only where the browser reports transition or animation support,
// then waits for an end event that jsdom never fires: a loading spinner or closing popover stays
// mounted for the rest of the test. Hiding the support it checks for (TransitionEvent, and the
// vendor-prefixed style properties) skips motion, so elements leave at once as the suites expect.
delete (window as { TransitionEvent?: unknown }).TransitionEvent
const stylePrototype = Object.getPrototypeOf(
  document.createElement('div').style,
) as Record<string, unknown>
for (const property of ['WebkitTransition', 'WebkitAnimation']) {
  delete stylePrototype[property]
}

// Mock BroadcastChannel to prevent ReferenceError in jsdom
global.BroadcastChannel = class BroadcastChannel {
  name: string
  onmessage: ((this: BroadcastChannel, ev: MessageEvent) => any) | null = null
  onmessageerror: ((this: BroadcastChannel, ev: MessageEvent) => any) | null =
    null

  constructor(name: string) {
    this.name = name
  }

  postMessage(message: any) {}

  close() {}

  addEventListener(
    type: string,
    listener: EventListenerOrEventListenerObject,
  ) {}

  removeEventListener(
    type: string,
    listener: EventListenerOrEventListenerObject,
  ) {}

  dispatchEvent(event: Event): boolean {
    return false
  }
}

Object.assign(navigator, {
  clipboard: {
    writeText: vi.fn(),
  },
})

Object.defineProperty(window, 'crypto', {
  value: {
    getRandomValues: (arr: Uint8Array) => {
      const bytes = crypto.randomBytes(arr.length)
      arr.set(bytes)
      return arr
    },
    subtle: {
      digest: async (algorithm: string, data: ArrayBuffer) => {
        const hash = crypto.createHash(algorithm.toLowerCase().replace('-', ''))
        hash.update(Buffer.from(data))
        return hash.digest()
      },
      importKey: async () => ({}),
      derive: async () => new Uint8Array(32),
      encrypt: async () => new Uint8Array(32),
      decrypt: async () => new Uint8Array(32),
    },
  },
})

Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation((query) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(), // deprecated
    removeListener: vi.fn(), // deprecated
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
})

// jsdom lacks ResizeObserver and Element.scrollIntoView, which Ant Design's
// popup/dropdown positioning (e.g. an open Select) relies on. Stub them so
// components that render an open dropdown don't throw during tests.
if (typeof window.ResizeObserver === 'undefined') {
  window.ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  } as unknown as typeof ResizeObserver
}

if (!Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = vi.fn()
}

// jsdom has no layout, so WaydGrid's scrolling body viewport measures 0×0 and
// the row virtualizer would render zero rows (TanStack virtual bails on a
// 0-height scroll rect; it reads clientWidth/clientHeight). Report a fixed
// 800×600 viewport for that one element (marked data-grid-body-viewport) so
// grids render a deterministic window: 600px ÷ 28px estimated rows = indexes
// 0-21 visible, plus 10 overscan.
const GRID_VIEWPORT_MARKER = 'data-grid-body-viewport'
const GRID_VIEWPORT_WIDTH = 800
const GRID_VIEWPORT_HEIGHT = 600

const mockViewportDimension = (
  property: 'offsetWidth' | 'offsetHeight' | 'clientWidth' | 'clientHeight',
  value: number,
) => {
  const original = Object.getOwnPropertyDescriptor(
    HTMLElement.prototype,
    property,
  )
  Object.defineProperty(HTMLElement.prototype, property, {
    configurable: true,
    get(this: HTMLElement) {
      if (this.hasAttribute?.(GRID_VIEWPORT_MARKER)) return value
      return original?.get?.call(this) ?? 0
    },
  })
}

mockViewportDimension('offsetWidth', GRID_VIEWPORT_WIDTH)
mockViewportDimension('offsetHeight', GRID_VIEWPORT_HEIGHT)
mockViewportDimension('clientWidth', GRID_VIEWPORT_WIDTH)
mockViewportDimension('clientHeight', GRID_VIEWPORT_HEIGHT)

const originalGetBoundingClientRect = Element.prototype.getBoundingClientRect
Element.prototype.getBoundingClientRect = function (this: Element) {
  if (this.hasAttribute?.(GRID_VIEWPORT_MARKER)) {
    return {
      x: 0,
      y: 0,
      top: 0,
      left: 0,
      right: GRID_VIEWPORT_WIDTH,
      bottom: GRID_VIEWPORT_HEIGHT,
      width: GRID_VIEWPORT_WIDTH,
      height: GRID_VIEWPORT_HEIGHT,
      toJSON: () => ({}),
    } as DOMRect
  }
  return originalGetBoundingClientRect.call(this)
}

// jsdom's storage is real and outlives a test, so each test starts with it empty.
afterEach(() => {
  localStorage.clear()
  sessionStorage.clear()
})
