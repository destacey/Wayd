import { render, screen, act } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import { ProjectStageListDto } from '@/src/services/wayd-api'
import StageTimeline from './stage-timeline'

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

// --- Stage factory ---
let nextId = 0

function createStage(
  overrides: Partial<ProjectStageListDto> & { name: string; order: number },
): ProjectStageListDto {
  return {
    id: `test-stage-${nextId++}`,
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

describe('StageTimeline', () => {
  // --- Basic rendering ---

  it('renders nothing when stages is empty', () => {
    const { container } = render(<StageTimeline stages={[]} />)
    expect(container).toBeEmptyDOMElement()
  })

  it('renders stage names', () => {
    const stages = [
      createStage({ name: 'Discovery', order: 1 }),
      createStage({ name: 'Development', order: 2 }),
      createStage({ name: 'Launch', order: 3 }),
    ]

    render(<StageTimeline stages={stages} />)

    expect(screen.getByText('Discovery')).toBeInTheDocument()
    expect(screen.getByText('Development')).toBeInTheDocument()
    expect(screen.getByText('Launch')).toBeInTheDocument()
  })

  it('sorts stages by order', () => {
    const stages = [
      createStage({ name: 'Launch', order: 3 }),
      createStage({ name: 'Discovery', order: 1 }),
      createStage({ name: 'Development', order: 2 }),
    ]

    const { container } = render(<StageTimeline stages={stages} />)

    const titles = container.querySelectorAll('.ant-steps-item-title')
    expect(titles[0]).toHaveTextContent('Discovery')
    expect(titles[1]).toHaveTextContent('Development')
    expect(titles[2]).toHaveTextContent('Launch')
  })

  it('does not mutate the original stages array', () => {
    const stages = [
      createStage({ name: 'B', order: 2 }),
      createStage({ name: 'A', order: 1 }),
    ]
    const original = [...stages]

    render(<StageTimeline stages={stages} />)

    expect(stages[0].name).toBe(original[0].name)
    expect(stages[1].name).toBe(original[1].name)
  })

  // --- Status rendering ---

  it('renders completed stages with finish status', () => {
    const stages = [
      createStage({
        name: 'Discovery',
        order: 1,
        status: { id: 3, name: 'Completed' },
      }),
    ]

    const { container } = render(<StageTimeline stages={stages} />)

    expect(
      container.querySelector('.ant-steps-item-finish'),
    ).toBeInTheDocument()
  })

  it('renders in-progress stages with process status', () => {
    const stages = [
      createStage({
        name: 'Development',
        order: 1,
        status: { id: 2, name: 'In Progress' },
      }),
    ]

    const { container } = render(<StageTimeline stages={stages} />)

    expect(
      container.querySelector('.ant-steps-item-process'),
    ).toBeInTheDocument()
  })

  it('renders canceled stages with error status', () => {
    const stages = [
      createStage({
        name: 'Canceled Stage',
        order: 1,
        status: { id: 4, name: 'Canceled' },
      }),
    ]

    const { container } = render(<StageTimeline stages={stages} />)

    expect(container.querySelector('.ant-steps-item-error')).toBeInTheDocument()
  })

  it('renders not-started stages with wait status', () => {
    const stages = [
      createStage({
        name: 'Future Stage',
        order: 1,
        status: { id: 1, name: 'Not Started' },
      }),
    ]

    const { container } = render(<StageTimeline stages={stages} />)

    expect(container.querySelector('.ant-steps-item-wait')).toBeInTheDocument()
  })

  it('handles mixed statuses', () => {
    const stages = [
      createStage({
        name: 'Done',
        order: 1,
        status: { id: 3, name: 'Completed' },
      }),
      createStage({
        name: 'Active',
        order: 2,
        status: { id: 2, name: 'In Progress' },
      }),
      createStage({
        name: 'Upcoming',
        order: 3,
        status: { id: 1, name: 'Not Started' },
      }),
    ]

    const { container } = render(<StageTimeline stages={stages} />)

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
    const stages = [
      createStage({
        name: 'Discovery',
        order: 1,
        status: { id: 2, name: 'In Progress' },
        start: new Date('2026-01-15T12:00:00'),
        end: new Date('2026-03-15T12:00:00'),
      }),
    ]

    render(<StageTimeline stages={stages} displayMode="default" />)

    expect(screen.getByText('Jan 15 - Mar 15, 2026')).toBeInTheDocument()
  })

  it('shows progress inline in default mode', () => {
    const stages = [
      createStage({
        name: 'Discovery',
        order: 1,
        status: { id: 2, name: 'In Progress' },
        progress: 45,
      }),
    ]

    render(<StageTimeline stages={stages} displayMode="default" />)

    expect(screen.getByText('45%')).toBeInTheDocument()
  })

  it('does not show dates when dates are not set', () => {
    const stages = [
      createStage({
        name: 'Discovery',
        order: 1,
        status: { id: 1, name: 'Not Started' },
      }),
    ]

    render(<StageTimeline stages={stages} displayMode="default" />)

    expect(screen.queryByText(/Jan|Feb|Mar/)).not.toBeInTheDocument()
  })

  it('shows start-only date inline', () => {
    const stages = [
      createStage({
        name: 'Discovery',
        order: 1,
        status: { id: 2, name: 'In Progress' },
        start: new Date('2026-02-01T12:00:00'),
      }),
    ]

    render(<StageTimeline stages={stages} displayMode="default" />)

    expect(screen.getByText('Starts Feb 1, 2026')).toBeInTheDocument()
  })

  it('shows end-only date inline', () => {
    const stages = [
      createStage({
        name: 'Discovery',
        order: 1,
        status: { id: 2, name: 'In Progress' },
        end: new Date('2026-06-30T12:00:00'),
      }),
    ]

    render(<StageTimeline stages={stages} displayMode="default" />)

    expect(screen.getByText('Ends Jun 30, 2026')).toBeInTheDocument()
  })

  // --- Small mode ---

  it('hides inline content in small mode', () => {
    const stages = [
      createStage({
        name: 'Discovery',
        order: 1,
        status: { id: 2, name: 'In Progress' },
        start: new Date('2026-01-15T12:00:00'),
        end: new Date('2026-03-15T12:00:00'),
        progress: 45,
      }),
    ]

    render(<StageTimeline stages={stages} displayMode="small" />)

    expect(screen.queryByText('Jan 15 - Mar 15, 2026')).not.toBeInTheDocument()
    expect(screen.queryByText('45%')).not.toBeInTheDocument()
  })

  it('shows tooltip with details in small mode on hover', async () => {
    const stages = [
      createStage({
        name: 'Discovery',
        order: 1,
        status: { id: 2, name: 'In Progress' },
        start: new Date('2026-01-15T12:00:00'),
        end: new Date('2026-03-15T12:00:00'),
        progress: 45,
      }),
    ]

    render(<StageTimeline stages={stages} displayMode="small" />)

    await userEvent.hover(screen.getByText('Discovery'))

    expect(await screen.findByText('In Progress')).toBeInTheDocument()
    expect(await screen.findByText('Jan 15 - Mar 15, 2026')).toBeInTheDocument()
    expect(await screen.findByText('Progress: 45%')).toBeInTheDocument()
  })

  // --- Tooltip in default mode ---

  it('shows tooltip with status only in default mode on hover', async () => {
    const stages = [
      createStage({
        name: 'Discovery',
        order: 1,
        status: { id: 2, name: 'In Progress' },
        start: new Date('2026-01-15T12:00:00'),
        end: new Date('2026-03-15T12:00:00'),
        progress: 45,
      }),
    ]

    render(<StageTimeline stages={stages} displayMode="default" />)

    await userEvent.hover(screen.getByText('Discovery'))

    expect(await screen.findByText('In Progress')).toBeInTheDocument()
  })

  // --- Auto-sizing display modes ---

  describe('auto-sizing', () => {
    const threeStages = [
      createStage({ name: 'Plan', order: 1 }),
      createStage({ name: 'Execute', order: 2 }),
      createStage({ name: 'Deliver', order: 3 }),
    ]

    it('uses default mode when container is wide enough', () => {
      // 3 stages × 120px = 360px needed for default
      const { container } = render(<StageTimeline stages={threeStages} />)
      triggerResize(400)

      expect(
        container.querySelector('.ant-steps-horizontal'),
      ).toBeInTheDocument()
    })

    it('uses small mode when container is moderately narrow', () => {
      // 3 stages × 120px = 360px for default, 3 × 70px = 210px for vertical
      const { container } = render(<StageTimeline stages={threeStages} />)
      triggerResize(250)

      expect(
        container.querySelector('.ant-steps-horizontal'),
      ).toBeInTheDocument()
    })

    it('switches to vertical when container is too narrow', () => {
      // 3 stages × 70px = 210px threshold
      const { container } = render(<StageTimeline stages={threeStages} />)
      triggerResize(150)

      expect(container.querySelector('.ant-steps-vertical')).toBeInTheDocument()
    })

    it('shows inline content in vertical mode', () => {
      const stages = [
        createStage({
          name: 'Plan',
          order: 1,
          start: new Date('2026-01-15T12:00:00'),
          end: new Date('2026-03-15T12:00:00'),
          progress: 50,
        }),
      ]

      render(<StageTimeline stages={stages} />)
      triggerResize(50)

      expect(screen.getByText('Jan 15 - Mar 15, 2026')).toBeInTheDocument()
      expect(screen.getByText('50%')).toBeInTheDocument()
    })

    it('switches to vertical when page width is below 500px', () => {
      setWindowWidth(400)
      const { container } = render(<StageTimeline stages={threeStages} />)
      triggerResize(800) // container is wide, but page is narrow

      expect(container.querySelector('.ant-steps-vertical')).toBeInTheDocument()
    })

    it('skips auto-detection when size is explicitly set', () => {
      const { container } = render(
        <StageTimeline stages={threeStages} displayMode="small" />,
      )
      triggerResize(800)

      // Should remain horizontal small, not switch to default
      expect(
        container.querySelector('.ant-steps-horizontal'),
      ).toBeInTheDocument()
    })

    it('adapts breakpoints to stage count', () => {
      const sixStages = Array.from({ length: 6 }, (_, i) =>
        createStage({ name: `Stage ${i + 1}`, order: i + 1 }),
      )

      // 6 × 70px = 420px for vertical threshold
      const { container } = render(<StageTimeline stages={sixStages} />)
      triggerResize(400)

      expect(container.querySelector('.ant-steps-vertical')).toBeInTheDocument()
    })

    it('stays horizontal for few stages at same width', () => {
      const twoStages = [
        createStage({ name: 'Start', order: 1 }),
        createStage({ name: 'End', order: 2 }),
      ]

      // 2 × 70px = 140px for vertical threshold — 400px is well above
      const { container } = render(<StageTimeline stages={twoStages} />)
      triggerResize(400)

      expect(
        container.querySelector('.ant-steps-horizontal'),
      ).toBeInTheDocument()
    })
  })
})
