// Runs before every suite in both environments; the browser shims load only where there is a DOM.
import '@testing-library/jest-dom/vitest'

if (typeof window !== 'undefined') {
  await import('./vitest.setup.dom')
}

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: vi.fn(),
    replace: vi.fn(),
    prefetch: vi.fn(),
  }),
  usePathname: vi.fn(() => '/'),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}))

// Mock the useTheme hook globally
vi.mock('./components/contexts/theme', () => ({
  default: () => ({
    token: {
      colorPrimary: '#1890ff',
      colorWarning: '#faad14',
      colorSuccess: '#52c41a',
      colorError: '#ff4d4f',
      colorInfo: '#1890ff',
    },
  }),
}))

afterEach(() => {
  vi.clearAllMocks()
  // A test that fails before its own `vi.useRealTimers()` would otherwise leave the rest of the
  // file on fake timers, where every later wait hangs.
  vi.useRealTimers()
})
