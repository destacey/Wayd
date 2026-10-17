import { TeamCapacityReportDto } from '@/src/store/features/organization/team-capacity-api'
import {
  formatCapacity,
  grandTotal,
  Measures,
  previousMonthOf,
  quarterOf,
  rowsByPerson,
  rowsByTeam,
} from './capacity-report-data'

const measures = (available: number, delivery = available): Measures => ({
  allocatedPersonDays: available + 1,
  holidayPersonDays: 0.5,
  timeAwayPersonDays: 0.5,
  availablePersonDays: available,
  deliveryPersonDays: delivery,
})

const team = (id: string, key: number, name: string, type = 'Team') => ({
  id,
  key,
  name,
  code: name.toUpperCase(),
  type,
})

const person = (id: string, key: number, name: string) => ({
  employee: { id, key, name },
  outsidePersonDays: key,
  unallocatedPersonDays: key * 2,
  outsideBuckets: [],
  unallocatedBuckets: [],
})

// Payments (a team of teams) holds Cards and Lending (another team of teams),
// which holds Loans. Ada has time on Payments itself and on Cards; Grace on
// Cards and Loans.
const report: TeamCapacityReportDto = {
  from: '2027-02-01',
  to: '2027-02-14',
  buckets: [
    { start: '2027-02-01', end: '2027-02-07' },
    { start: '2027-02-08', end: '2027-02-14' },
  ],
  workingDayHours: 8,
  total: measures(14, 12),
  roles: [],
  teams: [
    {
      nodeId: 0,
      team: team('pay', 1, 'Payments', 'Team of Teams'),
      periods: [],
      workingDays: 10,
    },
    {
      nodeId: 1,
      parentNodeId: 0,
      team: team('cards', 2, 'Cards'),
      periods: [],
      workingDays: 10,
    },
    {
      nodeId: 2,
      parentNodeId: 0,
      team: team('lend', 3, 'Lending', 'Team of Teams'),
      periods: [],
      workingDays: 5,
    },
    {
      nodeId: 3,
      parentNodeId: 2,
      team: team('loans', 4, 'Loans'),
      periods: [],
      workingDays: 5,
    },
  ],
  people: [
    person('ada', 10, 'Ada Lovelace'),
    person('grace', 11, 'Grace Hopper'),
  ],
  lines: [
    {
      nodeId: 0,
      employeeId: 'ada',
      countsTowardDeliveryCapacity: true,
      total: measures(4),
      buckets: [measures(2), measures(2)],
    },
    {
      nodeId: 1,
      employeeId: 'ada',
      role: { id: 'eng', key: 1, name: 'Engineer' },
      countsTowardDeliveryCapacity: true,
      total: measures(3),
      buckets: [measures(1), measures(2)],
    },
    {
      nodeId: 1,
      employeeId: 'grace',
      role: { id: 'lead', key: 2, name: 'Lead' },
      countsTowardDeliveryCapacity: false,
      total: measures(2, 0),
      buckets: [measures(1, 0), measures(1, 0)],
    },
    {
      nodeId: 3,
      employeeId: 'grace',
      role: { id: 'eng', key: 1, name: 'Engineer' },
      countsTowardDeliveryCapacity: true,
      total: measures(5),
      buckets: [measures(0), measures(5)],
    },
  ],
}

describe('rowsByTeam', () => {
  it('nests every team beneath its parent and sets a team of teams own people apart', () => {
    // Act
    const rows = rowsByTeam(report)

    // Assert
    expect(rows).toHaveLength(1)
    const payments = rows[0]
    expect(payments.name).toBe('Payments')
    expect(payments.href).toBe('/organizations/team-of-teams/1')
    expect(payments.children!.map((r) => [r.kind, r.name])).toEqual([
      ['direct', 'Allocated directly'],
      ['team', 'Cards'],
      ['team', 'Lending'],
    ])
    expect(payments.children![0].children!.map((r) => r.name)).toEqual([
      'Ada Lovelace',
    ])
    const lending = payments.children![2]
    expect(lending.children!.map((r) => r.name)).toEqual(['Loans'])
    expect(lending.children![0].children!.map((r) => r.name)).toEqual([
      'Grace Hopper',
    ])
  })

  it('adds each team up from everything beneath it, bucket by bucket', () => {
    // Act
    const [payments] = rowsByTeam(report)

    // Assert
    expect(payments.total.availablePersonDays).toBe(14)
    expect(payments.total.deliveryPersonDays).toBe(12)
    expect(payments.buckets.map((b) => b.availablePersonDays)).toEqual([4, 10])
    const cards = payments.children![1]
    expect(cards.total.availablePersonDays).toBe(5)
    expect(cards.children!.map((r) => [r.name, r.roles])).toEqual([
      ['Ada Lovelace', 'Engineer'],
      ['Grace Hopper', 'Lead'],
    ])
  })

  it('lists a team without teams beneath it with its people directly', () => {
    // Arrange
    const single: TeamCapacityReportDto = {
      ...report,
      teams: [report.teams[1]].map((t) => ({
        ...t,
        nodeId: 0,
        parentNodeId: undefined,
      })),
      lines: report.lines
        .filter((l) => l.nodeId === 1)
        .map((l) => ({ ...l, nodeId: 0 })),
    }

    // Act
    const rows = rowsByTeam(single)

    // Assert
    expect(rows[0].children!.map((r) => [r.kind, r.name])).toEqual([
      ['person', 'Ada Lovelace'],
      ['person', 'Grace Hopper'],
    ])
  })

  it('gives every row the path from the top row down to it, for the export', () => {
    // Act
    const [payments] = rowsByTeam(report)

    // Assert
    const [direct, cards, lending] = payments.children!
    expect(payments.path).toBe('Payments')
    expect(direct.children![0].path).toBe(
      'Payments > Allocated directly > Ada Lovelace',
    )
    expect(cards.children!.map((r) => r.path)).toEqual([
      'Payments > Cards > Ada Lovelace',
      'Payments > Cards > Grace Hopper',
    ])
    expect(lending.children![0].children![0].path).toBe(
      'Payments > Lending > Loans > Grace Hopper',
    )
  })
})

