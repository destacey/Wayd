import type { Mock } from 'vitest'
import { act, fireEvent, render, screen } from '@testing-library/react'
import GlobalSearchModal from './global-search-modal'
import { useLazyGlobalSearchQuery } from '@/src/store/features/search/search-api'

const mockPush = vi.fn()
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}))

vi.mock('@/src/store/features/search/search-api', () => ({
  useLazyGlobalSearchQuery: vi.fn(),
}))

vi.mock('@ant-design/icons', () => ({
  SearchOutlined: () => null,
}))

// Mock antd to avoid jsdom animation/state issues with React 19 + @testing-library/react 16
vi.mock('antd', () => {
  // eslint-disable-next-line @typescript-eslint/no-require-imports
  const React = require('react')
  // eslint-disable-next-line react/display-name
  const MockInput = React.forwardRef(
    ({ placeholder, value, onChange }: any, ref: any) =>
      React.createElement('input', { ref, placeholder, value, onChange }),
  )
  return {
    Modal: ({ open, children }: any) =>
      open
        ? React.createElement('div', { 'data-testid': 'modal' }, children)
        : null,
    Input: MockInput,
    Spin: ({ children }: any) =>
      React.createElement('div', { className: 'ant-spin' }, children),
    Tabs: ({ activeKey, onChange, items }: any) =>
      React.createElement(
        'div',
        null,
        (items ?? []).map((item: any) =>
          React.createElement(
            'button',
            {
              key: item.key,
              role: 'tab',
              'aria-selected': activeKey === item.key,
              onClick: () => onChange?.(item.key),
            },
            item.label,
          ),
        ),
      ),
    Typography: {
      Title: ({ children, level, type, className }: any) =>
        React.createElement(`h${level ?? 1}`, { className }, children),
      Text: ({ children, type, className, ellipsis, code }: any) =>
        code
          ? React.createElement('code', { className }, children)
          : React.createElement('span', { className }, children),
    },
    Flex: ({ children, vertical, justify, align, gap, wrap, ...rest }: any) =>
      React.createElement('div', rest, children),
    Empty: ({ description }: any) =>
      React.createElement('div', null, description),
    Divider: () => React.createElement('hr', null),
    Segmented: ({ value, onChange, options }: any) =>
      React.createElement(
        'div',
        { 'data-testid': 'scope-toggle' },
        (options ?? []).map((opt: any) =>
          React.createElement(
            'button',
            {
              key: typeof opt === 'string' ? opt : opt.value,
              'aria-pressed':
                value === (typeof opt === 'string' ? opt : opt.value),
              onClick: () =>
                onChange?.(typeof opt === 'string' ? opt : opt.value),
            },
            typeof opt === 'string' ? opt : opt.label,
          ),
        ),
      ),
    Radio: {
      Group: ({ value, onChange, options, className }: any) =>
        React.createElement(
          'div',
          { className },
          (options ?? []).map((opt: any) =>
            React.createElement('input', {
              key: opt.value,
              type: 'radio',
              role: 'radio',
              'aria-label': String(opt.label),
              checked: value === opt.value,
              onChange: () => onChange?.({ target: { value: opt.value } }),
            }),
          ),
        ),
    },
  }
})

const mockTriggerSearch = vi.fn()

const mockCategories = [
  {
    name: 'Work Items',
    slug: 'work-items',
    totalCount: 2,
    items: [
      {
        title: 'Fix the login bug',
        key: 'WAYD-100',
        entityType: 'WorkItem',
        auxKey: 'ws-1',
      },
      {
        title: 'Add dark mode',
        key: 'WAYD-101',
        entityType: 'WorkItem',
        auxKey: 'ws-1',
        subtitle: 'My Workspace',
      },
    ],
  },
  {
    name: 'Teams',
    slug: 'teams',
    totalCount: 1,
    items: [
      {
        title: 'Nova Team',
        key: 'NOVA',
        entityType: 'Team',
        auxKey: 'team-uuid',
      },
    ],
  },
]

function setupMock(
  overrides: Partial<ReturnType<typeof useLazyGlobalSearchQuery>[1]> = {},
) {
  ;(useLazyGlobalSearchQuery as Mock).mockReturnValue([
    mockTriggerSearch,
    { data: undefined, isFetching: false, ...overrides },
  ])
}

// jsdom doesn't implement scrollIntoView
Element.prototype.scrollIntoView = vi.fn()

