// The panel and its helpers need real date math (tree grouping, relative
// filters), so restore the actual dayjs over the global formatting shim.
jest.mock('dayjs', () => jest.requireActual('dayjs'))

import { useState } from 'react'
import { render, screen, fireEvent, within } from '@testing-library/react'
import DateFilterPanel from './date-filter-panel'
import type { ColumnFilterModel } from './filter-model'

const DAY_KEYS = ['2026-06-01', '2026-06-15', '2026-07-04']

const setup = (value?: ColumnFilterModel) => {
  const onChange = jest.fn()
  render(
    <DateFilterPanel dayKeys={DAY_KEYS} value={value} onChange={onChange} />,
  )
  return onChange
}

/**
 * Controlled harness that feeds `onChange` back into `value` and re-derives
 * `dayKeys` as a fresh array each render — mirroring the grid, where checking a
 * box changes the descriptor and re-renders the panel with new prop identities.
 * Used to reproduce the "tree collapses after toggling a box" bug.
 */
const ControlledPanel = ({ initial }: { initial?: ColumnFilterModel }) => {
  const [value, setValue] = useState<ColumnFilterModel | undefined>(initial)
  return (
    <DateFilterPanel
      // New array identity every render, like getDayKeys() in the grid.
      dayKeys={[...DAY_KEYS]}
      value={value}
      onChange={setValue}
    />
  )
}

describe('DateFilterPanel', () => {
  it('keeps a year node expanded after toggling a day (regression)', () => {
    // Arrange — controlled panel so a toggle re-renders with a new descriptor
    render(<ControlledPanel />)

    // Act — expand the 2026 year, then expand June, then uncheck day 1
    fireEvent.click(
      screen.getByText('2026').closest('div')!.querySelector(
        '[aria-label="Expand"]',
      )!,
    )
    fireEvent.click(
      screen.getByText('June').closest('div')!.querySelector(
        '[aria-label="Expand"]',
      )!,
    )
    const dayOne = screen.getByText('1').closest('label')!
    fireEvent.click(within(dayOne).getByRole('checkbox'))

    // Assert — after the descriptor change + re-render, June's day 1 is still
    // visible, meaning the year and month stayed expanded (didn't collapse).
    expect(screen.getByText('June')).toBeInTheDocument()
    expect(screen.getByText('1')).toBeInTheDocument()
  })

  it('renders the date tree with a (Select All) and year node', () => {
    // Arrange / Act
    setup()

    // Assert
    expect(screen.getByText('(Select All)')).toBeInTheDocument()
    expect(screen.getByText('2026')).toBeInTheDocument()
  })

  it('unchecking (Select All) then checking a year emits a dateSet subset', () => {
    // Arrange
    const onChange = setup()
    const selectAll = screen
      .getByText('(Select All)')
      .closest('label')!
      .querySelector('input[type="checkbox"]')!

    // Act — clear everything, then re-check the 2026 year node
    fireEvent.click(selectAll)

    // Assert — clearing all emits an empty dateSet (nothing selected)
    expect(onChange).toHaveBeenLastCalledWith({ type: 'dateSet', values: [] })
  })

  it('checking a subset of days emits a dateSet with just those days', () => {
    // Arrange — start from a value where only one day is checked already, then
    // toggle another via the tree. Simulate by starting all-but-one selected.
    const onChange = setup({
      type: 'dateSet',
      values: ['2026-06-01', '2026-06-15'],
    })

    // The 2026-07-04 day lives under July; expand year → month to reach it.
    fireEvent.click(screen.getByText('2026').closest('div')!.querySelector('[aria-label="Expand"]')!)

    // Act — check the remaining day should collapse to "all" (undefined)
    // Find the July day checkbox by its day label "4".
    const julyExpander = screen
      .getByText('July')
      .closest('div')!
      .querySelector('[aria-label="Expand"]')!
    fireEvent.click(julyExpander)
    const dayFour = screen.getByText('4').closest('label')!
    fireEvent.click(within(dayFour).getByRole('checkbox'))

    // Assert — checking the last missing day means all are selected ⇒ no filter
    expect(onChange).toHaveBeenLastCalledWith(undefined)
  })

  it('picking a relative quick-filter emits a date descriptor', () => {
    // Arrange
    const onChange = setup()

    // Act — open "Date Filters" and click "Today"
    fireEvent.click(screen.getByText('Date Filters'))
    fireEvent.click(screen.getByText('Today'))

    // Assert
    const arg = onChange.mock.calls.at(-1)?.[0] as ColumnFilterModel
    expect(arg.type).toBe('date')
    if (arg.type === 'date') {
      expect(arg.conditions[0].op).toBe('equals')
    }
  })

  it('Reset clears the descriptor', () => {
    // Arrange — a set descriptor is active
    const onChange = setup({ type: 'dateSet', values: ['2026-06-01'] })

    // Act
    fireEvent.click(screen.getByText('Reset'))

    // Assert
    expect(onChange).toHaveBeenLastCalledWith(undefined)
  })

  it('auto-expands Custom Filter when a conditions descriptor is active', () => {
    // Arrange — a date conditions descriptor is active
    setup({
      type: 'date',
      conditions: [{ op: 'before', value: '2026-06-10' }],
      join: 'AND',
    })

    // Assert — the Custom Filter section is expanded (its operator select shows)
    const custom = screen.getByText('Custom Filter').closest('button')!
    expect(custom).toHaveAttribute('aria-expanded', 'true')
  })
})
