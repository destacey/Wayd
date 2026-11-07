jest.mock('dayjs', () => jest.requireActual('dayjs'))

import dayjs, { Dayjs } from 'dayjs'
import {
  childrenSpan,
  getChildrenContainmentError,
  isShiftOnlyChange,
  rangeChangeContainmentError,
} from './date-containment'

const d = (value: string) => dayjs(value)

const SPAN = {
  start: d('2026-06-05'),
  end: d('2026-06-15'),
  earliest: 'Task 1',
  latest: 'Task 2',
}

describe('date-containment', () => {
  describe('isShiftOnlyChange', () => {
    const original = [d('2026-06-01'), d('2026-06-10')] as const

    it('is true when start and end both move by the same days', () => {
      // Arrange
      const next = [d('2026-06-05'), d('2026-06-14')] as const

      // Act
      const result = isShiftOnlyChange(...original, ...next)

      // Assert
      expect(result).toBe(true)
    })

    it('is false when only one end moves', () => {
      // Arrange
      const next = [d('2026-06-05'), d('2026-06-10')] as const

      // Act
      const result = isShiftOnlyChange(...original, ...next)

      // Assert
      expect(result).toBe(false)
    })

    it('is false when the ends move by different days', () => {
      // Arrange
      const next = [d('2026-06-05'), d('2026-06-12')] as const

      // Act
      const result = isShiftOnlyChange(...original, ...next)

      // Assert
      expect(result).toBe(false)
    })

    it('is false when nothing moves', () => {
      // Arrange
      const next = original

      // Act
      const result = isShiftOnlyChange(...original, ...next)

      // Assert
      expect(result).toBe(false)
    })

    it('is false without an original range', () => {
      // Arrange
      const next = [d('2026-06-05'), d('2026-06-14')] as const

      // Act
      const result = isShiftOnlyChange(null, null, ...next)

      // Assert
      expect(result).toBe(false)
    })
  })

  describe('getChildrenContainmentError', () => {
    it('names the earliest child when the start is after it', () => {
      // Arrange
      const [start, end] = [d('2026-06-06'), d('2026-06-20')]

      // Act
      const error = getChildrenContainmentError(SPAN, start, end)

      // Assert
      expect(error).toBe(
        'Start date must be on or before child item “Task 1” (Jun 5, 2026).',
      )
    })

    it('names the latest child when the end is before it', () => {
      // Arrange
      const [start, end] = [d('2026-06-01'), d('2026-06-14')]

      // Act
      const error = getChildrenContainmentError(SPAN, start, end)

      // Assert
      expect(error).toBe(
        'End date must be on or after child item “Task 2” (Jun 15, 2026).',
      )
    })

    it('allows a range that meets the children exactly', () => {
      // Arrange
      const [start, end] = [d('2026-06-05'), d('2026-06-15')]

      // Act
      const error = getChildrenContainmentError(SPAN, start, end)

      // Assert
      expect(error).toBeNull()
    })
  })

  describe('rangeChangeContainmentError', () => {
    const original: [Dayjs, Dayjs] = [d('2026-06-01'), d('2026-06-20')]

    it('allows a shift that leaves a child outside the new range', () => {
      // Arrange — moved 10 days later, past the child's start
      const next: [Dayjs, Dayjs] = [d('2026-06-11'), d('2026-06-30')]

      // Act
      const error = rangeChangeContainmentError({ original, next, span: SPAN })

      // Assert
      expect(error).toBeNull()
    })

    it('blocks a resize whose start passes a child, on the start', () => {
      // Arrange
      const next: [Dayjs, Dayjs] = [d('2026-06-06'), d('2026-06-20')]

      // Act
      const error = rangeChangeContainmentError({ original, next, span: SPAN })

      // Assert
      expect(error).toEqual({
        role: 'start',
        message:
          'Start date must be on or before child item “Task 1” (Jun 5, 2026).',
      })
    })

    it('blocks a resize whose end passes a child, on the end', () => {
      // Arrange
      const next: [Dayjs, Dayjs] = [d('2026-06-01'), d('2026-06-14')]

      // Act
      const error = rangeChangeContainmentError({ original, next, span: SPAN })

      // Assert
      expect(error).toEqual({
        role: 'end',
        message:
          'End date must be on or after child item “Task 2” (Jun 15, 2026).',
      })
    })

    it('allows a resize that still contains the children', () => {
      // Arrange
      const next: [Dayjs, Dayjs] = [d('2026-06-03'), d('2026-06-18')]

      // Act
      const error = rangeChangeContainmentError({ original, next, span: SPAN })

      // Assert
      expect(error).toBeNull()
    })

    it('allows any change when there are no dated children', () => {
      // Arrange
      const next: [Dayjs, Dayjs] = [d('2026-06-10'), d('2026-06-11')]

      // Act
      const error = rangeChangeContainmentError({ original, next, span: null })

      // Assert
      expect(error).toBeNull()
    })
  })

  describe('childrenSpan', () => {
    it('spans the children and names the first and the last', () => {
      // Arrange
      const children = [
        { name: 'B', range: [d('2026-06-05'), d('2026-06-10')] },
        { name: 'A', range: [d('2026-06-01'), d('2026-06-08')] },
        { name: 'Undated', range: null },
        { name: 'C', range: [d('2026-06-09'), d('2026-06-30')] },
      ] as const

      // Act
      const span = childrenSpan(
        children,
        (child) => child.range as [Dayjs, Dayjs] | null,
        (child) => child.name,
      )

      // Assert
      expect(span?.start.format('YYYY-MM-DD')).toBe('2026-06-01')
      expect(span?.end.format('YYYY-MM-DD')).toBe('2026-06-30')
      expect(span?.earliest).toBe('A')
      expect(span?.latest).toBe('C')
    })

    it('is null when no child has dates', () => {
      // Arrange
      const children = [{ name: 'Undated' }]

      // Act
      const span = childrenSpan(
        children,
        () => null,
        (child) => child.name,
      )

      // Assert
      expect(span).toBeNull()
    })
  })
})
