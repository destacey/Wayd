import { fireEvent, render, screen } from '@testing-library/react'
import WidgetCard from './widget-card'
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
