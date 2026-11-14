import { render, screen, act } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import { ProductStatusAlias } from '@/src/services/wayd-api'
import { formatInstantInZone } from '@/src/utils'
import IncidentStatusStepper from './incident-status-stepper'

type ResizeCallback = (entries: { contentRect: { width: number } }[]) => void

let resizeCallback: ResizeCallback | null = null

class MockResizeObserver {
  constructor(cb: ResizeCallback) {
    resizeCallback = cb
  }
  observe() {}
  unobserve() {}
  disconnect() {
    resizeCallback = null
  }
}

global.ResizeObserver = MockResizeObserver as unknown as typeof ResizeObserver

function triggerResize(width: number) {
  act(() => {
    resizeCallback?.([{ contentRect: { width } }])
  })
}

function setWindowWidth(width: number) {
  Object.defineProperty(window, 'innerWidth', {
    writable: true,
    configurable: true,
    value: width,
  })
}

const noticedAt = new Date('2026-11-12T16:00:00Z')
const startedAt = new Date('2026-11-12T18:30:00Z')
const recoveredAt = new Date('2026-11-12T20:15:00Z')
const viewerZone = 'America/Phoenix'

const noticedLabel = formatInstantInZone(noticedAt, viewerZone)
const startedLabel = formatInstantInZone(startedAt, viewerZone)
const recoveredLabel = formatInstantInZone(recoveredAt, viewerZone)

vi.mock(import('@/src/store/features/product-management/incidents-api'), () => ({
  useGetIncidentStatusWorkflowQuery: () => ({
    data: {
      statuses: [
        { id: 'open', name: 'Open', order: 1, isRetired: false, aliasName: ProductStatusAlias.Open },
        {
          id: 'responding',
          name: 'Responding',
          order: 2,
          isRetired: false,
          aliasName: ProductStatusAlias.Responding,
        },
        {
          id: 'resolved',
          name: 'Resolved',
          order: 3,
          isRetired: false,
          aliasName: ProductStatusAlias.Resolved,
        },
        {
          id: 'closed',
          name: 'Closed',
          order: 4,
          isRetired: false,
          aliasName: ProductStatusAlias.Closed,
        },
        {
          id: 'canceled',
          name: 'Canceled',
          order: 5,
          isRetired: false,
          aliasName: ProductStatusAlias.Canceled,
        },
      ],
    },
  }),
}))

function renderStepper(
  currentStatusId: string,
  instants: {
    noticedAt?: Date
    startedAt?: Date
    recoveredAt?: Date
  } = { noticedAt, startedAt, recoveredAt },
) {
  return render(
    <IncidentStatusStepper
      incidentId="incident-1"
      currentStatusId={currentStatusId}
      viewerZone={viewerZone}
      {...instants}
    />,
  )
}

beforeEach(() => {
  resizeCallback = null
  setWindowWidth(1024)
})

describe('IncidentStatusStepper', () => {
  it('puts the status name under the node and the stored time under the name', () => {
    // Act
    const { container } = renderStepper('responding')
    triggerResize(800)

    // Assert
    expect(container.querySelector('.ant-steps-horizontal')).toBeInTheDocument()
    expect(container.querySelector('.ant-steps-title-vertical')).toBeInTheDocument()

    const open = screen.getByText('Open').closest('.ant-steps-item')
    const responding = screen.getByText('Responding').closest('.ant-steps-item')
    const resolved = screen.getByText('Resolved').closest('.ant-steps-item')
    const closed = screen.getByText('Closed').closest('.ant-steps-item')
    const canceled = screen.getByText('Canceled').closest('.ant-steps-item')

    expect(open).toHaveTextContent(noticedLabel)
    expect(responding).toHaveTextContent(startedLabel)
    expect(resolved).toHaveTextContent(recoveredLabel)
    expect(closed).not.toHaveTextContent(recoveredLabel)
    expect(canceled).not.toHaveTextContent(/\d/)
    expect(open).toHaveClass('ant-steps-item-finish')
    expect(responding).toHaveClass('ant-steps-item-process')
    expect(resolved).toHaveClass('ant-steps-item-wait')
  })

  it('leaves a status blank when the incident has not stored that time', () => {
    // Act
    renderStepper('open', { noticedAt })
    triggerResize(800)

    // Assert
    expect(screen.getByText('Open').closest('.ant-steps-item')).toHaveTextContent(
      noticedLabel,
    )
    expect(screen.queryByText(startedLabel)).not.toBeInTheDocument()
    expect(screen.queryByText(recoveredLabel)).not.toBeInTheDocument()
  })

  it('marks a canceled incident canceled without a time of its own', () => {
    // Act
    const { container } = renderStepper('canceled', { noticedAt })
    triggerResize(800)

    // Assert
    expect(container.querySelector('.ant-steps-item-error')).toHaveTextContent('Canceled')
    expect(screen.getByText('Canceled').closest('.ant-steps-item')).not.toHaveTextContent(/\d/)
    expect(screen.getByText('Responding').closest('.ant-steps-item')).toHaveClass(
      'ant-steps-item-wait',
    )
    expect(screen.getByText('Open').closest('.ant-steps-item')).toHaveClass(
      'ant-steps-item-finish',
    )
  })

  it('hides the times when the row is compressed and does not scroll', async () => {
    // Act
    const { container } = renderStepper('responding')
    triggerResize(400)

    // Assert
    expect(container.querySelector('.ant-steps-horizontal')).toBeInTheDocument()
    expect(container.querySelector('.ant-steps-title-vertical')).toBeInTheDocument()
    expect(screen.getByText('Responding')).toBeInTheDocument()
    expect(screen.queryByText(noticedLabel)).not.toBeInTheDocument()
    expect(container.querySelector('.ant-steps')?.getAttribute('style') ?? '').not.toMatch(
      /overflow/i,
    )
    expect((container.firstChild as HTMLElement).getAttribute('style') ?? '').not.toMatch(
      /overflow/i,
    )

    await userEvent.hover(screen.getByText('Open'))
    expect(await screen.findByText(noticedLabel)).toBeInTheDocument()
  })

  it('stacks on a narrow page and shows the times again', () => {
    // Arrange
    setWindowWidth(400)

    // Act
    const { container } = renderStepper('responding')
    triggerResize(800)

    // Assert
    expect(container.querySelector('.ant-steps-vertical')).toBeInTheDocument()
    expect(screen.getByText(noticedLabel)).toBeInTheDocument()
    expect(screen.getByText(startedLabel)).toBeInTheDocument()
  })

  it('stacks when the row is narrower than the names and shows the times again', () => {
    // Act
    const { container } = renderStepper('responding')
    triggerResize(200)

    // Assert
    expect(container.querySelector('.ant-steps-vertical')).toBeInTheDocument()
    expect(screen.getByText(recoveredLabel)).toBeInTheDocument()
  })
})
