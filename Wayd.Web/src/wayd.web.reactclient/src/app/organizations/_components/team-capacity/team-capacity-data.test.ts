import {
  capacityRows,
  formatPeriods,
  formatPersonDays,
  cadencePeriodOf,
  monthOf,
  overProposed,
  proposalRequests,
  roleLines,
  rollupRows,
  timeAwayLines,
} from './team-capacity-data'

jest.unmock('dayjs')

describe('monthOf', () => {
  it.each([
    ['2027-02-14', '2027-02-01', '2027-02-28'],
    ['2028-02-01', '2028-02-01', '2028-02-29'],
    ['2027-12-31', '2027-12-01', '2027-12-31'],
    ['2027-09-30', '2027-09-01', '2027-09-30'],
  ])('spans the whole month %s falls in', (today, from, to) => {
    expect(monthOf(today)).toEqual({ from, to })
  })
})

describe('cadencePeriodOf', () => {
  const cadence = { weeks: 2, anchor: '2026-10-05' }

  it.each([
    ['2026-10-05', '2026-10-05', '2026-10-18'],
    ['2026-10-18', '2026-10-05', '2026-10-18'],
    ['2026-10-19', '2026-10-19', '2026-11-01'],
    ['2026-10-04', '2026-09-21', '2026-10-04'],
  ])('spans the whole two-week period %s falls in', (today, from, to) => {
    expect(cadencePeriodOf(today, cadence)).toEqual({ from, to })
  })
})

describe('formatPersonDays', () => {
  it.each([
    [5, '5'],
    [4.5, '4.5'],
    [7.2, '7.2'],
    [1.005, '1'],
    [2.256, '2.26'],
  ])('shows %s as %s', (personDays, expected) => {
    expect(formatPersonDays(personDays)).toBe(expected)
  })
})

describe('capacityRows', () => {
  it('is a row per allocation, each carrying its person', () => {
    const ada = {
      employee: { id: 'e-1', key: 1, name: 'Ada Lovelace' },
      workingDayHours: 8,
      allocatedPersonDays: 8,
      deliveryPersonDays: 5,
      elsewherePersonDays: 2,
      awayPersonDays: 0,
      timeAway: [],
      allocations: [
        {
          id: 'a-1',
          role: { id: 'r-1', key: 1, name: 'Engineer' },
          countsTowardDeliveryCapacity: true,
          start: '2027-01-01',
          end: undefined,
          percent: 50,
          personDays: 5,
        },
        {
          id: 'a-2',
          role: { id: 'r-2', key: 2, name: 'Lead' },
          countsTowardDeliveryCapacity: false,
          start: '2027-01-01',
          end: '2027-06-30',
          percent: 30,
          personDays: 3,
        },
      ],
      elsewhereAllocations: [],
    }
    const grace = {
      employee: { id: 'e-2', key: 2, name: 'Grace Hopper' },
      workingDayHours: 8,
      allocatedPersonDays: 10,
      deliveryPersonDays: 10,
      elsewherePersonDays: 0,
      awayPersonDays: 0,
      timeAway: [],
      allocations: [
        {
          id: 'a-3',
          role: undefined,
          countsTowardDeliveryCapacity: true,
          start: '2027-02-01',
          end: undefined,
          percent: 100,
          personDays: 10,
        },
      ],
      elsewhereAllocations: [],
    }

    const rows = capacityRows([ada, grace])

    expect(rows.map((r) => [r.id, r.person.employee.id, r.percent])).toEqual([
      ['a-1', 'e-1', 50],
      ['a-2', 'e-1', 30],
      ['a-3', 'e-2', 100],
    ])
    expect(rows[1]).toMatchObject({
      role: { name: 'Lead' },
      countsTowardDeliveryCapacity: false,
      end: '2027-06-30',
      personDays: 3,
    })
  })
})

