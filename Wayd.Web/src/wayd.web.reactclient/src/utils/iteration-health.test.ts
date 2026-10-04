import {
  calculateIterationHealth,
  IterationHealthStatus,
  sprintActiveDays,
} from './iteration-health'
import { toCalendarDate } from './calendar-date'

describe('calculateIterationHealth', () => {
  // Base sprint: 40 SP, 14 days (Jan 1-14)
  const baseParams = {
    startDate: '2024-01-01',
    endDate: '2024-01-15', // 14 days total
    total: 40,
  }

  describe('On Track scenarios', () => {
    it('should return On Track when ahead of schedule', () => {
      // Day 10: Should have ~71% done (28.6 SP), actually have 30 SP done
      const result = calculateIterationHealth({
        ...baseParams,
        completed: 30,
        referenceDate: '2024-01-11', // Day 10
      })

      expect(result.status).toBe(IterationHealthStatus.OnTrack)
      expect(result.variancePercent).toBeLessThan(0) // Ahead
    })

    it('should return On Track when exactly on ideal burndown', () => {
      // Day 7 (halfway): Should have 50% done (20 SP)
      const result = calculateIterationHealth({
        ...baseParams,
        completed: 20,
        referenceDate: '2024-01-08', // Day 7
      })

      expect(result.status).toBe(IterationHealthStatus.OnTrack)
      expect(Math.abs(result.variancePercent)).toBeLessThan(1) // Near zero
    })

    it('should return On Track when within 10% threshold', () => {
      // Day 10: ideal remaining = 40 * (4/14) = 11.4 SP
      // Actual: 26 SP done, 14 SP remaining
      // Variance: 14 - 11.4 = 2.6 SP = 6.5%
      const result = calculateIterationHealth({
        ...baseParams,
        completed: 26,
        referenceDate: '2024-01-11',
      })

      expect(result.status).toBe(IterationHealthStatus.OnTrack)
      expect(result.variancePercent).toBeLessThanOrEqual(10)
    })
  })

  describe('At Risk scenarios', () => {
    it('should return At Risk when 10-25% behind', () => {
      // Day 10: ideal remaining = 11.4 SP
      // Actual: 24 SP done, 16 SP remaining
      // Variance: 16 - 11.4 = 4.6 SP = 11.5%
      const result = calculateIterationHealth({
        ...baseParams,
        completed: 24,
        referenceDate: '2024-01-11',
      })

      expect(result.status).toBe(IterationHealthStatus.AtRisk)
      expect(result.variancePercent).toBeGreaterThan(10)
      expect(result.variancePercent).toBeLessThanOrEqual(25)
    })
  })

  describe('Off Track scenarios', () => {
    it('should return Off Track when more than 25% behind', () => {
      // Day 10: ideal remaining = 11.4 SP
      // Actual: 18 SP done, 22 SP remaining
      // Variance: 22 - 11.4 = 10.6 SP = 26.5%
      const result = calculateIterationHealth({
        ...baseParams,
        completed: 18,
        referenceDate: '2024-01-11',
      })

      expect(result.status).toBe(IterationHealthStatus.OffTrack)
      expect(result.variancePercent).toBeGreaterThan(25)
    })

    it('should return Off Track when nothing completed near end', () => {
      // Day 12: almost done, nothing completed
      const result = calculateIterationHealth({
        ...baseParams,
        completed: 0,
        referenceDate: '2024-01-13',
      })

      expect(result.status).toBe(IterationHealthStatus.OffTrack)
    })
  })

  describe('Edge cases', () => {
    it('should return Unknown when total is zero', () => {
      const result = calculateIterationHealth({
        ...baseParams,
        total: 0,
        completed: 0,
        referenceDate: '2024-01-08',
      })

      expect(result.status).toBe(IterationHealthStatus.Unknown)
      expect(result.variancePercent).toBe(0)
    })

    it('should return NotStarted when iteration has not started', () => {
      const result = calculateIterationHealth({
        ...baseParams,
        completed: 0,
        referenceDate: '2023-12-31', // Before start
      })

      expect(result.status).toBe(IterationHealthStatus.NotStarted)
    })

    it('should return Completed when iteration is past end date', () => {
      const result = calculateIterationHealth({
        ...baseParams,
        completed: 32,
        referenceDate: '2024-01-20', // After end
      })

      expect(result.status).toBe(IterationHealthStatus.Completed)
    })

    it('should return On Track when fully completed', () => {
      const result = calculateIterationHealth({
        ...baseParams,
        completed: 40,
        referenceDate: '2024-01-11',
      })

      expect(result.status).toBe(IterationHealthStatus.OnTrack)
      expect(result.variancePercent).toBeLessThan(0) // Ahead
    })

    it('should use current date when referenceDate not provided', () => {
      // This test just ensures no error is thrown
      const result = calculateIterationHealth({
        startDate: toCalendarDate(new Date(Date.now() - 7 * 24 * 3600 * 1000)),
        endDate: toCalendarDate(new Date(Date.now() + 7 * 24 * 3600 * 1000)),
        total: 40,
        completed: 20,
      })

      expect(result.status).toBeDefined()
    })
  })
})

describe('sprintActiveDays', () => {
  const planned = { start: '2026-09-28', end: '2026-10-09' }

  it('falls back to the planned days without an active period', () => {
    // Act
    const result = sprintActiveDays(planned)

    // Assert
    expect(result).toEqual({ start: '2026-09-28', end: '2026-10-09' })
  })

  it('runs from the day it became active to the day before an end at midnight', () => {
    // Arrange — started Tuesday 9am and runs to the end of Friday, in Chicago
    const sprint = {
      ...planned,
      activeFrom: new Date('2026-09-29T14:00:00Z'),
      activeUntil: new Date('2026-10-10T05:00:00Z'),
      timeZone: 'America/Chicago',
    }

    // Act
    const result = sprintActiveDays(sprint)

    // Assert
    expect(result).toEqual({ start: '2026-09-29', end: '2026-10-09' })
  })

  it('ends on the day it was completed', () => {
    // Arrange — completed Thursday afternoon in Chicago
    const sprint = {
      ...planned,
      activeFrom: new Date('2026-09-28T05:00:00Z'),
      activeUntil: new Date('2026-10-08T20:00:00Z'),
      timeZone: 'America/Chicago',
    }

    // Act
    const result = sprintActiveDays(sprint)

    // Assert
    expect(result).toEqual({ start: '2026-09-28', end: '2026-10-08' })
  })
})
