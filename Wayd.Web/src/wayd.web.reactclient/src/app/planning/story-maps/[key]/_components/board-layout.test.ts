import {
  StoryMapDetailsDto,
  StoryMapGoalDto,
  StoryMapStepDto,
  StoryMapSwimLaneDto,
  StoryMapTaskDto,
} from '@/src/services/wayd-api'
import { buildBoardLayout, cellKey } from './board-layout'

/**
 * Two invariants are asserted throughout: a goal header spans exactly its own steps' tracks, and
 * `stepColumnTracks` declares one track per step plus a placeholder for every step-less goal and a
 * narrow spine for every collapsed one.
 */

/** The flexible track every non-collapsed step column gets. */
const FLEX_TRACK = 'minmax(var(--sm-col-min), 1fr)'

/** The fixed narrow track a collapsed goal's spine gets. */
const SPINE_TRACK = 'var(--sm-collapsed-col-width)'

const task = (
  id: string,
  stepId: string,
  swimLaneId: string,
  order: number,
): StoryMapTaskDto => ({
  id,
  stepId,
  swimLaneId,
  title: id,
  order,
  personaIds: [],
  checklist: [],
  checklistCompletedCount: 0,
  checklistTotalCount: 0,
})

const step = (
  id: string,
  goalId: string,
  order: number,
  tasks: StoryMapTaskDto[] = [],
): StoryMapStepDto => ({
  id,
  goalId,
  name: id,
  order,
  personaIds: [],
  tasks,
})

const goal = (
  id: string,
  order: number,
  steps: StoryMapStepDto[] = [],
): StoryMapGoalDto => ({
  id,
  name: id,
  order,
  personaIds: [],
  steps,
})

const swimLane = (
  id: string,
  order: number,
  isDefault = false,
): StoryMapSwimLaneDto => ({ id, name: id, order, isDefault })

const map = (
  goals: StoryMapGoalDto[],
  swimLanes: StoryMapSwimLaneDto[] = [swimLane('lane-default', 0, true)],
): StoryMapDetailsDto =>
  ({
    id: 'map-1',
    key: 1,
    name: 'Map',
    status: 'Active',
    goals,
    swimLanes,
    personas: [],
  }) as StoryMapDetailsDto

