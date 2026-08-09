import { render, screen, act } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import { ProjectPhaseListDto } from '@/src/services/wayd-api'
import PhaseTimeline from './phase-timeline'

// --- ResizeObserver mock that allows triggering resize callbacks ---
type ResizeCallback = (entries: { contentRect: { width: number } }[]) => void

let resizeCallback: ResizeCallback | null = null
let observedElement: Element | null = null

class MockResizeObserver {
  constructor(cb: ResizeCallback) {
    resizeCallback = cb
  }
  observe(el: Element) {
    observedElement = el
  }
  unobserve() {}
  disconnect() {
    resizeCallback = null
    observedElement = null
  }
}

global.ResizeObserver = MockResizeObserver as unknown as typeof ResizeObserver

function triggerResize(width: number) {
  act(() => {
    resizeCallback?.([{ contentRect: { width } }])
  })
}

// --- Helper to set window.innerWidth ---
function setWindowWidth(width: number) {
  Object.defineProperty(window, 'innerWidth', {
    writable: true,
    configurable: true,
    value: width,
  })
}

// --- Phase factory ---
let nextId = 0

function createPhase(
  overrides: Partial<ProjectPhaseListDto> & { name: string; order: number },
): ProjectPhaseListDto {
  return {
    id: `test-phase-${nextId++}`,
    status: { id: 1, name: 'Not Started' },
    start: undefined,
    end: undefined,
    progress: 0,
    ...overrides,
  }
}

// Reset between tests
beforeEach(() => {
  nextId = 0
  resizeCallback = null
  observedElement = null
  setWindowWidth(1024)
})

