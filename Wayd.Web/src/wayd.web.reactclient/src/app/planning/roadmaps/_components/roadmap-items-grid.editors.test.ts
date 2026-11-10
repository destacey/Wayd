import type { RoadmapItemTreeNode } from './roadmap-items-grid'
import {
  roadmapColorEditor,
  roadmapEndEditor,
  roadmapNameEditor,
  roadmapStartEditor,
  roadmapTypeEditor,
  toCreateRoadmapItemRequest,
  toRoadmapItemPatch,
} from './roadmap-items-grid.editors'

const day = (value: string) => {
  const [y, m, d] = value.split('-').map(Number)
  return new Date(y, m - 1, d)
}

const node = (
  type: 'Activity' | 'Timebox' | 'Milestone',
  extra: Partial<RoadmapItemTreeNode> = {},
): RoadmapItemTreeNode => ({
  id: `${type}-1`,
  name: `${type} 1`,
  type,
  $type: type.toLowerCase(),
  start: type === 'Milestone' ? null : day('2026-03-01'),
  end: type === 'Milestone' ? null : day('2026-03-31'),
  date: type === 'Milestone' ? day('2026-04-15') : null,
  color: null,
  children: [],
  ...extra,
})

const fieldOf = (
  editor: { field: string | ((row: RoadmapItemTreeNode) => string) },
  row: RoadmapItemTreeNode,
) => (typeof editor.field === 'function' ? editor.field(row) : editor.field)

const pairCheck = (
  row: RoadmapItemTreeNode,
  start: string | null,
  end: string | null,
) => roadmapStartEditor.group!.validate!({ start, end }, row)