describe('rowsByPerson', () => {
  it('gives each team beneath a person the path from that person', () => {
    // Act
    const [ada] = rowsByPerson(report)

    // Assert
    expect(ada.path).toBe('Ada Lovelace')
    expect(ada.children!.map((r) => r.path)).toEqual([
      'Ada Lovelace > Payments',
      'Ada Lovelace > Cards',
    ])
  })

  it('gives each person every team they have time on, with their time outside and unallocated', () => {
    // Act
    const rows = rowsByPerson(report)

    // Assert
    expect(rows.map((r) => r.name)).toEqual(['Ada Lovelace', 'Grace Hopper'])
    const grace = rows[1]
    expect(
      grace.children!.map((r) => [r.name, r.total.availablePersonDays]),
    ).toEqual([
      ['Cards', 2],
      ['Loans', 5],
    ])
    expect(grace.total.availablePersonDays).toBe(7)
    expect(grace.roles).toBe('Lead, Engineer')
    expect(grace.outsidePersonDays).toBe(11)
    expect(grace.unallocatedPersonDays).toBe(22)
  })

  it('counts a team that sits at two places in the hierarchy once under a person', () => {
    // Arrange — Cards also appears beneath Lending, as a node of its own
    const moved: TeamCapacityReportDto = {
      ...report,
      teams: [
        ...report.teams,
        {
          nodeId: 4,
          parentNodeId: 2,
          team: team('cards', 2, 'Cards'),
          periods: [],
          workingDays: 2,
        },
      ],
      lines: [
        ...report.lines,
        {
          nodeId: 4,
          employeeId: 'ada',
          countsTowardDeliveryCapacity: true,
          total: measures(1),
          buckets: [measures(0), measures(1)],
        },
      ],
    }

    // Act
    const ada = rowsByPerson(moved)[0]

    // Assert
    expect(
      ada.children!.map((r) => [r.name, r.total.availablePersonDays]),
    ).toEqual([
      ['Payments', 4],
      ['Cards', 4],
    ])
  })
})

describe('grandTotal', () => {
  it('is the same whether grouped by team or by person', () => {
    // Act
    const byTeam = grandTotal(rowsByTeam(report))
    const byPerson = grandTotal(rowsByPerson(report))

    // Assert
    expect(byTeam).toEqual(byPerson)
    expect(byTeam.availablePersonDays).toBe(14)
    expect(byTeam.allocatedPersonDays).toBe(18)
  })
})

describe('formatCapacity', () => {
  it.each([
    [2.5, 'days', '2.5'],
    [2.5, 'hours', '20'],
    [1 / 3, 'days', '0.33'],
  ] as const)(
    'shows %d person-days in %s as %s',
    (personDays, unit, expected) => {
      // Act
      const text = formatCapacity(personDays, unit, 8)

      // Assert
      expect(text).toBe(expected)
    },
  )
})

describe('previousMonthOf', () => {
  it.each([
    ['2027-03-15', '2027-02-01', '2027-02-28'],
    ['2027-01-01', '2026-12-01', '2026-12-31'],
  ])('for %s is %s to %s', (today, from, to) => {
    // Act
    const range = previousMonthOf(today)

    // Assert
    expect(range).toEqual({ from, to })
  })
})

describe('quarterOf', () => {
  it.each([
    ['2027-05-20', 0, '2027-04-01', '2027-06-30'],
    ['2027-11-02', 0, '2027-10-01', '2027-12-31'],
    ['2027-02-10', -1, '2026-10-01', '2026-12-31'],
  ])('for %s offset %d is %s to %s', (today, offset, from, to) => {
    // Act
    const range = quarterOf(today, offset)

    // Assert
    expect(range).toEqual({ from, to })
  })
})