describe('buildBoardLayout', () => {
  describe('column placement', () => {
    it('gives each step its own column, left to right across goals', () => {
      // Arrange
      const details = map([
        goal('g1', 0, [step('s1', 'g1', 0), step('s2', 'g1', 1)]),
        goal('g2', 1, [step('s3', 'g2', 0)]),
      ])

      // Act
      const { steps, lastColumn, stepColumnTracks } = buildBoardLayout(details)

      // Assert — column 1 is the label column, so steps start at 2.
      expect(steps.map((s) => [s.step.id, s.column])).toEqual([
        ['s1', 2],
        ['s2', 3],
        ['s3', 4],
      ])
      expect(lastColumn).toBe(4)
      expect(stepColumnTracks).toEqual([FLEX_TRACK, FLEX_TRACK, FLEX_TRACK])
    })

    it('spans a goal header across exactly its own steps', () => {
      // Arrange
      const details = map([
        goal('g1', 0, [
          step('s1', 'g1', 0),
          step('s2', 'g1', 1),
          step('s3', 'g1', 2),
        ]),
        goal('g2', 1, [step('s4', 'g2', 0)]),
      ])

      // Act
      const { goals } = buildBoardLayout(details)

      // Assert
      expect(
        goals.map((g) => [g.goal.id, g.columnStart, g.columnSpan]),
      ).toEqual([
        ['g1', 2, 3],
        ['g2', 5, 1],
      ])
    })

    it('orders goals and steps by their order field, not array position', () => {
      // Arrange — deliberately supplied out of order.
      const details = map([
        goal('g2', 1, [step('s2', 'g2', 1), step('s1', 'g2', 0)]),
        goal('g1', 0, [step('s0', 'g1', 0)]),
      ])

      // Act
      const { goals, steps } = buildBoardLayout(details)

      // Assert
      expect(goals.map((g) => g.goal.id)).toEqual(['g1', 'g2'])
      expect(steps.map((s) => s.step.id)).toEqual(['s0', 's1', 's2'])
    })

    it('records each step’s index within its own goal', () => {
      // Arrange
      const details = map([
        goal('g1', 0, [step('s1', 'g1', 0), step('s2', 'g1', 1)]),
        goal('g2', 1, [step('s3', 'g2', 0)]),
      ])

      // Act
      const { steps } = buildBoardLayout(details)

      // Assert — s3 is first in g2, even though it is third on the board.
      expect(steps.map((s) => [s.step.id, s.indexInGoal])).toEqual([
        ['s1', 0],
        ['s2', 1],
        ['s3', 0],
      ])
    })
  })

  describe('goals with no steps', () => {
    it('claims a placeholder column so the goal keeps its share of the width', () => {
      // Arrange
      const details = map([goal('g1', 0), goal('g2', 1), goal('g3', 2)])

      // Act
      const { goals, steps, stepColumnTracks } = buildBoardLayout(details)

      // Assert — three tracks for three goals, despite there being no steps at all.
      expect(steps).toHaveLength(0)
      expect(stepColumnTracks).toEqual([FLEX_TRACK, FLEX_TRACK, FLEX_TRACK])
      expect(
        goals.map((g) => [g.goal.id, g.columnStart, g.columnSpan]),
      ).toEqual([
        ['g1', 2, 1],
        ['g2', 3, 1],
        ['g3', 4, 1],
      ])
    })

    it('flags the placeholder so blank filler cells can be rendered', () => {
      // Arrange
      const details = map([goal('g1', 0, [step('s1', 'g1', 0)]), goal('g2', 1)])

      // Act
      const { goals } = buildBoardLayout(details)

      // Assert
      expect(goals.map((g) => [g.goal.id, g.isPlaceholderColumn])).toEqual([
        ['g1', false],
        ['g2', true],
      ])
    })

    it('interleaves placeholder and real columns without overlap', () => {
      // Arrange — an empty goal between two populated ones.
      const details = map([
        goal('g1', 0, [step('s1', 'g1', 0), step('s2', 'g1', 1)]),
        goal('g2', 1),
        goal('g3', 2, [step('s3', 'g3', 0)]),
      ])

      // Act
      const { goals, steps, stepColumnTracks } = buildBoardLayout(details)

      // Assert — g2 occupies column 4, so g3's step lands at 5.
      expect(
        goals.map((g) => [g.goal.id, g.columnStart, g.columnSpan]),
      ).toEqual([
        ['g1', 2, 2],
        ['g2', 4, 1],
        ['g3', 5, 1],
      ])
      expect(steps.map((s) => s.column)).toEqual([2, 3, 5])
      expect(stepColumnTracks).toHaveLength(4)
    })

    it('declares one track for a map with no goals at all', () => {
      // Arrange / Act
      const { stepColumnTracks } = buildBoardLayout(map([]))

      // Assert — the grid template still needs a track to name.
      expect(stepColumnTracks).toEqual([FLEX_TRACK])
    })
  })

  describe('collapsed goals', () => {
    it('folds a collapsed goal to one narrow track and shuffles the rest left', () => {
      // Arrange
      const details = map([
        goal('g1', 0, [step('s1', 'g1', 0), step('s2', 'g1', 1)]),
        goal('g2', 1, [step('s3', 'g2', 0), step('s4', 'g2', 1)]),
        goal('g3', 2, [step('s5', 'g3', 0)]),
      ])

      // Act
      const { goals, stepColumnTracks, lastColumn } = buildBoardLayout(
        details,
        {
          goalIds: new Set(['g2']),
        },
      )

      // Assert — g2 spans one track instead of two, so g3 starts at 5 rather than 6.
      expect(
        goals.map((g) => [
          g.goal.id,
          g.columnStart,
          g.columnSpan,
          g.isCollapsed,
        ]),
      ).toEqual([
        ['g1', 2, 2, false],
        ['g2', 4, 1, true],
        ['g3', 5, 1, false],
      ])
      expect(lastColumn).toBe(5)
      expect(stepColumnTracks).toEqual([
        FLEX_TRACK,
        FLEX_TRACK,
        SPINE_TRACK,
        FLEX_TRACK,
      ])
    })

    it('contributes no step placements for a collapsed goal', () => {
      // Arrange
      const details = map([
        goal('g1', 0, [step('s1', 'g1', 0)]),
        goal('g2', 1, [step('s2', 'g2', 0), step('s3', 'g2', 1)]),
      ])

      // Act
      const { steps } = buildBoardLayout(details, { goalIds: new Set(['g2']) })

      // Assert — a collapsed goal's steps render no cells, so they are not placed at all.
      expect(steps.map((s) => s.step.id)).toEqual(['s1'])
    })

    it('leaves a collapsed goal’s tasks out of the task buckets', () => {
      // Arrange
      const details = map([
        goal('g1', 0, [
          step('s1', 'g1', 0, [task('t1', 's1', 'lane-default', 0)]),
        ]),
        goal('g2', 1, [
          step('s2', 'g2', 0, [task('t2', 's2', 'lane-default', 0)]),
        ]),
      ])

      // Act
      const { tasksByCell } = buildBoardLayout(details, {
        goalIds: new Set(['g2']),
      })

      // Assert — hidden cells must not be drop targets.
      expect(tasksByCell.has(cellKey('s1', 'lane-default'))).toBe(true)
      expect(tasksByCell.has(cellKey('s2', 'lane-default'))).toBe(false)
    })

    it('does not flag a collapsed goal as a placeholder column', () => {
      // Arrange — both fold to one track, but only the placeholder accepts a dropped step.
      const details = map([goal('g1', 0), goal('g2', 1)])

      // Act
      const { goals } = buildBoardLayout(details, { goalIds: new Set(['g2']) })

      // Assert
      expect(
        goals.map((g) => [g.goal.id, g.isPlaceholderColumn, g.isCollapsed]),
      ).toEqual([
        ['g1', true, false],
        ['g2', false, true],
      ])
    })

    it('declares a spine for every collapsed goal when all are collapsed', () => {
      // Arrange
      const details = map([
        goal('g1', 0, [step('s1', 'g1', 0)]),
        goal('g2', 1, [step('s2', 'g2', 0)]),
      ])

      // Act
      const {
        steps,
        stepColumnTracks,
        collapsedColumnCount,
        flexibleColumnCount,
      } = buildBoardLayout(details, { goalIds: new Set(['g1', 'g2']) })

      // Assert
      expect(steps).toHaveLength(0)
      expect(stepColumnTracks).toEqual([SPINE_TRACK, SPINE_TRACK])
      expect(collapsedColumnCount).toBe(2)
      expect(flexibleColumnCount).toBe(0)
    })

    it('collapses goals and swim lanes independently', () => {
      // Arrange
      const details = map(
        [
          goal('g1', 0, [step('s1', 'g1', 0, [task('t1', 's1', 'l0', 0)])]),
          goal('g2', 1, [step('s2', 'g2', 0)]),
        ],
        [swimLane('l0', 0, true), swimLane('l1', 1)],
      )

      // Act
      const { goals, swimLanes, steps } = buildBoardLayout(details, {
        goalIds: new Set(['g2']),
        swimLaneIds: new Set(['l1']),
      })

      // Assert — the two axes fold independently.
      expect(goals.map((g) => g.isCollapsed)).toEqual([false, true])
      expect(swimLanes.map((l) => [l.swimLane.id, l.row])).toEqual([
        ['l0', 4],
        ['l1', null],
      ])
      expect(steps.map((s) => s.step.id)).toEqual(['s1'])
    })
  })

  describe('swim lane rows', () => {
    it('gives each lane a header row and a task row, in order', () => {
      // Arrange
      const details = map(
        [goal('g1', 0)],
        [swimLane('l0', 0, true), swimLane('l1', 1)],
      )

      // Act
      const { swimLanes } = buildBoardLayout(details)

      // Assert — rows 1 and 2 are goals and steps, so lanes start at 3.
      expect(swimLanes.map((l) => [l.swimLane.id, l.headerRow, l.row])).toEqual(
        [
          ['l0', 3, 4],
          ['l1', 5, 6],
        ],
      )
    })

    it('orders lanes by their order field', () => {
      // Arrange
      const details = map(
        [goal('g1', 0)],
        [swimLane('l1', 1), swimLane('l0', 0, true)],
      )

      // Act
      const { swimLanes } = buildBoardLayout(details)

      // Assert
      expect(swimLanes.map((l) => l.swimLane.id)).toEqual(['l0', 'l1'])
    })
  })

  describe('collapsed swim lanes', () => {
    it('gives a collapsed lane no task row and pulls the lanes below it up', () => {
      // Arrange
      const details = map(
        [goal('g1', 0, [step('s1', 'g1', 0)])],
        [swimLane('l0', 0, true), swimLane('l1', 1), swimLane('l2', 2)],
      )

      // Act
      const { swimLanes } = buildBoardLayout(details, {
        swimLaneIds: new Set(['l1']),
      })

      // Assert — l1 keeps only its banner (row 5), so l2 starts at 6 rather than 7.
      expect(
        swimLanes.map((l) => [
          l.swimLane.id,
          l.headerRow,
          l.row,
          l.isCollapsed,
        ]),
      ).toEqual([
        ['l0', 3, 4, false],
        ['l1', 5, null, true],
        ['l2', 6, 7, false],
      ])
    })

    it('leaves a collapsed lane out of the task buckets', () => {
      // Arrange — one task in each lane, on the same step.
      const details = map(
        [
          goal('g1', 0, [
            step('s1', 'g1', 0, [
              task('t-open', 's1', 'l0', 0),
              task('t-folded', 's1', 'l1', 0),
            ]),
          ]),
        ],
        [swimLane('l0', 0, true), swimLane('l1', 1)],
      )

      // Act
      const { tasksByCell } = buildBoardLayout(details, {
        swimLaneIds: new Set(['l1']),
      })

      // Assert — a collapsed lane renders no cells, so its tasks are not drop siblings.
      expect(tasksByCell.get(cellKey('s1', 'l0'))?.map((t) => t.id)).toEqual([
        't-open',
      ])
      expect(tasksByCell.has(cellKey('s1', 'l1'))).toBe(false)
    })

    it('collapses every lane when all are collapsed', () => {
      // Arrange
      const details = map(
        [goal('g1', 0)],
        [swimLane('l0', 0, true), swimLane('l1', 1)],
      )

      // Act
      const { swimLanes } = buildBoardLayout(details, {
        swimLaneIds: new Set(['l0', 'l1']),
      })

      // Assert — consecutive banner rows, no task rows at all.
      expect(swimLanes.map((l) => [l.swimLane.id, l.headerRow, l.row])).toEqual(
        [
          ['l0', 3, null],
          ['l1', 4, null],
        ],
      )
    })

    it('treats an unknown collapsed id as collapsing nothing', () => {
      // Arrange — a lane deleted by someone else can leave a stale id in the set.
      const details = map([goal('g1', 0)], [swimLane('l0', 0, true)])

      // Act
      const { swimLanes } = buildBoardLayout(details, {
        swimLaneIds: new Set(['gone']),
      })

      // Assert
      expect(swimLanes.map((l) => [l.swimLane.id, l.row])).toEqual([['l0', 4]])
    })
  })

  describe('task bucketing', () => {
    it('groups tasks by step and swim lane, sorted by order', () => {
      // Arrange — supplied out of order to prove they are sorted.
      const details = map(
        [
          goal('g1', 0, [
            step('s1', 'g1', 0, [
              task('t2', 's1', 'l0', 1),
              task('t1', 's1', 'l0', 0),
              task('t3', 's1', 'l1', 0),
            ]),
          ]),
        ],
        [swimLane('l0', 0, true), swimLane('l1', 1)],
      )

      // Act
      const { tasksByCell } = buildBoardLayout(details)

      // Assert — the two lanes of the same step are separate cells.
      expect(tasksByCell.get(cellKey('s1', 'l0'))?.map((t) => t.id)).toEqual([
        't1',
        't2',
      ])
      expect(tasksByCell.get(cellKey('s1', 'l1'))?.map((t) => t.id)).toEqual([
        't3',
      ])
    })

    it('omits cells that hold no tasks', () => {
      // Arrange
      const details = map([goal('g1', 0, [step('s1', 'g1', 0)])])

      // Act
      const { tasksByCell } = buildBoardLayout(details)

      // Assert — the board renders empty cells from the grid geometry, not from this map.
      expect(tasksByCell.get(cellKey('s1', 'lane-default'))).toBeUndefined()
    })
  })
})
