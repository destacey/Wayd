import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import CapacityPlanGrid, { PlanUnit } from './capacity-plan-grid'
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
  isFirstOfPerson: true,
  canManageTimeAway: true,
}

const mockEdits = jest.fn()

/** The grid in editing, holding its edits as the plan does. */
const EditingGrid = ({ unit = 'percent' }: { unit?: PlanUnit }) => {
  const [edits, setEdits] = useState<Record<string, RowEdits>>({})
  mockEdits(edits)
  return (
    <CapacityPlanGrid
      rows={[ROW]}
      weeks={WEEKS}
      editing
      unit={unit}
      workingDayHours={8}
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

  it('takes hours a day in hours and keeps them as the share of the day', async () => {
    render(<EditingGrid unit="hours" />)

    const input = screen.getByRole('spinbutton', {
      name: /Week of Feb 3 – Feb 5 .*hours a day/,
    })
    expect(input).toHaveValue('4.00')

    await userEvent.clear(input)
    await userEvent.type(input, '6')

    expect(mockEdits).toHaveBeenLastCalledWith({ 'ada|': { 'w:0': 75 } })
  })

  it('offers no input for a week the team works no day of', () => {
    render(<EditingGrid />)

    expect(
      screen.queryByRole('spinbutton', { name: /Week of Feb 6 – Feb 7/ }),
    ).not.toBeInTheDocument()
    expect(screen.getByTestId('cell-ada|-w:1')).toHaveTextContent('—')
  })
})

describe('CapacityPlanGrid in view', () => {
  const AWAY_ROW: PlanRow = {
    ...ROW,
    conditionsByDate: new Map([
      [
        '2027-02-03',
        {
          date: '2027-02-03',
          isHoliday: false,
          percentAway: 25,
          elsewherePercent: 0,
        },
      ],
      [
        '2027-02-04',
        {
          date: '2027-02-04',
          isHoliday: true,
          percentAway: 0,
          elsewherePercent: 0,
        },
      ],
    ]),
  }

  const renderView = (
    props: Partial<React.ComponentProps<typeof CapacityPlanGrid>> = {},
  ) =>
    render(
      <CapacityPlanGrid
        rows={[AWAY_ROW]}
        weeks={WEEKS}
        editing={false}
        edits={{}}
        onEditRow={jest.fn()}
        workingDayHours={8}
        {...props}
      />,
    )

  it('reads a week in hours a day in the hours unit', () => {
    renderView({ unit: 'hours' })

    expect(screen.getByTestId('cell-ada|-w:0')).toHaveTextContent('4 h/day')
    expect(screen.getAllByText('Hours').length).toBeGreaterThan(0)
  })

  it('says beneath a day how long the person is away, or that it is a holiday', async () => {
    renderView()

    await userEvent.click(
      screen.getAllByRole('button', { name: /Expand week of Feb 3/ })[0],
    )

    expect(screen.getByTestId('cell-ada|-d:2027-02-03')).toHaveTextContent(
      'Away 2 h',
    )
    expect(screen.getByTestId('cell-ada|-d:2027-02-04')).toHaveTextContent(
      'Holiday',
    )
    expect(screen.getByTestId('cell-ada|-d:2027-02-05')).toHaveTextContent(
      /^50%$/,
    )
  })

  it('offers to record time away for a person whose time away the caller may manage', async () => {
    const onRecordTimeAway = jest.fn()
    renderView({ onRecordTimeAway })

    await userEvent.click(
      screen.getAllByRole('button', {
        name: 'Record time away for Ada Lovelace',
      })[0],
    )

    expect(onRecordTimeAway).toHaveBeenCalledWith(AWAY_ROW)
  })

  it.each([
    ['who may not manage their time away', { canManageTimeAway: false }],
    ['on a second row for the person', { isFirstOfPerson: false }],
  ])('offers no time away action for a person %s', (_, row) => {
    renderView({
      rows: [{ ...AWAY_ROW, ...row }],
      onRecordTimeAway: jest.fn(),
    })

    expect(
      screen.queryByRole('button', { name: /Record time away/ }),
    ).not.toBeInTheDocument()
  })
})
