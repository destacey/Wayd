import { TeamCapacityPlanDto } from '@/src/services/wayd-api'
import {
  dayEditKey,
  hoursLabel,
  pendingPercent,
  planCell,
  percentLabel,
  planRequests,
  planRows,
  planWeeks,
  rowChanges,
  weekEditKey,
  weekIsEdited,
  withCellEdit,
} from './capacity-plan-data'

const TEAM = 'team-1'

// A two-week sprint, Wednesday to the Tuesday two weeks on, worked Monday to
// Friday: ten working days.
const FROM = '2027-02-03'
const TO = '2027-02-16'
const WORKING_DAYS = [
  '2027-02-03',
  '2027-02-04',
  '2027-02-05',
  '2027-02-08',
  '2027-02-09',
  '2027-02-10',
  '2027-02-11',
  '2027-02-12',
  '2027-02-15',
  '2027-02-16',
]

function plan(
  overrides: Partial<TeamCapacityPlanDto> = {},
): TeamCapacityPlanDto {
  return {
    capacity: {
      from: FROM,
      to: TO,
      workingDays: 10,
      allocatedPersonDays: 0,
      deliveryPersonDays: 0,
      awayPersonDays: 0,
      canManage: true,
      workingDayHours: 8,
      roles: [],
      people: [
        {
          employee: { id: 'ada', key: 1, name: 'Ada Lovelace' },
          workingDayHours: 6,
          allocatedPersonDays: 0,
          deliveryPersonDays: 0,
          elsewherePersonDays: 0,
          awayPersonDays: 0,
          timeAway: [],
          allocations: [],
          elsewhereAllocations: [],
        },
      ],
    },
    days: WORKING_DAYS,
    teamDaysOff: [],
    people: [
      {
        employeeId: 'ada',
        canManageTimeAway: true,
        days: [
          {
            date: '2027-02-04',
            isHoliday: true,
            percentAway: 0,
            elsewherePercent: 0,
          },
          {
            date: '2027-02-05',
            isHoliday: false,
            percentAway: 50,
            elsewherePercent: 40,
          },
        ],
        rows: [
          {
            role: { id: 'engineer', key: 7, name: 'Engineer' },
            countsTowardDeliveryCapacity: true,
            days: WORKING_DAYS.map((date) => ({
              date,
              percent: date < '2027-02-08' ? 60 : 50,
              personDays:
                date === '2027-02-04' ? 0 : date < '2027-02-08' ? 0.6 : 0.5,
            })),
          },
          {
            countsTowardDeliveryCapacity: true,
            days: [{ date: '2027-02-03', percent: 20, personDays: 0.2 }],
          },
        ],
      },
    ],
    ...overrides,
  }
}

describe('planWeeks', () => {
  it('counts weeks of seven days from the first day, the last ending with the plan', () => {
    const weeks = planWeeks(FROM, TO, WORKING_DAYS)

    expect(weeks.map((w) => [w.start, w.end])).toEqual([
      ['2027-02-03', '2027-02-09'],
      ['2027-02-10', '2027-02-16'],
    ])
    expect(weeks[0].days).toEqual([
      '2027-02-03',
      '2027-02-04',
      '2027-02-05',
      '2027-02-08',
      '2027-02-09',
    ])
    expect(weeks.map((w) => w.index)).toEqual([0, 1])
  })

  it('ends a last part week on the plan’s last day', () => {
    const weeks = planWeeks(FROM, '2027-02-11', WORKING_DAYS)

    expect(weeks.map((w) => [w.start, w.end])).toEqual([
      ['2027-02-03', '2027-02-09'],
      ['2027-02-10', '2027-02-11'],
    ])
    expect(weeks[1].days).toEqual(['2027-02-10', '2027-02-11'])
  })

  it('has one week for a plan of one day', () => {
    const weeks = planWeeks(FROM, FROM, WORKING_DAYS)

    expect(weeks).toEqual([{ index: 0, start: FROM, end: FROM, days: [FROM] }])
  })
})

