import { fireEvent, render, screen } from '@testing-library/react'
import { HomeWidget } from './widget-registry'
import { rowsSetting } from './widget-settings'
import WidgetSettingsForm from './widget-settings-form'

const widget: HomeWidget = {
  id: 'a',
  title: 'Alpha',
  description: '',
  defaultColumn: 'main',
  defaultOrder: 10,
  requires: {},
  Component: () => null,
  settings: [
    rowsSetting(5),
    {
      kind: 'toggle',
      key: 'unread-only',
      label: 'Unread only',
      default: false,
    },
  ],
}

describe('WidgetSettingsForm', () => {
  it('shows each setting at its default, with no reset, when nothing is stored', () => {
    // Act
    render(
      <WidgetSettingsForm widget={widget} stored={{}} onChange={vi.fn()} />,
    )

    // Assert
    expect(screen.getByRole('radio', { name: '5' })).toBeChecked()
    expect(
      screen.getByRole('switch', { name: 'Unread only' }),
    ).not.toBeChecked()
    expect(
      screen.queryByRole('button', { name: /Reset Alpha/ }),
    ).not.toBeInTheDocument()
  })

  it('reports a choice as the value to store', () => {
    // Arrange
    const onChange = vi.fn()
    render(
      <WidgetSettingsForm widget={widget} stored={{}} onChange={onChange} />,
    )

    // Act
    fireEvent.click(screen.getByRole('radio', { name: '10' }))

    // Assert
    expect(onChange).toHaveBeenCalledWith({ rows: '10' })
  })

  it('stores nothing for a setting put back to its default', () => {
    // Arrange
    const onChange = vi.fn()
    render(
      <WidgetSettingsForm
        widget={widget}
        stored={{ rows: '10', 'unread-only': 'true' }}
        onChange={onChange}
      />,
    )

    // Act
    fireEvent.click(screen.getByRole('radio', { name: '5' }))

    // Assert
    expect(onChange).toHaveBeenCalledWith({ 'unread-only': 'true' })
  })

  it('reports a toggle as true or false', () => {
    // Arrange
    const onChange = vi.fn()
    render(
      <WidgetSettingsForm widget={widget} stored={{}} onChange={onChange} />,
    )

    // Act
    fireEvent.click(screen.getByRole('switch', { name: 'Unread only' }))

    // Assert
    expect(onChange).toHaveBeenCalledWith({ 'unread-only': 'true' })
  })

  it('clears every stored value through its reset', () => {
    // Arrange
    const onChange = vi.fn()
    render(
      <WidgetSettingsForm
        widget={widget}
        stored={{ rows: '10' }}
        onChange={onChange}
      />,
    )

    // Act
    fireEvent.click(
      screen.getByRole('button', { name: 'Reset Alpha to default' }),
    )

    // Assert
    expect(onChange).toHaveBeenCalledWith({})
  })
})
