import {
  StoryMapDetailsDto,
  StoryMapGoalDto,
  StoryMapPersonaDto,
  StoryMapStepDto,
  StoryMapSwimLaneDto,
  StoryMapTaskDto,
} from '@/src/services/wayd-api'
import { generateCsv } from '@/src/utils/csv-utils'
import { buildExportRows, EXPORT_HEADERS } from './board-export'

/**
 * The export is one row per task with its ancestors repeated, in board order, with empty goals and
 * steps still represented.
 */

const task = (
  title: string,
  order: number,
  swimLaneId = 'lane-1',
  personaIds: string[] = [],
  description?: string,
): StoryMapTaskDto =>
  ({
    id: title,
    stepId: 's1',
    swimLaneId,
    title,
    description,
    order,
    personaIds,
    checklist: [],
    checklistCompletedCount: 0,
    checklistTotalCount: 0,
  }) as StoryMapTaskDto

const step = (
  name: string,
  order: number,
  tasks: StoryMapTaskDto[] = [],
  personaIds: string[] = [],
): StoryMapStepDto =>
  ({
    id: name,
    goalId: 'g1',
    name,
    order,
    personaIds,
    tasks,
  }) as StoryMapStepDto

const goal = (
  name: string,
  order: number,
  steps: StoryMapStepDto[] = [],
): StoryMapGoalDto =>
  ({ id: name, name, order, personaIds: [], steps }) as StoryMapGoalDto

const lane = (id: string, name: string, order: number): StoryMapSwimLaneDto =>
  ({ id, name, order, isDefault: order === 0 }) as StoryMapSwimLaneDto

const persona = (id: string, name: string, order: number): StoryMapPersonaDto =>
  ({ id, name, order, color: '#fff' }) as StoryMapPersonaDto

const map = (
  goals: StoryMapGoalDto[],
  swimLanes: StoryMapSwimLaneDto[] = [lane('lane-1', 'Tasks', 0)],
  personas: StoryMapPersonaDto[] = [],
): StoryMapDetailsDto =>
  ({
    id: 'map-1',
    key: 1,
    name: 'Map',
    status: 'Active',
    goals,
    swimLanes,
    personas,
  }) as StoryMapDetailsDto