describe('planRows', () => {
  it('makes a row per person and role, named from the capacity', () => {
    const rows = planRows(plan())

    expect(rows.map((r) => [r.id, r.employee.name, r.role?.name])).toEqual([
      ['ada|engineer', 'Ada Lovelace', 'Engineer'],
      ['ada|', 'Ada Lovelace', undefined],
    ])
    expect(rows[0].percentByDate.get('2027-02-08')).toBe(50)
    expect(rows.map((r) => r.isFirstOfPerson)).toEqual([true, false])
    expect(rows.every((r) => r.canManageTimeAway)).toBe(true)
    expect(rows[1].conditionsByDate.get('2027-02-04')?.isHoliday).toBe(true)
  })

  it("carries the person's own working day on each of their rows", () => {
    const rows = planRows(plan())

    expect(rows.map((r) => r.workingDayHours)).toEqual([6, 6])
  })
})

describe('planCell', () => {
  it('reads the percents, person-days and conditions over its days', () => {
    const [row] = planRows(plan())

    const cell = planCell(row, planWeeks(FROM, TO, WORKING_DAYS)[0].days)

    expect(cell).toEqual({
      minPercent: 50,
      maxPercent: 60,
      personDays: expect.closeTo(2.2),
      holidays: 1,
      awayDays: 1,
      maxElsewherePercent: 40,
    })
    expect(percentLabel(cell)).toBe('50–60%')
  })

  it('reads in hours of the working day, as one figure or a range', () => {
    const [row] = planRows(plan())

    expect(hoursLabel(planCell(row, ['2027-02-10', '2027-02-11']), 8)).toBe(
      '4 h/day',
    )
    expect(
      hoursLabel(planCell(row, planWeeks(FROM, TO, WORKING_DAYS)[0].days), 8),
    ).toBe('4–4.8 h/day')
  })

  it('reads a day with nothing allocated as 0%', () => {
    const [, noRole] = planRows(plan())

    const cell = planCell(noRole, ['2027-02-08'])

    expect(percentLabel(cell)).toBe('0%')
    expect(cell.personDays).toBe(0)
  })

  it('shows one percent when every day has it', () => {
    const [row] = planRows(plan())

    expect(percentLabel(planCell(row, ['2027-02-10', '2027-02-11']))).toBe(
      '50%',
    )
  })
})

describe('rowChanges', () => {
  const weeks = planWeeks(FROM, TO, WORKING_DAYS)

  it('sets each edited week over all seven of its days and no others', () => {
    const [row] = planRows(plan())

    const changes = rowChanges(
      row,
      weeks,
      { [weekEditKey(weeks[0])]: 100, [weekEditKey(weeks[1])]: 0 },
      TEAM,
    )

    expect(changes).toEqual([
      {
        teamId: TEAM,
        roleId: 'engineer',
        start: '2027-02-03',
        end: '2027-02-09',
        percent: 100,
      },
      {
        teamId: TEAM,
        roleId: 'engineer',
        start: '2027-02-10',
        end: '2027-02-16',
        percent: 0,
      },
    ])
  })

  it('sets an edited day on that day only, after the week it falls in', () => {
    const [row] = planRows(plan())

    const changes = rowChanges(
      row,
      weeks,
      { [dayEditKey('2027-02-11')]: 0, [weekEditKey(weeks[1])]: 80 },
      TEAM,
    )

    expect(changes).toEqual([
      {
        teamId: TEAM,
        roleId: 'engineer',
        start: '2027-02-10',
        end: '2027-02-16',
        percent: 80,
      },
      {
        teamId: TEAM,
        roleId: 'engineer',
        start: '2027-02-11',
        end: '2027-02-11',
        percent: 0,
      },
    ])
  })

  it('leaves out a day set to the percent its edited week now gives it', () => {
    const [row] = planRows(plan())

    const changes = rowChanges(
      row,
      weeks,
      { [weekEditKey(weeks[1])]: 80, [dayEditKey('2027-02-11')]: 80 },
      TEAM,
    )

    expect(changes).toEqual([
      {
        teamId: TEAM,
        roleId: 'engineer',
        start: '2027-02-10',
        end: '2027-02-16',
        percent: 80,
      },
    ])
  })

  it('changes nothing for a cell that already reads the entered percent', () => {
    const [row] = planRows(plan())

    const changes = rowChanges(
      row,
      weeks,
      {
        [weekEditKey(weeks[1])]: 50,
        [dayEditKey('2027-02-03')]: 60,
      },
      TEAM,
    )

    expect(changes).toEqual([])
  })

  it('sets a week whose days differ even to a percent one of them has', () => {
    const [row] = planRows(plan())

    const changes = rowChanges(
      row,
      weeks,
      { [weekEditKey(weeks[0])]: 60 },
      TEAM,
    )

    expect(changes).toHaveLength(1)
    expect(changes[0]).toMatchObject({
      start: '2027-02-03',
      end: '2027-02-09',
      percent: 60,
    })
  })

  it('leaves out the role for time in no role', () => {
    const [, noRole] = planRows(plan())

    const [change] = rowChanges(
      noRole,
      weeks,
      { [dayEditKey('2027-02-08')]: 30 },
      TEAM,
    )

    expect(change.roleId).toBeUndefined()
  })
})

