import dayjs from 'dayjs'
import {
  findParentPlanNodeRange,
  getParentExpansionHint,
  getMilestoneParentExpansionHint,
  findOwnChildrenSpan,
  getChildrenContainmentError,
  isShiftOnlyChange,
} from './project-parent-date-hint'

describe('project-parent-date-hint', () => {
  describe('isShiftOnlyChange', () => {
    it('returns true when start and end both shift by the same delta', () => {
      const origStart = dayjs('2026-06-01')
      const origEnd = dayjs('2026-06-10')
      const newStart = dayjs('2026-06-05')
      const newEnd = dayjs('2026-06-14')

      expect(isShiftOnlyChange(origStart, origEnd, newStart, newEnd)).toBe(true)
    })

    it('returns false when only start changes', () => {
      const origStart = dayjs('2026-06-01')
      const origEnd = dayjs('2026-06-10')
      const newStart = dayjs('2026-06-05')
      const newEnd = dayjs('2026-06-10')

      expect(isShiftOnlyChange(origStart, origEnd, newStart, newEnd)).toBe(false)
    })

    it('returns false when dates shift by different amounts', () => {
      const origStart = dayjs('2026-06-01')
      const origEnd = dayjs('2026-06-10')
      const newStart = dayjs('2026-06-05')
      const newEnd = dayjs('2026-06-12')

      expect(isShiftOnlyChange(origStart, origEnd, newStart, newEnd)).toBe(false)
    })

    it('returns false when dates do not change', () => {
      const origStart = dayjs('2026-06-01')
      const origEnd = dayjs('2026-06-10')

      expect(isShiftOnlyChange(origStart, origEnd, origStart, origEnd)).toBe(false)
    })
  })

  describe('findParentPlanNodeRange case-insensitivity', () => {
    const nodes = [
      {
        id: 'A1B2C3D4-E5F6-7A8B-9C0D-E1F2A3B4C5D6',
        name: 'Phase 1',
        start: '2026-06-01',
        end: '2026-06-30',
        children: []
      }
    ]

    it('finds node with lowercase parentId', () => {
      const result = findParentPlanNodeRange(nodes, 'a1b2c3d4-e5f6-7a8b-9c0d-e1f2a3b4c5d6')
      expect(result).not.toBeNull()
      expect(result?.name).toBe('Phase 1')
      expect(result?.start.format('YYYY-MM-DD')).toBe('2026-06-01')
    })

    it('finds node with uppercase parentId', () => {
      const result = findParentPlanNodeRange(nodes, 'A1B2C3D4-E5F6-7A8B-9C0D-E1F2A3B4C5D6')
      expect(result).not.toBeNull()
      expect(result?.name).toBe('Phase 1')
    })
  })

  describe('getChildrenContainmentError', () => {
    const span = {
      start: dayjs('2026-06-05'),
      end: dayjs('2026-06-15'),
      earliest: 'Task 1',
      latest: 'Task 2'
    }

    it('returns error when start is after children span', () => {
      const start = dayjs('2026-06-06')
      const end = dayjs('2026-06-20')
      const err = getChildrenContainmentError(span, start, end)
      expect(err).toContain('Start date must be on or before child item')
    })

    it('returns error when end is before children span', () => {
      const start = dayjs('2026-06-01')
      const end = dayjs('2026-06-14')
      const err = getChildrenContainmentError(span, start, end)
      expect(err).toContain('End date must be on or after child item')
    })

    it('returns null when range contains all children', () => {
      const start = dayjs('2026-06-01')
      const end = dayjs('2026-06-20')
      const err = getChildrenContainmentError(span, start, end)
      expect(err).toBeNull()
    })
  })
})
