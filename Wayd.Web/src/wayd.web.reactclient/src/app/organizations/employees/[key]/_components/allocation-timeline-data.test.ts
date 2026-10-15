import type { EmployeeAllocationDto } from '@/src/store/features/organization/employee-allocations-api'
import { TimeAwayReason } from '@/src/services/wayd-api'
import {
  allocationTimelineData,
  TIME_AWAY_GROUP_ID,
  TOTAL_GROUP_ID,
} from './allocation-timeline-data'

const colors = {
  allocation: 'blue',
  partTotal: 'grey',
  fullTotal: 'green',
  timeAway: 'orange',
}

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
      [],
      '2027-01-15',
      colors,
    )

    expect(data.groups.map((g) => g.label)).toEqual(['Total', 'Cards', 'Loans'])
    expect(data.groups[0].id).toBe(TOTAL_GROUP_ID)
  })

  it('draws an allocation on its team lane, ending the day after its last day', () => {
    const data = allocationTimelineData(
      [allocation('a', cards, '2027-02-01', '2027-02-07', 50, 'Engineer')],
      [],
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
      [],
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
      [],
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
      [],
      '2027-01-15',
      colors,
    )

    expect(data.items.filter((i) => i.groupId === TOTAL_GROUP_ID)).toHaveLength(
      1,
    )
  })

  it('has a Time away lane after Total only when there is time away', () => {
    const allocations = [allocation('a', cards, '2027-01-01', undefined, 60)]

    const without = allocationTimelineData(
      allocations,
      [],
      '2027-01-15',
      colors,
    )
    const withTimeAway = allocationTimelineData(
      allocations,
      [{ id: 't1', start: '2027-02-01', end: '2027-02-05', percentAway: 100 }],
      '2027-01-15',
      colors,
    )

    expect(without.groups.map((g) => g.label)).toEqual(['Total', 'Cards'])
    expect(withTimeAway.groups.map((g) => g.label)).toEqual([
      'Total',
      'Time away',
      'Cards',
    ])
    expect(withTimeAway.groups.map((g) => g.order)).toEqual([0, 1, 2])
    expect(withTimeAway.groups[1].id).toBe(TIME_AWAY_GROUP_ID)
  })

  it('draws time away on its lane, to the day after its last day, saying how much of the day', () => {
    const data = allocationTimelineData(
      [],
      [
        { id: 't1', start: '2027-02-01', end: '2027-02-05', percentAway: 100 },
        { id: 't2', start: '2027-02-10', end: '2027-02-10', percentAway: 50 },
        { id: 't3', start: '2027-02-11', end: '2027-02-11', percentAway: 25 },
      ],
      '2027-01-15',
      colors,
      8,
    )

    const items = data.items.filter((i) => i.groupId === TIME_AWAY_GROUP_ID)
    expect(items.map((i) => i.label)).toEqual([
      'Away',
      'Away half day',
      'Away 2 h',
    ])
    expect(items[0].start).toBe(day(2027, 2, 1))
    expect(items[0].end).toBe(day(2027, 2, 6))
    expect(items[1].end).toBe(day(2027, 2, 11))
    expect(items.every((i) => i.color === 'orange')).toBe(true)
    // Not an allocation, so a click on it must not open the allocation form.
    expect(items.every((i) => i.data === undefined)).toBe(true)
  })

  it('never puts the reason for time away on the timeline', () => {
    const data = allocationTimelineData(
      [],
      [
        {
          id: 't1',
          start: '2027-02-01',
          end: '2027-02-05',
          percentAway: 100,
          reason: TimeAwayReason.BusinessTravel,
        },
      ],
      '2027-01-15',
      colors,
    )

    expect(JSON.stringify(data)).not.toContain('BusinessTravel')
  })

  it('reaches out to time away beyond the allocations', () => {
    const data = allocationTimelineData(
      [allocation('a', cards, '2027-01-01', '2027-03-31', 50)],
      [{ id: 't1', start: '2026-06-01', end: '2028-06-30', percentAway: 100 }],
      '2027-01-31',
      colors,
    )

    expect(data.minDate).toBe(day(2026, 5, 2))
    expect(data.maxDate).toBe(day(2028, 7, 30))
  })

  it('opens on a window around today and reaches back to the first allocation', () => {
    const data = allocationTimelineData(
      [allocation('a', cards, '2025-06-01', '2025-12-31', 50)],
      [],
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