describe('planRequests', () => {
  it('gathers each person’s changes across their roles into one request', () => {
    const rows = planRows(plan())
    const weeks = planWeeks(FROM, TO, WORKING_DAYS)

    const requests = planRequests(
      rows,
      weeks,
      {
        'ada|engineer': { [weekEditKey(weeks[0])]: 100 },
        'ada|': { [dayEditKey('2027-02-03')]: 0 },
      },
      TEAM,
    )

    expect(requests).toHaveLength(1)
    expect(requests[0].employeeId).toBe('ada')
    expect(
      requests[0].request.changes.map((c) => [c.roleId, c.percent]),
    ).toEqual([
      ['engineer', 100],
      [undefined, 0],
    ])
  })

  it('has no request for a person whose edits change nothing', () => {
    const rows = planRows(plan())
    const weeks = planWeeks(FROM, TO, WORKING_DAYS)

    const requests = planRequests(
      rows,
      weeks,
      { 'ada|engineer': { [weekEditKey(weeks[1])]: 50 } },
      TEAM,
    )

    expect(requests).toEqual([])
  })
})

describe('pending edits', () => {
  const weeks = planWeeks(FROM, TO, WORKING_DAYS)

  it("reads a day as its own edit, else its week's, else what it has", () => {
    const [row] = planRows(plan())
    const edits = {
      [weekEditKey(weeks[1])]: 80,
      [dayEditKey('2027-02-11')]: 0,
    }

    expect(pendingPercent(row, weeks[1], '2027-02-11', edits)).toBe(0)
    expect(pendingPercent(row, weeks[1], '2027-02-10', edits)).toBe(80)
    expect(pendingPercent(row, weeks[0], '2027-02-03', edits)).toBe(60)
    expect(pendingPercent(row, weeks[0], '2027-02-03')).toBe(60)
  })

  it('reads a day nothing is allocated on and nothing edits as 0', () => {
    const [, noRole] = planRows(plan())

    expect(pendingPercent(noRole, weeks[1], '2027-02-10', {})).toBe(0)
  })

  it('marks a week edited when it or any of its days is', () => {
    expect(weekIsEdited(weeks[1], { [weekEditKey(weeks[1])]: 0 })).toBe(true)
    expect(weekIsEdited(weeks[1], { [dayEditKey('2027-02-12')]: 0 })).toBe(true)
    expect(weekIsEdited(weeks[1], { [dayEditKey('2027-02-03')]: 0 })).toBe(
      false,
    )
    expect(weekIsEdited(weeks[1], {})).toBe(false)
  })

  it("replaces a week's day edits when the week is set, and keeps other weeks'", () => {
    const edits = withCellEdit(
      {
        [dayEditKey('2027-02-11')]: 0,
        [dayEditKey('2027-02-03')]: 10,
      },
      weeks[1],
      undefined,
      70,
    )

    expect(edits).toEqual({
      [weekEditKey(weeks[1])]: 70,
      [dayEditKey('2027-02-03')]: 10,
    })
  })

  it("keeps the week's edit when one of its days is set", () => {
    const edits = withCellEdit(
      { [weekEditKey(weeks[1])]: 70 },
      weeks[1],
      '2027-02-11',
      0,
    )

    expect(edits).toEqual({
      [weekEditKey(weeks[1])]: 70,
      [dayEditKey('2027-02-11')]: 0,
    })
  })

  it('clears a cell whose input is emptied', () => {
    const edits = withCellEdit(
      { [dayEditKey('2027-02-11')]: 0, [weekEditKey(weeks[0])]: 50 },
      weeks[1],
      '2027-02-11',
      undefined,
    )

    expect(edits).toEqual({ [weekEditKey(weeks[0])]: 50 })
  })
})