describe('roadmap items grid editors', () => {
  describe('name', () => {
    it('requires a name', () => {
      // Arrange
      const row = node('Activity')

      // Act
      const blank = roadmapNameEditor.validate!('  ', row)
      const named = roadmapNameEditor.validate!('Plan', row)

      // Assert
      expect(blank).toBe('Name is required.')
      expect(named).toBeUndefined()
    })
  })

  describe('type', () => {
    it('edits only a new row', () => {
      // Arrange
      const row = node('Activity')

      // Act
      const onDraft = roadmapTypeEditor.editable!(row, { isDraft: true })
      const onItem = roadmapTypeEditor.editable!(row, { isDraft: false })

      // Assert
      expect(onDraft).toBe(true)
      expect(onItem).toBe(false)
    })

    it('shows the chosen type on the row', () => {
      // Arrange
      const row = node('Activity')

      // Act
      const changed = roadmapTypeEditor.apply(row, 'timebox')

      // Assert
      expect(changed.type).toBe('Timebox')
      expect(roadmapTypeEditor.get(changed)).toBe('timebox')
    })
  })

  describe('start and end', () => {
    it('reads and writes calendar dates', () => {
      // Arrange
      const row = node('Activity')

      // Act
      const start = roadmapStartEditor.get(row)
      const moved = roadmapStartEditor.apply(row, '2026-03-05')

      // Assert
      expect(start).toBe('2026-03-01')
      expect(roadmapStartEditor.get(moved)).toBe('2026-03-05')
      expect(moved.start).toEqual(day('2026-03-05'))
    })

    it("edits a milestone's date in the Start column", () => {
      // Arrange
      const row = node('Milestone')

      // Act
      const moved = roadmapStartEditor.apply(row, '2026-04-20')

      // Assert
      expect(fieldOf(roadmapStartEditor, row)).toBe('date')
      expect(fieldOf(roadmapStartEditor, node('Activity'))).toBe('start')
      expect(roadmapStartEditor.get(row)).toBe('2026-04-15')
      expect(moved.date).toEqual(day('2026-04-20'))
      expect(moved.start).toBeNull()
    })

    it('has no End on a milestone', () => {
      // Arrange
      const context = { isDraft: false }

      // Act
      const milestone = roadmapEndEditor.editable!(node('Milestone'), context)
      const timebox = roadmapEndEditor.editable!(node('Timebox'), context)

      // Assert
      expect(milestone).toBe(false)
      expect(timebox).toBe(true)
    })

    it('requires both dates, naming a milestone’s as its Date', () => {
      // Arrange
      const activity = node('Activity')
      const milestone = node('Milestone')

      // Act
      const start = roadmapStartEditor.validate!(null, activity)
      const date = roadmapStartEditor.validate!(null, milestone)
      const end = roadmapEndEditor.validate!(null, activity)

      // Assert
      expect(start).toBe('Start is required.')
      expect(date).toBe('Date is required.')
      expect(end).toBe('End is required.')
    })

    it('rejects an End before the Start', () => {
      // Arrange
      const row = node('Activity')

      // Act
      const errors = pairCheck(row, '2026-03-10', '2026-03-09')

      // Assert
      expect(errors).toEqual({ end: 'End must be on or after Start.' })
    })

    it('allows an End on the Start', () => {
      // Arrange
      const row = node('Activity')

      // Act
      const errors = pairCheck(row, '2026-03-10', '2026-03-10')

      // Assert
      expect(errors).toBeUndefined()
    })

    it('leaves a milestone’s single date unchecked against an End', () => {
      // Arrange
      const row = node('Milestone')

      // Act
      const errors = pairCheck(row, '2026-05-01', null)

      // Assert
      expect(errors).toBeUndefined()
    })

    describe('a parent with children', () => {
      const parent = () =>
        node('Activity', {
          children: [
            node('Activity', {
              id: 'child',
              name: 'Build',
              start: day('2026-03-10'),
              end: day('2026-03-20'),
            }),
            node('Milestone', {
              id: 'launch',
              name: 'Launch',
              date: day('2026-03-25'),
            }),
          ],
        })

      it('allows a shift that carries the children past the new start', () => {
        // Arrange — both ends 15 days later
        const row = parent()

        // Act
        const errors = pairCheck(row, '2026-03-16', '2026-04-15')

        // Assert
        expect(errors).toBeUndefined()
      })

      it('blocks a resize that starts after a child, on Start', () => {
        // Arrange
        const row = parent()

        // Act
        const errors = pairCheck(row, '2026-03-11', '2026-03-31')

        // Assert
        expect(errors).toEqual({
          start:
            'Start date must be on or before child item “Build” (Mar 10, 2026).',
        })
      })

      it('blocks a resize that ends before a milestone child, on End', () => {
        // Arrange
        const row = parent()

        // Act
        const errors = pairCheck(row, '2026-03-01', '2026-03-24')

        // Assert
        expect(errors).toEqual({
          end: 'End date must be on or after child item “Launch” (Mar 25, 2026).',
        })
      })

      it('allows a resize that still contains the children', () => {
        // Arrange
        const row = parent()

        // Act
        const errors = pairCheck(row, '2026-03-05', '2026-03-28')

        // Assert
        expect(errors).toBeUndefined()
      })
    })
  })

  describe('color', () => {
    it('edits an activity but not a timebox', () => {
      // Arrange
      const editor = roadmapColorEditor([])
      const context = { isDraft: false }

      // Act
      const activity = editor.editable!(node('Activity'), context)
      const timebox = editor.editable!(node('Timebox'), context)

      // Assert
      expect(activity).toBe(true)
      expect(timebox).toBe(false)
    })

    it('reads no color as null and writes the chosen one', () => {
      // Arrange
      const editor = roadmapColorEditor([])

      // Act
      const none = editor.get(node('Activity'))
      const colored = editor.apply(node('Activity'), '#FF0000')

      // Assert
      expect(none).toBeNull()
      expect(editor.get(colored)).toBe('#FF0000')
    })

    it('accepts a hex color or none, and nothing else', () => {
      // Arrange
      const editor = roadmapColorEditor([])
      const row = node('Activity')

      // Act
      const results = ['#1677FF', '#abc', null, 'blue', '#12345'].map((color) =>
        editor.validate!(color, row),
      )

      // Assert
      expect(results).toStrictEqual([
        undefined,
        undefined,
        undefined,
        'Use a hex color, such as #1677FF.',
        'Use a hex color, such as #1677FF.',
      ])
    })
  })

  describe('toRoadmapItemPatch', () => {
    it('replaces each field the grid sends, and only those', () => {
      // Arrange
      const patch = { start: '2026-03-05', end: '2026-04-05' }

      // Act
      const operations = toRoadmapItemPatch(patch)

      // Assert
      expect(operations).toStrictEqual([
        { op: 'replace', path: '/start', value: '2026-03-05' },
        { op: 'replace', path: '/end', value: '2026-04-05' },
      ])
    })

    it('sends a cleared color as null', () => {
      // Arrange
      const patch = { color: null }

      // Act
      const operations = toRoadmapItemPatch(patch)

      // Assert
      expect(operations).toStrictEqual([
        { op: 'replace', path: '/color', value: null },
      ])
    })
  })

  describe('toCreateRoadmapItemRequest', () => {
    it('creates an activity by default, under its parent, with its color', () => {
      // Arrange
      const values = {
        name: 'Build',
        start: '2026-03-01',
        end: '2026-03-31',
        color: '#FF0000',
      }

      // Act
      const request = toCreateRoadmapItemRequest('r1', 'p1', values)

      // Assert
      expect(request).toStrictEqual({
        $type: 'activity',
        roadmapId: 'r1',
        parentId: 'p1',
        name: 'Build',
        start: '2026-03-01',
        end: '2026-03-31',
        color: '#FF0000',
      })
    })

    it('drops a color picked before the type became Timebox', () => {
      // Arrange
      const values = {
        type: 'timebox',
        name: 'Sprint',
        start: '2026-03-01',
        end: '2026-03-14',
        color: '#FF0000',
      }

      // Act
      const request = toCreateRoadmapItemRequest('r1', null, values)

      // Assert
      expect(request).toStrictEqual({
        $type: 'timebox',
        roadmapId: 'r1',
        parentId: undefined,
        name: 'Sprint',
        start: '2026-03-01',
        end: '2026-03-14',
      })
    })
  })
})
