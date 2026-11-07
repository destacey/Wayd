import {
  findOwnChildrenSpan,
  findParentPlanNodeRange,
} from './project-parent-date-hint'

describe('project-parent-date-hint', () => {
  describe('findParentPlanNodeRange case-insensitivity', () => {
    const nodes = [
      {
        id: 'A1B2C3D4-E5F6-7A8B-9C0D-E1F2A3B4C5D6',
        name: 'Stage 1',
        start: '2026-06-01',
        end: '2026-06-30',
        children: []
      }
    ]

    it('finds node with lowercase parentId', () => {
      const result = findParentPlanNodeRange(nodes, 'a1b2c3d4-e5f6-7a8b-9c0d-e1f2a3b4c5d6')
      expect(result).not.toBeNull()
      expect(result?.name).toBe('Stage 1')
      expect(result?.start.format('YYYY-MM-DD')).toBe('2026-06-01')
    })

    it('finds node with uppercase parentId', () => {
      const result = findParentPlanNodeRange(nodes, 'A1B2C3D4-E5F6-7A8B-9C0D-E1F2A3B4C5D6')
      expect(result).not.toBeNull()
      expect(result?.name).toBe('Stage 1')
    })
  })

  describe('findOwnChildrenSpan', () => {
    it('spans the direct children, a milestone by its planned date', () => {
      // Arrange
      const nodes = [
        {
          id: 'STAGE-1',
          name: 'Stage 1',
          children: [
            { id: 't1', name: 'Task 1', start: '2026-06-05', end: '2026-06-10' },
            { id: 'm1', name: 'Launch', plannedDate: '2026-06-20' },
          ]
        }
      ]

      // Act
      const span = findOwnChildrenSpan(nodes, 'stage-1')

      // Assert
      expect(span?.start.format('YYYY-MM-DD')).toBe('2026-06-05')
      expect(span?.end.format('YYYY-MM-DD')).toBe('2026-06-20')
      expect(span?.earliest).toBe('Task 1')
      expect(span?.latest).toBe('Launch')
    })
  })
})
