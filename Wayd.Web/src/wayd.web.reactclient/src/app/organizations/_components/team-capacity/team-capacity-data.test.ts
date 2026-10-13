import {
  capacityRows,
  clipToRange,
  formatPersonDays,
  monthOf,
  overProposed,
  proposalRequests,
  roleLines,
} from './team-capacity-data'

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
      allocatedPersonDays: 8,
      deliveryPersonDays: 5,
      elsewherePersonDays: 2,
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
      allocatedPersonDays: 10,
      deliveryPersonDays: 10,
      elsewherePersonDays: 0,
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

describe('clipToRange', () => {
  const range = { from: '2027-02-01', to: '2027-02-28' }

  it.each([
    ['2027-02-15', '2027-02-20', '2027-02-15', '2027-02-20'],
    ['2027-01-01', undefined, '2027-02-01', '2027-02-28'],
    ['2027-01-01', '2027-02-10', '2027-02-01', '2027-02-10'],
    ['2027-02-10', '2027-06-30', '2027-02-10', '2027-02-28'],
  ])(
    'keeps the days of %s to %s that fall in the range',
    (start, end, expectedStart, expectedEnd) => {
      expect(clipToRange({ start, end }, range)).toEqual({
        start: expectedStart,
        end: expectedEnd,
      })
    },
  )
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
