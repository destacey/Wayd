import type { EmployeeAllocationDto } from '@/src/store/features/organization/employee-allocations-api'
import {
  allocationTimelineData,
  TOTAL_GROUP_ID,
} from './allocation-timeline-data'

const colors = { allocation: 'blue', partTotal: 'grey', fullTotal: 'green' }

const day = (year: number, month: number, date: number) =>
  new Date(year, month - 1, date).getTime()

const allocation = (
  id: string,
  team: { id: string; name: string },
  start: string,
  end: string | undefined,
  percent: number,
  role?: string,
): EmployeeAllocationDto => ({
  id,
  team: { ...team, key: 1, code: team.name.toUpperCase(), type: 'Team' },
  role: role ? { id: `role-${role}`, key: 1, name: role } : undefined,
  start,
  end,
  percent,
  canManage: true,
})

const cards = { id: 'cards', name: 'Cards' }
const loans = { id: 'loans', name: 'Loans' }

describe('allocationTimelineData', () => {
  it('has a Total lane first, then a lane per team by name', () => {
    const data = allocationTimelineData(
      [
        allocation('a', loans, '2027-01-01', undefined, 40),
        allocation('b', cards, '2027-01-01', undefined, 60),
      ],
      '2027-01-15',
      colors,
    )

    expect(data.groups.map((g) => g.label)).toEqual(['Total', 'Cards', 'Loans'])
    expect(data.groups[0].id).toBe(TOTAL_GROUP_ID)
  })

  it('draws an allocation on its team lane, ending the day after its last day', () => {
    const data = allocationTimelineData(
      [allocation('a', cards, '2027-02-01', '2027-02-07', 50, 'Engineer')],
      '2027-01-15',
      colors,
    )

    const bar = data.items.find((i) => i.id === 'a')!
    expect(bar.groupId).toBe('cards')
    expect(bar.start).toBe(day(2027, 2, 1))
    expect(bar.end).toBe(day(2027, 2, 8))
    expect(bar.label).toBe('50% Engineer')
    expect(bar.color).toBe('blue')
  })

  it('draws an allocation with no end to the edge of the timeline', () => {
    const data = allocationTimelineData(
      [allocation('a', cards, '2027-01-01', undefined, 50)],
      '2027-01-15',
      colors,
    )

    expect(data.items.find((i) => i.id === 'a')!.end).toBe(data.maxDate)
  })

  it('adds the teams up on the Total lane, marking stretches that are full', () => {
    const data = allocationTimelineData(
      [
        allocation('a', cards, '2027-01-01', undefined, 60),
        allocation('b', loans, '2027-02-01', '2027-02-07', 40),
      ],
      '2027-01-15',
      colors,
    )

    const totals = data.items.filter((i) => i.groupId === TOTAL_GROUP_ID)
    expect(totals.map((t) => [t.label, t.color])).toEqual([
      ['60%', 'grey'],
      ['100%', 'green'],
      ['60%', 'grey'],
    ])
    expect(totals[1].start).toBe(day(2027, 2, 1))
    expect(totals[1].end).toBe(day(2027, 2, 8))
  })

  it('leaves unallocated stretches off the Total lane', () => {
    const data = allocationTimelineData(
      [allocation('a', cards, '2027-02-01', '2027-02-07', 50)],
      '2027-01-15',
      colors,
    )

    expect(data.items.filter((i) => i.groupId === TOTAL_GROUP_ID)).toHaveLength(
      1,
    )
  })

  it('opens on a window around today and reaches back to the first allocation', () => {
    const data = allocationTimelineData(
      [allocation('a', cards, '2025-06-01', '2025-12-31', 50)],
      '2027-01-31',
      colors,
    )

    expect(data.windowStart).toBe(day(2027, 1, 1))
    expect(data.windowEnd).toBe(day(2027, 7, 30))
    expect(data.minDate).toBe(day(2025, 5, 2))
    expect(data.minDate).toBeLessThan(data.windowStart)
    expect(data.maxDate).toBeGreaterThan(data.windowEnd)
  })
})
