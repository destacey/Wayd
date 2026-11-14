import { fireEvent, render, screen } from '@testing-library/react'
import { HomeWidget } from './widget-registry'
import { rowsSetting, WidgetPickSetting } from './widget-settings'
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

  it('reports a multi-select as the values picked, and never clears the last one', () => {
    // Arrange
    const onChange = vi.fn()
    const picker: HomeWidget = {
      ...widget,
      settings: [
        {
          kind: 'multiSelect',
          key: 'types',
          label: 'Record types',
          options: [
            { value: 'a', label: 'A' },
            { value: 'b', label: 'B' },
          ],
          default: ['a', 'b'],
        },
      ],
    }
    const { rerender } = render(
      <WidgetSettingsForm widget={picker} stored={{}} onChange={onChange} />,
    )

    // Act
    fireEvent.click(screen.getByRole('checkbox', { name: 'A' }))
    rerender(
      <WidgetSettingsForm
        widget={picker}
        stored={{ types: 'b' }}
        onChange={onChange}
      />,
    )

    // Assert
    expect(onChange).toHaveBeenCalledWith({ types: 'b' })
    expect(screen.getByRole('group', { name: 'Record types' })).toBeVisible()
    expect(screen.getByRole('checkbox', { name: 'B' })).toBeDisabled()
    expect(screen.getByRole('checkbox', { name: 'A' })).toBeEnabled()
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

describe('WidgetSettingsForm pick setting', () => {
  const pick: WidgetPickSetting = {
    kind: 'pick',
    key: 'teams',
    label: 'Teams',
    noun: 'teams',
    Options: ({ children }) =>
      children({
        options: [
          { value: 'a', label: 'Alpha team' },
          { value: 'b', label: 'Beta team' },
          { value: 'c', label: 'Gamma team' },
        ],
        isLoading: false,
      }),
  }
  const picking: HomeWidget = { ...widget, settings: [pick] }

  it('checks every entry, with no note, while nothing is stored', () => {
    // Act
    render(
      <WidgetSettingsForm widget={picking} stored={{}} onChange={vi.fn()} />,
    )

    // Assert
    for (const name of ['Alpha team', 'Beta team', 'Gamma team'])
      expect(screen.getByRole('checkbox', { name })).toBeChecked()
    expect(screen.queryByText(/not shown/)).not.toBeInTheDocument()
  })

  it('stores the ids left when one is unchecked', () => {
    // Arrange
    const onChange = vi.fn()
    render(
      <WidgetSettingsForm widget={picking} stored={{}} onChange={onChange} />,
    )

    // Act
    fireEvent.click(screen.getByRole('checkbox', { name: 'Beta team' }))

    // Assert
    expect(onChange).toHaveBeenCalledWith({ teams: 'a,c' })
  })

  it('says how many a narrowed pick leaves out, ignoring an id no longer listed', () => {
    // Act
    render(
      <WidgetSettingsForm
        widget={picking}
        stored={{ teams: 'a,gone' }}
        onChange={vi.fn()}
      />,
    )

    // Assert
    expect(screen.getByRole('checkbox', { name: 'Alpha team' })).toBeChecked()
    expect(
      screen.getByRole('checkbox', { name: 'Beta team' }),
    ).not.toBeChecked()
    expect(
      screen.getByText(
        '2 of your 3 teams are not shown, nor will any you join later.',
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'Alpha team' })).toBeDisabled()
  })

  it('stores nothing once every entry is checked again, so one joined later shows', () => {
    // Arrange
    const onChange = vi.fn()
    render(
      <WidgetSettingsForm
        widget={picking}
        stored={{ teams: 'a,b' }}
        onChange={onChange}
      />,
    )

    // Act
    fireEvent.click(screen.getByRole('checkbox', { name: 'Gamma team' }))

    // Assert
    expect(onChange).toHaveBeenCalledWith({})
  })

  it('checks every entry when no stored id is still listed', () => {
    // Act
    render(
      <WidgetSettingsForm
        widget={picking}
        stored={{ teams: 'gone' }}
        onChange={vi.fn()}
      />,
    )

    // Assert
    expect(screen.getByRole('checkbox', { name: 'Gamma team' })).toBeChecked()
    expect(screen.queryByText(/not shown/)).not.toBeInTheDocument()
  })

  const withOptions = (
    options: { value: string; label: string }[],
    state: { isLoading?: boolean; isError?: boolean } = {},
  ): HomeWidget => ({
    ...widget,
    settings: [
      {
        ...pick,
        Options: ({ children }) =>
          children({
            options,
            isLoading: !!state.isLoading,
            isError: state.isError,
          }),
      },
    ],
  })

  it('keeps the last checked entry from being cleared', () => {
    // Act
    render(
      <WidgetSettingsForm
        widget={picking}
        stored={{ teams: 'b' }}
        onChange={vi.fn()}
      />,
    )

    // Assert
    expect(screen.getByRole('checkbox', { name: 'Beta team' })).toBeDisabled()
    expect(screen.getByRole('checkbox', { name: 'Alpha team' })).toBeEnabled()
  })

  it('shows a busy group, with nothing to change, while the list loads', () => {
    // Act
    render(
      <WidgetSettingsForm
        widget={withOptions([], { isLoading: true })}
        stored={{ teams: 'a' }}
        onChange={vi.fn()}
      />,
    )

    // Assert
    expect(screen.getByRole('group', { name: 'Teams' })).toHaveAttribute(
      'aria-busy',
      'true',
    )
    expect(screen.queryByRole('checkbox')).not.toBeInTheDocument()
  })

  it('says the list could not load rather than offering nothing', () => {
    // Act
    render(
      <WidgetSettingsForm
        widget={withOptions([], { isError: true })}
        stored={{}}
        onChange={vi.fn()}
      />,
    )

    // Assert
    expect(
      screen.getByRole('group', { name: 'Teams' }),
    ).toHaveAccessibleDescription('Could not load your teams.')
  })

  it('says a narrowed pick still keeps out later ones once every listed entry is checked, and offers to stop', () => {
    // Arrange — "a,b" was stored when team c was listed too; the caller has since left c
    const onChange = vi.fn()
    render(
      <WidgetSettingsForm
        widget={withOptions([
          { value: 'a', label: 'Alpha team' },
          { value: 'b', label: 'Beta team' },
        ])}
        stored={{ teams: 'a,b' }}
        onChange={onChange}
      />,
    )
    expect(
      screen.getByRole('group', { name: 'Teams' }),
    ).toHaveAccessibleDescription(
      'All your teams are shown, but any you join later will not be.',
    )

    // Act
    fireEvent.click(
      screen.getByRole('button', { name: 'Show any you join later too' }),
    )

    // Assert
    expect(onChange).toHaveBeenCalledWith({})
  })

  it('refuses a pick too long to save, saying how many it can hold', () => {
    // Arrange — 60 ids of 36 characters is past what one setting stores
    const ids = Array.from(
      { length: 60 },
      (_, i) => `0190a1b2-0000-7000-8000-${String(i).padStart(12, '0')}`,
    )
    const onChange = vi.fn()
    render(
      <WidgetSettingsForm
        widget={withOptions(
          ids.map((id, i) => ({ value: id, label: `Team ${i}` })),
        )}
        stored={{}}
        onChange={onChange}
      />,
    )

    // Act
    fireEvent.click(screen.getByRole('checkbox', { name: 'Team 0' }))

    // Assert
    expect(onChange).not.toHaveBeenCalled()
    expect(
      screen.getByText('Choose up to 55 of your teams, or all of them.'),
    ).toBeInTheDocument()
  })
})