describe('timeAwayLines', () => {
  it('says when a person is away and for how long each day, a line per stretch', () => {
    const lines = timeAwayLines(
      [
        { start: '2027-02-01', end: '2027-02-05', percentAway: 100 },
        { start: '2027-02-10', end: '2027-02-10', percentAway: 25 },
      ],
      8,
    )

    expect(lines).toHaveLength(2)
    expect(lines[0]).toMatch(/ to .*: Full day$/)
    expect(lines[1]).not.toContain(' to ')
    expect(lines[1]).toMatch(/: 2 h$/)
  })

  it('is empty when the person has no time away', () => {
    expect(timeAwayLines([], 8)).toEqual([])
  })
})

describe('roleLines', () => {
  it('lists delivery roles and gathers the rest into one non-delivery line', () => {
    const lines = roleLines([
      {
        role: { id: 'r-1', key: 1, name: 'Engineer' },
        countsTowardDeliveryCapacity: true,
        personDays: 9.5,
      },
      {
        role: { id: 'r-2', key: 2, name: 'Lead' },
        countsTowardDeliveryCapacity: false,
        personDays: 2.7,
      },
      {
        role: { id: 'r-3', key: 3, name: 'Coach' },
        countsTowardDeliveryCapacity: false,
        personDays: 1.3,
      },
      { role: undefined, countsTowardDeliveryCapacity: true, personDays: 5 },
    ])

    expect(lines).toEqual([
      { key: 'r-1', label: 'Engineer', personDays: 9.5, delivery: true },
      { key: 'no-role', label: 'No role', personDays: 5, delivery: true },
      {
        key: 'non-delivery',
        label: 'Non-delivery',
        personDays: 4,
        delivery: false,
      },
    ])
  })

  it('has no non-delivery line when every role counts', () => {
    const lines = roleLines([
      {
        role: { id: 'r-1', key: 1, name: 'Engineer' },
        countsTowardDeliveryCapacity: true,
        personDays: 3,
      },
    ])

    expect(lines.map((l) => l.label)).toEqual(['Engineer'])
  })
})

describe('proposalRequests', () => {
  it('is one request per person, open-ended from the first day, without the rows at 0%', () => {
    const requests = proposalRequests(
      [
        { employeeId: 'e-1', roleId: 'r-1', percent: 28 },
        { employeeId: 'e-2', roleId: 'r-1', percent: 100 },
        { employeeId: 'e-1', roleId: 'r-2', percent: 27 },
        { employeeId: 'e-3', roleId: 'r-1', percent: 0 },
      ],
      'team-1',
      '2027-02-01',
    )

    expect(requests).toEqual([
      {
        employeeId: 'e-1',
        request: {
          changes: [
            {
              teamId: 'team-1',
              roleId: 'r-1',
              start: '2027-02-01',
              end: undefined,
              percent: 28,
            },
            {
              teamId: 'team-1',
              roleId: 'r-2',
              start: '2027-02-01',
              end: undefined,
              percent: 27,
            },
          ],
        },
      },
      {
        employeeId: 'e-2',
        request: {
          changes: [
            {
              teamId: 'team-1',
              roleId: 'r-1',
              start: '2027-02-01',
              end: undefined,
              percent: 100,
            },
          ],
        },
      },
    ])
  })
})

describe('proposalRequests with an end', () => {
  it('ends a row on the day it is given', () => {
    const requests = proposalRequests(
      [{ employeeId: 'e-1', roleId: 'r-1', end: '2027-02-28', percent: 40 }],
      'team-1',
      '2027-02-01',
    )

    expect(requests[0].request.changes).toEqual([
      {
        teamId: 'team-1',
        roleId: 'r-1',
        start: '2027-02-01',
        end: '2027-02-28',
        percent: 40,
      },
    ])
  })
})

describe('overProposed', () => {
  it('names only the people whose rows add up to more than they have left', () => {
    const over = overProposed([
      { employeeId: 'e-1', roleId: 'r-1', percent: 30, remainingPercent: 55 },
      { employeeId: 'e-1', roleId: 'r-2', percent: 26, remainingPercent: 55 },
      { employeeId: 'e-2', roleId: 'r-1', percent: 50, remainingPercent: 50 },
    ])

    expect([...over]).toEqual(['e-1'])
  })
})

