import { fireEvent, render, screen } from '@testing-library/react'
import WidgetCard, { WidgetCardProps } from './widget-card'
import { WidgetDisplay, WidgetDisplayContext } from './widget-display'
import {
  renderReportingWidget,
  widgetDisplay,
} from './widget-display-test-utils'
import { HomeWidget } from './widget-registry'
import { rowsSetting } from './widget-settings'
import { WidgetSettingsContext } from './widget-settings-form'

const widget = (overrides: Partial<HomeWidget> = {}): HomeWidget => ({
  id: 'a',
  title: 'Alpha',
  description: '',
  defaultColumn: 'main',
  defaultOrder: 10,
  requires: {},
  Component: () => null,
  settings: [rowsSetting(5)],
  ...overrides,
})

describe('WidgetCard', () => {
  it('offers no settings away from the page', () => {
    // Act
    render(<WidgetCard title="Alpha" />)

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Alpha settings' }),
    ).not.toBeInTheDocument()
  })

  it('offers no settings for a widget that declares none', () => {
    // Act
    render(
      <WidgetSettingsContext
        value={{
          widget: widget({ settings: undefined }),
          stored: {},
          onChange: vi.fn(),
        }}
      >
        <WidgetCard title="Alpha" />
      </WidgetSettingsContext>,
    )

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Alpha settings' }),
    ).not.toBeInTheDocument()
  })

  it('opens the settings from its gear, beside the View all link, and reports a change', async () => {
    // Arrange
    const onChange = vi.fn()
    render(
      <WidgetSettingsContext value={{ widget: widget(), stored: {}, onChange }}>
        <WidgetCard title="Alpha" viewAllHref="/alpha" />
      </WidgetSettingsContext>,
    )

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Alpha settings' }))
    fireEvent.click(await screen.findByRole('radio', { name: '10' }))

    // Assert
    expect(onChange).toHaveBeenCalledWith({ rows: '10' })
    expect(screen.getByRole('link', { name: 'View all' })).toBeInTheDocument()
  })
})

describe('WidgetCard empty display', () => {
  const display = widgetDisplay

  const body = () => screen.queryByText('How it fills.')

  const card = (props: Partial<WidgetCardProps>) => (
    <WidgetCard title="Alpha" emptySummary="No alphas" {...props}>
      How it fills.
    </WidgetCard>
  )

  const renderCard = (
    value: WidgetDisplay | null,
    props: Partial<WidgetCardProps> = {},
  ) =>
    render(
      <WidgetDisplayContext value={value}>{card(props)}</WidgetDisplayContext>,
    )

  it.each([
    ['loading', { loading: true }],
    ['failed', { failed: true, empty: true }],
    ['empty', { empty: true }],
    ['rows', {}],
  ] as const)('reports %s to the page', (state, props) => {
    // Act
    const { reportedState } = renderReportingWidget(card(props))

    // Assert
    expect(reportedState()).toBe(state)
  })

  it('withdraws its report when it leaves the page', () => {
    // Arrange
    const value = display()
    const { unmount } = renderReportingWidget(card({ empty: true }), value)

    // Act
    unmount()

    // Assert
    expect(value.onReport).toHaveBeenLastCalledWith({
      widgetId: 'widget',
      state: undefined,
    })
  })

  it.each(['loading', 'failed', 'rows'] as const)(
    'draws a widget left on Explain in full while %s',
    (state) => {
      // Act
      const { container } = renderCard(display('explain'), {
        loading: state === 'loading',
        failed: state === 'failed',
      })

      // Assert
      expect(screen.getByText('Alpha')).toBeInTheDocument()
      expect(screen.queryByText('No alphas')).not.toBeInTheDocument()
      if (state === 'loading')
        expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
      else expect(body()).toBeInTheDocument()
    },
  )

  it('explains an empty widget by default, away from the page', () => {
    // Act
    renderCard(null, { empty: true })

    // Assert
    expect(body()).toBeInTheDocument()
    expect(screen.queryByText('No alphas')).not.toBeInTheDocument()
  })

  it('draws an empty widget set to One line as its title and summary alone', () => {
    // Act
    renderCard(display('line'), { empty: true, viewAllHref: '/alpha' })

    // Assert
    expect(screen.getByText('Alpha')).toBeInTheDocument()
    expect(screen.getByText('No alphas')).toBeInTheDocument()
    expect(body()).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'View all' })).toBeInTheDocument()
  })

  it('draws nothing for an empty widget set to Hide', () => {
    // Act
    const { container } = renderCard(display('hide'), { empty: true })

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('draws an empty widget set to Hide in full once the user shows the hidden', () => {
    // Act
    renderCard(display('hide', true), { empty: true })

    // Assert
    expect(body()).toBeInTheDocument()
  })

  it.each(['line', 'hide'] as const)(
    'keeps a widget set to %s on its skeleton while it loads',
    (emptyDisplay) => {
      // Act
      const { container } = renderCard(display(emptyDisplay), {
        loading: true,
        empty: true,
      })

      // Assert
      expect(screen.getByText('Alpha')).toBeInTheDocument()
      expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
    },
  )

  it.each(['line', 'hide'] as const)(
    'shows a failed widget set to %s in full',
    (emptyDisplay) => {
      // Act
      renderCard(display(emptyDisplay), { failed: true, empty: true })

      // Assert
      expect(body()).toBeInTheDocument()
      expect(screen.queryByText('No alphas')).not.toBeInTheDocument()
    },
  )

  it.each(['line', 'hide'] as const)(
    'shows a widget with rows set to %s in full',
    (emptyDisplay) => {
      // Act
      renderCard(display(emptyDisplay))

      // Assert
      expect(body()).toBeInTheDocument()
    },
  )
})