describe('PhaseTimeline', () => {
  // --- Basic rendering ---

  it('renders nothing when phases is empty', () => {
    const { container } = render(<PhaseTimeline phases={[]} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('renders phase names', () => {
    const phases = [
      createPhase({ name: 'Discovery', order: 1 }),
      createPhase({ name: 'Development', order: 2 }),
      createPhase({ name: 'Launch', order: 3 }),
    ]

    render(<PhaseTimeline phases={phases} />)

    expect(screen.getByText('Discovery')).toBeInTheDocument()
    expect(screen.getByText('Development')).toBeInTheDocument()
    expect(screen.getByText('Launch')).toBeInTheDocument()
  })

  it('sorts phases by order', () => {
    const phases = [
      createPhase({ name: 'Launch', order: 3 }),
      createPhase({ name: 'Discovery', order: 1 }),
      createPhase({ name: 'Development', order: 2 }),
    ]

    const { container } = render(<PhaseTimeline phases={phases} />)

    const titles = container.querySelectorAll('.ant-steps-item-title')
    expect(titles[0]).toHaveTextContent('Discovery')
    expect(titles[1]).toHaveTextContent('Development')
    expect(titles[2]).toHaveTextContent('Launch')
  })

  it('does not mutate the original phases array', () => {
    const phases = [
      createPhase({ name: 'B', order: 2 }),
      createPhase({ name: 'A', order: 1 }),
    ]
    const original = [...phases]

    render(<PhaseTimeline phases={phases} />)

    expect(phases[0].name).toBe(original[0].name)
    expect(phases[1].name).toBe(original[1].name)
  })

  // --- Status rendering ---

  it('renders completed phases with finish status', () => {
    const phases = [
      createPhase({
        name: 'Discovery',
        order: 1,
        status: { id: 3, name: 'Completed' },
      }),
    ]

    const { container } = render(<PhaseTimeline phases={phases} />)

    expect(
      container.querySelector('.ant-steps-item-finish'),
    ).toBeInTheDocument()
  })

  it('renders in-progress phases with process status', () => {
    const phases = [
      createPhase({
        name: 'Development',
        order: 1,
        status: { id: 2, name: 'In Progress' },
      }),
    ]

    const { container } = render(<PhaseTimeline phases={phases} />)

    expect(
      container.querySelector('.ant-steps-item-process'),
    ).toBeInTheDocument()
  })

  it('renders canceled phases with error status', () => {
    const phases = [
      createPhase({
        name: 'Canceled Phase',
        order: 1,
        status: { id: 4, name: 'Canceled' },
      }),
    ]

    const { container } = render(<PhaseTimeline phases={phases} />)

    expect(container.querySelector('.ant-steps-item-error')).toBeInTheDocument()
  })

  it('renders not-started phases with wait status', () => {
    const phases = [
      createPhase({
        name: 'Future Phase',
        order: 1,
        status: { id: 1, name: 'Not Started' },
      }),
    ]

    const { container } = render(<PhaseTimeline phases={phases} />)

    expect(container.querySelector('.ant-steps-item-wait')).toBeInTheDocument()
  })

  it('handles mixed statuses', () => {
    const phases = [
      createPhase({
        name: 'Done',
        order: 1,
        status: { id: 3, name: 'Completed' },
      }),
      createPhase({
        name: 'Active',
        order: 2,
        status: { id: 2, name: 'In Progress' },
      }),
      createPhase({
        name: 'Upcoming',
        order: 3,
        status: { id: 1, name: 'Not Started' },
      }),
    ]

    const { container } = render(<PhaseTimeline phases={phases} />)

    expect(
      container.querySelector('.ant-steps-item-finish'),
    ).toBeInTheDocument()
    expect(
      container.querySelector('.ant-steps-item-process'),
    ).toBeInTheDocument()
    expect(container.querySelector('.ant-steps-item-wait')).toBeInTheDocument()
  })

  // --- Inline content (default mode) ---

  it('shows dates inline in default mode', () => {
    const phases = [
      createPhase({
        name: 'Discovery',
        order: 1,
        status: { id: 2, name: 'In Progress' },
        start: new Date('2026-01-15T12:00:00'),
        end: new Date('2026-03-15T12:00:00'),
      }),
    ]

    render(<PhaseTimeline phases={phases} displayMode="default" />)

    expect(screen.getByText('Jan 15 - Mar 15, 2026')).toBeInTheDocument()
  })

  it('shows progress inline in default mode', () => {
    const phases = [
      createPhase({
        name: 'Discovery',
        order: 1,
        status: { id: 2, name: 'In Progress' },
        progress: 45,
      }),
    ]

    render(<PhaseTimeline phases={phases} displayMode="default" />)

    expect(screen.getByText('45%')).toBeInTheDocument()
  })

  it('does not show dates when dates are not set', () => {
    const phases = [
      createPhase({
        name: 'Discovery',
        order: 1,
        status: { id: 1, name: 'Not Started' },
      }),
    ]

    render(<PhaseTimeline phases={phases} displayMode="default" />)

    expect(screen.queryByText(/Jan|Feb|Mar/)).not.toBeInTheDocument()
  })

  it('shows start-only date inline', () => {
    const phases = [
      createPhase({
        name: 'Discovery',
        order: 1,
        status: { id: 2, name: 'In Progress' },
        start: new Date('2026-02-01T12:00:00'),
      }),
    ]

    render(<PhaseTimeline phases={phases} displayMode="default" />)

    expect(screen.getByText('Starts Feb 1, 2026')).toBeInTheDocument()
  })

  it('shows end-only date inline', () => {
    const phases = [
      createPhase({
        name: 'Discovery',
        order: 1,
        status: { id: 2, name: 'In Progress' },
        end: new Date('2026-06-30T12:00:00'),
      }),
    ]

    render(<PhaseTimeline phases={phases} displayMode="default" />)

    expect(screen.getByText('Ends Jun 30, 2026')).toBeInTheDocument()
  })

  // --- Small mode ---

  it('hides inline content in small mode', () => {
    const phases = [
      createPhase({
        name: 'Discovery',
        order: 1,
        status: { id: 2, name: 'In Progress' },
        start: new Date('2026-01-15T12:00:00'),
        end: new Date('2026-03-15T12:00:00'),
        progress: 45,
      }),
    ]

    render(<PhaseTimeline phases={phases} displayMode="small" />)

    expect(screen.queryByText('Jan 15 - Mar 15, 2026')).not.toBeInTheDocument()
    expect(screen.queryByText('45%')).not.toBeInTheDocument()
  })

  it('shows tooltip with details in small mode on hover', async () => {
    const phases = [
      createPhase({
        name: 'Discovery',
        order: 1,
        status: { id: 2, name: 'In Progress' },
        start: new Date('2026-01-15T12:00:00'),
        end: new Date('2026-03-15T12:00:00'),
        progress: 45,
      }),
    ]

    render(<PhaseTimeline phases={phases} displayMode="small" />)

    await userEvent.hover(screen.getByText('Discovery'))

    expect(await screen.findByText('In Progress')).toBeInTheDocument()
    expect(await screen.findByText('Jan 15 - Mar 15, 2026')).toBeInTheDocument()
    expect(await screen.findByText('Progress: 45%')).toBeInTheDocument()
  })

  // --- Tooltip in default mode ---

  it('shows tooltip with status only in default mode on hover', async () => {
    const phases = [
      createPhase({
        name: 'Discovery',
        order: 1,
        status: { id: 2, name: 'In Progress' },
        start: new Date('2026-01-15T12:00:00'),
        end: new Date('2026-03-15T12:00:00'),
        progress: 45,
      }),
    ]

    render(<PhaseTimeline phases={phases} displayMode="default" />)

    await userEvent.hover(screen.getByText('Discovery'))

    expect(await screen.findByText('In Progress')).toBeInTheDocument()
  })

  // --- Auto-sizing display modes ---

  describe('auto-sizing', () => {
    const threePhases = [
      createPhase({ name: 'Plan', order: 1 }),
      createPhase({ name: 'Execute', order: 2 }),
      createPhase({ name: 'Deliver', order: 3 }),
    ]

    it('uses default mode when container is wide enough', () => {
      // 3 phases × 120px = 360px needed for default
      const { container } = render(<PhaseTimeline phases={threePhases} />)
      triggerResize(400)

      expect(
        container.querySelector('.ant-steps-horizontal'),
      ).toBeInTheDocument()
    })

    it('uses small mode when container is moderately narrow', () => {
      // 3 phases × 120px = 360px for default, 3 × 70px = 210px for vertical
      const { container } = render(<PhaseTimeline phases={threePhases} />)
      triggerResize(250)

      expect(
        container.querySelector('.ant-steps-horizontal'),
      ).toBeInTheDocument()
    })

    it('switches to vertical when container is too narrow', () => {
      // 3 phases × 70px = 210px threshold
      const { container } = render(<PhaseTimeline phases={threePhases} />)
      triggerResize(150)

      expect(container.querySelector('.ant-steps-vertical')).toBeInTheDocument()
    })

    it('shows inline content in vertical mode', () => {
      const phases = [
        createPhase({
          name: 'Plan',
          order: 1,
          start: new Date('2026-01-15T12:00:00'),
          end: new Date('2026-03-15T12:00:00'),
          progress: 50,
        }),
      ]

      render(<PhaseTimeline phases={phases} />)
      triggerResize(50)

      expect(screen.getByText('Jan 15 - Mar 15, 2026')).toBeInTheDocument()
      expect(screen.getByText('50%')).toBeInTheDocument()
    })

    it('switches to vertical when page width is below 500px', () => {
      setWindowWidth(400)
      const { container } = render(<PhaseTimeline phases={threePhases} />)
      triggerResize(800) // container is wide, but page is narrow

      expect(container.querySelector('.ant-steps-vertical')).toBeInTheDocument()
    })

    it('skips auto-detection when size is explicitly set', () => {
      const { container } = render(
        <PhaseTimeline phases={threePhases} displayMode="small" />,
      )
      triggerResize(800)

      // Should remain horizontal small, not switch to default
      expect(
        container.querySelector('.ant-steps-horizontal'),
      ).toBeInTheDocument()
    })

    it('adapts breakpoints to phase count', () => {
      const sixPhases = Array.from({ length: 6 }, (_, i) =>
        createPhase({ name: `Phase ${i + 1}`, order: i + 1 }),
      )

      // 6 × 70px = 420px for vertical threshold
      const { container } = render(<PhaseTimeline phases={sixPhases} />)
      triggerResize(400)

      expect(container.querySelector('.ant-steps-vertical')).toBeInTheDocument()
    })

    it('stays horizontal for few phases at same width', () => {
      const twoPhases = [
        createPhase({ name: 'Start', order: 1 }),
        createPhase({ name: 'End', order: 2 }),
      ]

      // 2 × 70px = 140px for vertical threshold — 400px is well above
      const { container } = render(<PhaseTimeline phases={twoPhases} />)
      triggerResize(400)

      expect(
        container.querySelector('.ant-steps-horizontal'),
      ).toBeInTheDocument()
    })
  })
})