describe('rollupRows', () => {
  const person = (id: string, name: string, allocated: number) => ({
    employee: { id, key: Number(id.slice(2)), name },
    allocatedPersonDays: allocated,
    deliveryPersonDays: allocated / 2,
    allocations: [],
  })

  const node = (
    id: string,
    key: number,
    name: string,
    type: string,
    rest: { people?: unknown[]; children?: unknown[] } = {},
  ) =>
    ({
      team: { id, key, name, code: name.toUpperCase(), type },
      periods: [{ start: '2027-02-08', end: '2027-02-14' }],
      workingDays: 5,
      allocatedPersonDays: 9,
      deliveryPersonDays: 7,
      directAllocatedPersonDays: 2,
      directDeliveryPersonDays: 0,
      people: [],
      children: [],
      ...rest,
    }) as never

  it('puts the people allocated to a team under it, ahead of the teams beneath it', () => {
    const rows = rollupRows([
      node('t-1', 1, 'Lending', 'Team of Teams', {
        people: [person('e-7', 'Grace Hopper', 2)],
        children: [
          node('t-2', 2, 'Loans', 'Team', {
            people: [person('e-8', 'Ada Lovelace', 4)],
          }),
        ],
      }),
    ])

    expect(rows).toHaveLength(1)
    expect(rows[0]).toMatchObject({
      kind: 'team',
      name: 'Lending',
      type: 'Team of Teams',
      href: '/organizations/team-of-teams/1',
      workingDays: 5,
      allocatedPersonDays: 9,
      deliveryPersonDays: 7,
    })
    expect(rows[0].children.map((c) => [c.kind, c.name, c.href])).toEqual([
      ['person', 'Grace Hopper', '/organizations/employees/7'],
      ['team', 'Loans', '/organizations/teams/2'],
    ])
    expect(rows[0].children[0]).toMatchObject({
      type: 'Person',
      allocatedPersonDays: 2,
      deliveryPersonDays: 1,
    })
    expect(rows[0].children[0].periods).toBeUndefined()
    expect(rows[0].children[0].workingDays).toBeUndefined()
    expect(rows[0].children[1].children.map((c) => c.name)).toEqual([
      'Ada Lovelace',
    ])
  })

  it('gives a team that appears under two parents, and a person on two teams, a different id each time', () => {
    const moved = () =>
      node('t-9', 9, 'Cards', 'Team', {
        people: [person('e-7', 'Grace Hopper', 2)],
      })
    const rows = rollupRows([
      node('t-1', 1, 'Lending', 'Team of Teams', { children: [moved()] }),
      node('t-2', 2, 'Retail', 'Team of Teams', { children: [moved()] }),
    ])

    const ids = rows.flatMap((r) => [
      r.id,
      ...r.children.flatMap((c) => [c.id, ...c.children.map((p) => p.id)]),
    ])
    expect(ids).toHaveLength(6)
    expect(new Set(ids).size).toBe(6)
  })
})

describe('formatPeriods', () => {
  const range = { from: '2027-02-01', to: '2027-02-28' }

  it('says so when the team was in the hierarchy for the whole range', () => {
    expect(
      formatPeriods([{ start: '2027-02-01', end: '2027-02-28' }], range),
    ).toBe('Whole range')
  })

  it('lists each run of days, a single day on its own', () => {
    const text = formatPeriods(
      [
        { start: '2027-02-01', end: '2027-02-05' },
        { start: '2027-02-20', end: '2027-02-20' },
      ],
      range,
    )

    expect(text).toBe('Feb 1, 2027 to Feb 5, 2027; Feb 20, 2027')
  })

  it('does not call one run the whole range when it only starts with it', () => {
    expect(
      formatPeriods([{ start: '2027-02-01', end: '2027-02-05' }], range),
    ).not.toBe('Whole range')
  })
})
