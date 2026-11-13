import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import AttentionTile, { attentionColor } from './attention-tile'
import { AttentionSlotContext } from './attention-strip'
import { tileValueStyle } from './attention-tile-test-utils'

const mockPush = vi.fn()

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}))

describe('attentionColor', () => {
  it.each([
    [3, 'error', 'var(--ant-color-error)'],
    [1, 'warning', 'var(--ant-color-warning)'],
  ] as const)('colours %i with the %s colour', (value, alert, expected) => {
    // Arrange / Act
    const color = attentionColor(value, alert)

    // Assert
    expect(color).toBe(expected)
  })

  it.each([0, undefined])('leaves %s uncoloured', (value) => {
    // Arrange / Act
    const color = attentionColor(value, 'error')

    // Assert
    expect(color).toBeUndefined()
  })
})

describe('AttentionTile', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  const renderTile = (
    props: Partial<React.ComponentProps<typeof AttentionTile>> = {},
  ) =>
    render(
      <AttentionTile
        title="Overdue tasks"
        value={4}
        tooltip="Open tasks past their planned end."
        href="/ppm/dashboards/projects"
        alert="error"
        loading={false}
        {...props}
      />,
    )

  it('shows the count in the alert colour above zero', () => {
    // Arrange / Act
    const { container } = renderTile({ value: 4 })

    // Assert
    expect(
      screen.getByRole('button', { name: 'Overdue tasks: 4' }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).toContain('var(--ant-color-error)')
  })

  it('leaves a zero uncoloured', () => {
    // Arrange / Act
    const { container } = renderTile({ value: 0 })

    // Assert
    expect(
      screen.getByRole('button', { name: 'Overdue tasks: 0' }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).not.toContain('var(--ant-color')
  })

  it('shows a dash, not a zero, when the count could not be loaded', () => {
    // Arrange / Act
    renderTile({ value: undefined })

    // Assert
    expect(
      screen.getByRole('button', { name: 'Overdue tasks: unavailable' }),
    ).toBeInTheDocument()
    expect(screen.getByText('—')).toBeInTheDocument()
  })

  it('says under the number where the count comes from', () => {
    // Arrange / Act
    renderTile({ value: 4, context: 'on your projects' })

    // Assert
    expect(screen.getByText('on your projects')).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Overdue tasks on your projects: 4' }),
    ).toBeInTheDocument()
  })

  it('marks a capped count with a plus', () => {
    // Arrange / Act
    renderTile({ value: 99, capped: true })

    // Assert
    expect(
      screen.getByRole('button', { name: 'Overdue tasks: 99+' }),
    ).toBeInTheDocument()
  })

  it('opens the list behind the count', async () => {
    // Arrange
    renderTile({ href: '/account/notifications' })

    // Act
    await userEvent.click(screen.getByRole('button'))

    // Assert
    expect(mockPush).toHaveBeenCalledWith('/account/notifications')
  })

  it('scrolls to a widget on this page instead of navigating', async () => {
    // Arrange
    const target = document.createElement('section')
    target.id = 'home-widget-my-risks'
    target.scrollIntoView = vi.fn()
    document.body.appendChild(target)
    renderTile({ href: '#home-widget-my-risks' })

    // Act
    await userEvent.click(screen.getByRole('button'))

    // Assert
    expect(target.scrollIntoView).toHaveBeenCalled()
    expect(mockPush).not.toHaveBeenCalled()
    target.remove()
  })

  it('shows the widgets hidden as empty before scrolling to one of them', async () => {
    // Arrange
    const target = document.createElement('section')
    target.id = 'home-widget-my-risks'
    target.hidden = true
    target.scrollIntoView = vi.fn()
    document.body.appendChild(target)
    const revealHiddenWidgets = vi.fn(() => {
      target.hidden = false
    })
    render(
      <AttentionSlotContext.Provider
        value={{ visible: true, report: vi.fn(), revealHiddenWidgets }}
      >
        <AttentionTile
          title="Risk follow-ups"
          value={1}
          tooltip="Follow-ups due."
          href="#home-widget-my-risks"
          alert="warning"
          loading={false}
        />
      </AttentionSlotContext.Provider>,
    )

    // Act
    await userEvent.click(screen.getByRole('button'))
    await new Promise((resolve) => requestAnimationFrame(resolve))

    // Assert
    expect(revealHiddenWidgets).toHaveBeenCalled()
    expect(target.scrollIntoView).toHaveBeenCalled()
    target.remove()
  })
})

describe('AttentionTile in a strip slot', () => {
  const tileIn = (visible: boolean, report = vi.fn()) => {
    render(
      <AttentionSlotContext.Provider value={{ visible, report }}>
        <AttentionTile
          title="Overdue tasks"
          value={4}
          tooltip="Open tasks past their planned end."
          href="/ppm/dashboards/projects"
          alert="error"
          loading={false}
        />
      </AttentionSlotContext.Provider>,
    )
    return report
  }

  it('reports its count to the slot', () => {
    // Arrange / Act
    const report = tileIn(true)

    // Assert
    expect(report).toHaveBeenCalledWith({ value: 4, loading: false })
    expect(
      screen.getByRole('button', { name: 'Overdue tasks: 4' }),
    ).toBeInTheDocument()
  })

  it('draws nothing without a slot, but still reports', () => {
    // Arrange / Act
    const report = tileIn(false)

    // Assert
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
    expect(report).toHaveBeenCalledWith({ value: 4, loading: false })
  })
})