describe('buildExportRows', () => {
  it('emits one row per task with its goal and step repeated', () => {
    // Arrange
    const details = map([
      goal('Goal A', 0, [
        step('Step 1', 0, [task('Task 1', 0), task('Task 2', 1)]),
      ]),
    ])

    // Act
    const rows = buildExportRows(details)

    // Assert
    expect(rows).toEqual([
      ['Goal A', 1, 'Step 1', 1, 'Task 1', 1, '', '', 'Tasks'],
      ['Goal A', 1, 'Step 1', 1, 'Task 2', 2, '', '', 'Tasks'],
    ])
  })

  it('produces a row for every header column', () => {
    // Arrange
    const details = map([
      goal('Goal A', 0, [step('Step 1', 0, [task('T', 0)])]),
    ])

    // Act
    const rows = buildExportRows(details)

    // Assert
    expect(rows[0]).toHaveLength(EXPORT_HEADERS.length)
  })

  describe('ordering', () => {
    it('walks goals, steps, and tasks in board order rather than array order', () => {
      // Arrange — every level is stored out of order.
      const details = map([
        goal('Second', 1, [step('Step B', 0, [task('Task B', 0)])]),
        goal('First', 0, [
          step('Step 2', 1, [task('Task 2', 0)]),
          step('Step 1', 0, [task('Later', 1), task('Earlier', 0)]),
        ]),
      ])

      // Act
      const rows = buildExportRows(details)

      // Assert
      expect(rows.map((r) => [r[0], r[2], r[4]])).toEqual([
        ['First', 'Step 1', 'Earlier'],
        ['First', 'Step 1', 'Later'],
        ['First', 'Step 2', 'Task 2'],
        ['Second', 'Step B', 'Task B'],
      ])
    })

    it('numbers goals and steps from one, by position rather than stored order', () => {
      // Arrange — orders are sparse, as they can be mid-reorder.
      const details = map([
        goal('Goal A', 5, [step('Step 1', 3, [task('T', 9)])]),
      ])

      // Act
      const rows = buildExportRows(details)

      // Assert — goal order, step order, task order.
      expect([rows[0][1], rows[0][3], rows[0][5]]).toEqual([1, 1, 1])
    })

    it('numbers tasks per swim lane, not across the whole step', () => {
      // Arrange
      const details = map(
        [
          goal('Goal A', 0, [
            step('Step 1', 0, [
              task('Lane 1 first', 0, 'lane-1'),
              task('Lane 2 first', 0, 'lane-2'),
              task('Lane 2 second', 1, 'lane-2'),
            ]),
          ]),
        ],
        [lane('lane-1', 'Tasks', 0), lane('lane-2', 'Release 2', 1)],
      )

      // Act
      const rows = buildExportRows(details)

      // Assert — each cell restarts at 1.
      expect(rows.map((r) => [r[4], r[5], r[8]])).toEqual([
        ['Lane 1 first', 1, 'Tasks'],
        ['Lane 2 first', 1, 'Release 2'],
        ['Lane 2 second', 2, 'Release 2'],
      ])
    })

    it("groups a step's tasks by swim lane in board order", () => {
      // Arrange
      const details = map(
        [
          goal('Goal A', 0, [
            step('Step 1', 0, [
              task('In last lane', 0, 'lane-3'),
              task('In first lane', 0, 'lane-1'),
            ]),
          ]),
        ],
        [lane('lane-1', 'First', 0), lane('lane-3', 'Third', 2)],
      )

      // Act
      const rows = buildExportRows(details)

      // Assert
      expect(rows.map((r) => r[4])).toEqual(['In first lane', 'In last lane'])
    })
  })

  describe('empty goals and steps', () => {
    it('emits a row for a goal with no steps', () => {
      // Arrange
      const details = map([goal('Empty goal', 0)])

      // Act
      const rows = buildExportRows(details)

      // Assert — everything below the goal is blank.
      expect(rows).toEqual([['Empty goal', 1, '', '', '', '', '', '', '']])
    })

    it('emits a row for a step with no tasks, keeping its personas', () => {
      // Arrange
      const details = map(
        [goal('Goal A', 0, [step('Empty step', 0, [], ['p1'])])],
        undefined,
        [persona('p1', 'Engineer', 0)],
      )

      // Act
      const rows = buildExportRows(details)

      // Assert
      expect(rows).toEqual([
        ['Goal A', 1, 'Empty step', 1, '', '', '', 'Engineer', ''],
      ])
    })

    it('keeps an empty step in sequence among its populated siblings', () => {
      // Arrange
      const details = map([
        goal('Goal A', 0, [
          step('Step 1', 0, [task('Task 1', 0)]),
          step('Step 2', 1),
          step('Step 3', 2, [task('Task 3', 0)]),
        ]),
      ])

      // Act
      const rows = buildExportRows(details)

      // Assert
      expect(rows.map((r) => [r[2], r[3], r[4]])).toEqual([
        ['Step 1', 1, 'Task 1'],
        ['Step 2', 2, ''],
        ['Step 3', 3, 'Task 3'],
      ])
    })

    it('returns no rows for a map with no goals', () => {
      // Arrange
      const details = map([])

      // Act
      const rows = buildExportRows(details)

      // Assert
      expect(rows).toEqual([])
    })
  })

  describe('personas', () => {
    it('joins linked persona names with a semicolon', () => {
      // Arrange
      const details = map(
        [
          goal('Goal A', 0, [
            step('Step 1', 0, [task('T', 0, 'lane-1', ['p1', 'p2'])]),
          ]),
        ],
        undefined,
        [persona('p1', 'Engineer', 0), persona('p2', 'Designer', 1)],
      )

      // Act
      const rows = buildExportRows(details)

      // Assert — comma is the CSV delimiter, so the list uses semicolons.
      expect(rows[0][7]).toBe('Engineer; Designer')
    })

    it("lists personas in the map's order, not the order they were linked", () => {
      // Arrange
      const details = map(
        [
          goal('Goal A', 0, [
            step('Step 1', 0, [task('T', 0, 'lane-1', ['p2', 'p1'])]),
          ]),
        ],
        undefined,
        [persona('p1', 'Engineer', 0), persona('p2', 'Designer', 1)],
      )

      // Act
      const rows = buildExportRows(details)

      // Assert
      expect(rows[0][7]).toBe('Engineer; Designer')
    })

    it('leaves the column blank when nothing is linked', () => {
      // Arrange
      const details = map(
        [goal('Goal A', 0, [step('Step 1', 0, [task('T', 0)])])],
        undefined,
        [persona('p1', 'Engineer', 0)],
      )

      // Act
      const rows = buildExportRows(details)

      // Assert
      expect(rows[0][7]).toBe('')
    })

    it('ignores a persona id the map no longer has', () => {
      // Arrange — a stale link left by a deleted persona.
      const details = map(
        [
          goal('Goal A', 0, [
            step('Step 1', 0, [task('T', 0, 'lane-1', ['gone', 'p1'])]),
          ]),
        ],
        undefined,
        [persona('p1', 'Engineer', 0)],
      )

      // Act
      const rows = buildExportRows(details)

      // Assert
      expect(rows[0][7]).toBe('Engineer')
    })
  })

  it('exports a description when the task has one', () => {
    // Arrange
    const details = map([
      goal('Goal A', 0, [
        step('Step 1', 0, [task('T', 0, 'lane-1', [], 'Some detail')]),
      ]),
    ])

    // Act
    const rows = buildExportRows(details)

    // Assert
    expect(rows[0][6]).toBe('Some detail')
  })

  it('ignores the persona filter — every task is exported', () => {
    // Arrange
    const details = map(
      [
        goal('Goal A', 0, [
          step('Step 1', 0, [
            task('Tagged', 0, 'lane-1', ['p1']),
            task('Untagged', 1),
          ]),
        ]),
      ],
      undefined,
      [persona('p1', 'Engineer', 0)],
    )

    // Act — buildExportRows takes no filter argument at all.
    const rows = buildExportRows(details)

    // Assert
    expect(rows.map((r) => r[4])).toEqual(['Tagged', 'Untagged'])
  })

  describe('as CSV', () => {
    it('quotes a name containing the delimiter, keeping the column count', () => {
      // Arrange
      const details = map([
        goal('Plan, build, ship', 0, [
          step('Step 1', 0, [task('Do "the" work', 0)]),
        ]),
      ])

      // Act
      const csv = generateCsv(EXPORT_HEADERS, buildExportRows(details))
      const [, dataRow] = csv.split('\n')

      // Assert — the comma is inside quotes and the embedded quotes are doubled.
      expect(dataRow).toBe(
        '"Plan, build, ship",1,Step 1,1,"Do ""the"" work",1,,,Tasks',
      )
    })

    it('quotes a description spanning several lines', () => {
      // Arrange
      const details = map([
        goal('Goal A', 0, [
          step('Step 1', 0, [task('T', 0, 'lane-1', [], 'Line one\nLine two')]),
        ]),
      ])

      // Act
      const csv = generateCsv(EXPORT_HEADERS, buildExportRows(details))

      // Assert — the newline stays inside a quoted field rather than splitting the row.
      expect(csv).toContain('"Line one\nLine two"')
    })

    it('starts with the header row', () => {
      // Arrange
      const details = map([goal('Goal A', 0)])

      // Act
      const csv = generateCsv(EXPORT_HEADERS, buildExportRows(details))

      // Assert
      expect(csv.split('\n')[0]).toBe(
        'Goal,Goal Order,Step,Step Order,Task,Task Order,Description,Personas,Swim Lane',
      )
    })
  })

  it('still exports a task whose swim lane is missing from the map', () => {
    // Arrange
    const details = map(
      [
        goal('Goal A', 0, [
          step('Step 1', 0, [task('Orphan', 0, 'lane-gone')]),
        ]),
      ],
      [lane('lane-1', 'Tasks', 0)],
    )

    // Act
    const rows = buildExportRows(details)

    // Assert — kept, with the lane name blank rather than dropped from the file.
    expect(rows).toEqual([['Goal A', 1, 'Step 1', 1, 'Orphan', 1, '', '', '']])
  })
})
