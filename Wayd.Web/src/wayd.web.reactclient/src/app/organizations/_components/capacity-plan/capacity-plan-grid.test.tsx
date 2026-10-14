import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import CapacityPlanGrid from './capacity-plan-grid'
import { PlanRow, PlanWeek, RowEdits } from './capacity-plan-data'

jest.unmock('dayjs')

// The antd Table measures its scrollbar with a pseudo-element, which jsdom does
// not implement and reports as an error.
Object.defineProperty(window, 'getComputedStyle', {
  value: () => ({
    getPropertyValue: () => '',
  }),
})

// A week of three working days, then a last week of only a weekend.
const WEEKS: PlanWeek[] = [
  {
    index: 0,
    start: '2027-02-03',
    end: '2027-02-05',
    days: ['2027-02-03', '2027-02-04', '2027-02-05'],
  },
  { index: 1, start: '2027-02-06', end: '2027-02-07', days: [] },
]

const ROW: PlanRow = {
  id: 'ada|',
  employee: { id: 'ada', key: 1, name: 'Ada Lovelace' },
  countsTowardDeliveryCapacity: true,
  percentByDate: new Map([
    ['2027-02-03', 50],
    ['2027-02-04', 50],
    ['2027-02-05', 50],
  ]),
  personDaysByDate: new Map([
    ['2027-02-03', 0.5],
    ['2027-02-04', 0.5],
    ['2027-02-05', 0.5],
  ]),
  conditionsByDate: new Map(),
}

const mockEdits = jest.fn()

/** The grid in editing, holding its edits as the plan does. */
const EditingGrid = () => {
  const [edits, setEdits] = useState<Record<string, RowEdits>>({})
  mockEdits(edits)
  return (
    <CapacityPlanGrid
      rows={[ROW]}
      weeks={WEEKS}
      editing
      edits={edits}
      onEditRow={(rowId, rowEdits) =>
        setEdits((current) => ({ ...current, [rowId]: rowEdits }))
      }
    />
  )
}

const weekInput = () =>
  screen.getByRole('spinbutton', { name: /Week of Feb 3 – Feb 5/ })
const dayInput = (label: string) =>
  screen.getByRole('spinbutton', { name: new RegExp(`^${label} for`) })
const toggle = (direction: 'Expand' | 'Collapse') =>
  userEvent.click(
    screen.getAllByRole('button', {
      name: new RegExp(`${direction} week of Feb 3`),
    })[0],
  )

describe('CapacityPlanGrid', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('shows a week edit on each of its days once the week is expanded', async () => {
    render(<EditingGrid />)

    await userEvent.clear(weekInput())
    await userEvent.type(weekInput(), '80')
    await toggle('Expand')

    expect(dayInput('Feb 3, 2027')).toHaveValue('80')
    expect(dayInput('Feb 5, 2027')).toHaveValue('80')
  })

  it('shows the days a week was given once it is collapsed again', async () => {
    render(<EditingGrid />)

    await toggle('Expand')
    await userEvent.clear(dayInput('Feb 4, 2027'))
    await userEvent.type(dayInput('Feb 4, 2027'), '0')
    await toggle('Collapse')

    expect(weekInput()).toHaveValue('')
    expect(weekInput()).toHaveAttribute('placeholder', '0–50%')
  })

  it('replaces the days a week was given when the week is set', async () => {
    render(<EditingGrid />)

    await toggle('Expand')
    await userEvent.clear(dayInput('Feb 4, 2027'))
    await userEvent.type(dayInput('Feb 4, 2027'), '0')
    await toggle('Collapse')
    await userEvent.type(weekInput(), '70')

    expect(mockEdits).toHaveBeenLastCalledWith({ 'ada|': { 'w:0': 70 } })
  })

  it('offers no input for a week the team works no day of', () => {
    render(<EditingGrid />)

    expect(
      screen.queryByRole('spinbutton', { name: /Week of Feb 6 – Feb 7/ }),
    ).not.toBeInTheDocument()
    expect(screen.getByTestId('cell-ada|-w:1')).toHaveTextContent('—')
  })
})
