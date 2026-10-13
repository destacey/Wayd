import { allocatedOn, allocationPeriods } from './allocation-periods'

describe('allocatedOn', () => {
  it('adds up every allocation covering the day, ends inclusive', () => {
    const allocations = [
      { start: '2027-01-01', percent: 60 },
      { start: '2027-02-01', end: '2027-02-07', percent: 40 },
    ]

    expect(allocatedOn(allocations, '2027-01-31')).toBe(60)
    expect(allocatedOn(allocations, '2027-02-01')).toBe(100)
    expect(allocatedOn(allocations, '2027-02-07')).toBe(100)
    expect(allocatedOn(allocations, '2027-02-08')).toBe(60)
    expect(allocatedOn(allocations, '2026-12-31')).toBe(0)
  })
})

describe('allocationPeriods', () => {
  it('is one unallocated period when there are no allocations', () => {
    expect(allocationPeriods([], '2027-01-01')).toEqual([
      { start: '2027-01-01', percent: 0 },
    ])
  })

  it('starts a period wherever the total changes', () => {
    const allocations = [
      { start: '2027-01-01', percent: 60 },
      { start: '2027-02-01', end: '2027-02-07', percent: 40 },
    ]

    expect(allocationPeriods(allocations, '2027-01-15')).toEqual([
      { start: '2027-01-15', end: '2027-01-31', percent: 60 },
      { start: '2027-02-01', end: '2027-02-07', percent: 100 },
      { start: '2027-02-08', percent: 60 },
    ])
  })

  it('joins stretches whose total is the same across teams', () => {
    const allocations = [
      { start: '2027-01-01', end: '2027-01-31', percent: 50 },
      { start: '2027-02-01', percent: 50 },
    ]

    expect(allocationPeriods(allocations, '2027-01-01')).toEqual([
      { start: '2027-01-01', percent: 50 },
    ])
  })

  it('ends with an unallocated period once every allocation has ended', () => {
    const allocations = [
      { start: '2027-01-01', end: '2027-03-31', percent: 80 },
    ]

    expect(allocationPeriods(allocations, '2027-01-01')).toEqual([
      { start: '2027-01-01', end: '2027-03-31', percent: 80 },
      { start: '2027-04-01', percent: 0 },
    ])
  })

  it('leaves out allocations that ended before the first day', () => {
    const allocations = [
      { start: '2026-01-01', end: '2026-12-31', percent: 100 },
      { start: '2027-01-01', percent: 30 },
    ]

    expect(allocationPeriods(allocations, '2027-01-10')).toEqual([
      { start: '2027-01-10', percent: 30 },
    ])
  })
})
