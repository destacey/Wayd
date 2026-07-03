import { render, screen, fireEvent } from '@testing-library/react'

import CombinedFilterPanel from './combined-filter-panel'
import type { ColumnFilterModel } from './filter-model'

const ALL = ['Core Services', 'Team Juice', 'Team Sauce']

const setRow = (label: string) =>
  screen.getByText(label).closest('label') as HTMLLabelElement
const checkboxIn = (label: HTMLElement) =>
  label.querySelector('input[type="checkbox"]') as HTMLInputElement

describe('CombinedFilterPanel', () => {
  it('renders the Text Filter expander and the set list', () => {
    // Arrange / Act
    render(
      <CombinedFilterPanel allValues={ALL} value={undefined} onChange={jest.fn()} />,
    )

    // Assert — Text Filter header + set values both present
    expect(screen.getByText('Text Filter')).toBeInTheDocument()
    expect(screen.getByText('(Select All)')).toBeInTheDocument()
    expect(screen.getByText('Team Juice')).toBeInTheDocument()
  })

  it('collapses the text section by default when unfiltered', () => {
    // Arrange / Act
    render(
      <CombinedFilterPanel allValues={ALL} value={undefined} onChange={jest.fn()} />,
    )

    // Assert — no text value input shown until the expander is opened
    expect(screen.queryByPlaceholderText('Value')).not.toBeInTheDocument()
  })

  it('auto-expands the text section when a text filter is already active', () => {
    // Arrange / Act — e.g. the user typed in the floating input
    render(
      <CombinedFilterPanel
        allValues={ALL}
        value={{
          type: 'text',
          conditions: [{ op: 'contains', value: 'core' }],
          join: 'AND',
        }}
        onChange={jest.fn()}
      />,
    )

    // Assert — the text condition value is visible without expanding
    expect(screen.getByDisplayValue('core')).toBeInTheDocument()
  })

  it('emits a text descriptor when the text section is edited', () => {
    // Arrange
    const onChange = jest.fn()
    render(
      <CombinedFilterPanel allValues={ALL} value={undefined} onChange={onChange} />,
    )

    // Act — expand Text Filter, type a value
    fireEvent.click(screen.getByText('Text Filter'))
    fireEvent.change(screen.getByPlaceholderText('Value'), {
      target: { value: 'team' },
    })

    // Assert
    const next = onChange.mock.calls.at(-1)?.[0] as ColumnFilterModel
    expect(next.type).toBe('text')
    if (next.type === 'text') {
      expect(next.conditions[0].value).toBe('team')
    }
  })

  it('emits a set descriptor when set values are chosen (replacing text)', () => {
    // Arrange — start with a text filter active
    const onChange = jest.fn()
    render(
      <CombinedFilterPanel
        allValues={ALL}
        value={{
          type: 'text',
          conditions: [{ op: 'contains', value: 'core' }],
          join: 'AND',
        }}
        onChange={onChange}
      />,
    )

    // Act — uncheck a set value (set side reads as all-checked since the active
    // descriptor is text), which emits a set descriptor
    fireEvent.click(checkboxIn(setRow('Team Juice')))

    // Assert — now a set descriptor (text is gone → last-wins/clear)
    const next = onChange.mock.calls.at(-1)?.[0] as ColumnFilterModel
    expect(next.type).toBe('set')
  })

  it('shows the set side as unfiltered while a text filter is active', () => {
    // Arrange / Act — text active; set side should read all-checked
    render(
      <CombinedFilterPanel
        allValues={ALL}
        value={{
          type: 'text',
          conditions: [{ op: 'contains', value: 'core' }],
          join: 'AND',
        }}
        onChange={jest.fn()}
      />,
    )

    // Assert — every set value checked (text doesn't constrain the set UI)
    ALL.forEach((v) => expect(checkboxIn(setRow(v)).checked).toBe(true))
  })
})