describe('GlobalSearchModal', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    vi.useFakeTimers()
    setupMock()
  })

  afterEach(() => {
    vi.runOnlyPendingTimers()
    vi.useRealTimers()
  })

  describe('Initial state', () => {
    it('shows empty state hint when open with no search', async () => {
      render(<GlobalSearchModal open={true} onClose={vi.fn()} />)
      await act(async () => {
        vi.runAllTimers()
      })
      expect(
        screen.getByText('Type at least 2 characters to search'),
      ).toBeInTheDocument()
    })

    it('does not render when closed', async () => {
      const { container } = render(
        <GlobalSearchModal open={false} onClose={vi.fn()} />,
      )
      await act(async () => {
        vi.runAllTimers()
      })
      expect(container).toBeEmptyDOMElement()
    })
  })

  describe('Search input', () => {
    it('does not trigger search for single character', async () => {
      render(<GlobalSearchModal open={true} onClose={vi.fn()} />)
      await act(async () => {
        fireEvent.change(screen.getByPlaceholderText('Search Wayd...'), {
          target: { value: 'a' },
        })
        vi.advanceTimersByTime(300)
      })
      expect(mockTriggerSearch).not.toHaveBeenCalled()
    })

    it('triggers search after 2+ characters with debounce', async () => {
      render(<GlobalSearchModal open={true} onClose={vi.fn()} />)
      await act(async () => {
        fireEvent.change(screen.getByPlaceholderText('Search Wayd...'), {
          target: { value: 'te' },
        })
        vi.advanceTimersByTime(300)
      })
      expect(mockTriggerSearch).toHaveBeenCalledWith({
        query: 'te',
        maxResultsPerCategory: 5,
      })
    })
  })

  describe('Loading state', () => {
    it('shows spinner on initial fetch (no data yet)', async () => {
      setupMock({ isFetching: true, data: undefined })
      render(<GlobalSearchModal open={true} onClose={vi.fn()} />)
      await act(async () => {
        vi.runAllTimers()
      })
      expect(document.querySelector('.ant-spin')).toBeInTheDocument()
    })

    it('does not show spinner when re-fetching with existing data', async () => {
      setupMock({ isFetching: true, data: { categories: mockCategories } })
      render(<GlobalSearchModal open={true} onClose={vi.fn()} />)
      await act(async () => {
        vi.runAllTimers()
      })
      expect(document.querySelector('.ant-spin')).not.toBeInTheDocument()
    })
  })

  describe('Results', () => {
    async function renderWithResults(onClose = vi.fn()) {
      setupMock({ data: { categories: mockCategories } })
      render(<GlobalSearchModal open={true} onClose={onClose} />)
      await act(async () => {
        vi.runAllTimers()
      }) // flush autofocus timer
      await act(async () => {
        fireEvent.change(screen.getByPlaceholderText('Search Wayd...'), {
          target: { value: 'te' },
        })
        vi.runAllTimers() // flush debounce
      })
      return { onClose }
    }

    it('renders category headers in All tab', async () => {
      await renderWithResults()
      expect(screen.getByText('Work Items')).toBeInTheDocument()
      expect(screen.getByText('Teams')).toBeInTheDocument()
    })

    it('renders result items with key and title', async () => {
      await renderWithResults()
      expect(screen.getByText('Fix the login bug')).toBeInTheDocument()
      expect(screen.getByText('WAYD-100')).toBeInTheDocument()
    })

    it('renders subtitle when present', async () => {
      await renderWithResults()
      expect(screen.getByText('My Workspace')).toBeInTheDocument()
    })

    it('renders tabs for each category with results', async () => {
      await renderWithResults()
      expect(screen.getByRole('tab', { name: /All/ })).toBeInTheDocument()
      expect(
        screen.getByRole('tab', { name: /Work Items/ }),
      ).toBeInTheDocument()
      expect(screen.getByRole('tab', { name: /Teams/ })).toBeInTheDocument()
    })

    it('shows total count in All tab label', async () => {
      await renderWithResults()
      expect(screen.getByRole('tab', { name: 'All (3)' })).toBeInTheDocument()
    })

    it('shows no results empty state', async () => {
      setupMock({ data: { categories: [] } })
      render(<GlobalSearchModal open={true} onClose={vi.fn()} />)
      await act(async () => {
        fireEvent.change(screen.getByPlaceholderText('Search Wayd...'), {
          target: { value: 'te' },
        })
        vi.runAllTimers()
      })
      expect(screen.getByText(/No results found for/)).toBeInTheDocument()
    })
  })

  describe('Navigation', () => {
    it('calls onClose when Escape is pressed', async () => {
      const onClose = vi.fn()
      render(<GlobalSearchModal open={true} onClose={onClose} />)
      await act(async () => {
        vi.runAllTimers()
      })
      fireEvent.keyDown(screen.getByPlaceholderText('Search Wayd...'), {
        key: 'Escape',
      })
      expect(onClose).toHaveBeenCalled()
    })

    it('navigates to result on Enter', async () => {
      mockPush.mockClear()
      setupMock({ data: { categories: mockCategories } })
      const onClose = vi.fn()
      render(<GlobalSearchModal open={true} onClose={onClose} />)
      await act(async () => {
        vi.runAllTimers()
      })
      await act(async () => {
        fireEvent.change(screen.getByPlaceholderText('Search Wayd...'), {
          target: { value: 'te' },
        })
        vi.runAllTimers()
      })

      // Select first item with ArrowDown, then press Enter
      await act(async () => {
        fireEvent.keyDown(screen.getByPlaceholderText('Search Wayd...'), {
          key: 'ArrowDown',
        })
      })
      await act(async () => {
        fireEvent.keyDown(screen.getByPlaceholderText('Search Wayd...'), {
          key: 'Enter',
        })
      })
      expect(mockPush).toHaveBeenCalled()
      expect(onClose).toHaveBeenCalled()
    })
  })

  describe('Footer', () => {
    it('shows keyboard shortcut hints', () => {
      render(<GlobalSearchModal open={true} onClose={vi.fn()} />)
      expect(screen.getByText('navigate')).toBeInTheDocument()
      expect(screen.getByText('select')).toBeInTheDocument()
      expect(screen.getByText('close')).toBeInTheDocument()
    })

    it('shows Per Category radio group', () => {
      render(<GlobalSearchModal open={true} onClose={vi.fn()} />)
      expect(screen.getByText('Per Category:')).toBeInTheDocument()
      expect(screen.getByRole('radio', { name: '5' })).toBeInTheDocument()
      expect(screen.getByRole('radio', { name: '10' })).toBeInTheDocument()
      expect(screen.getByRole('radio', { name: '20' })).toBeInTheDocument()
    })
  })
})
