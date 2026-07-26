import {
  StoryMapDetailsDto,
  StoryMapGoalDto,
  StoryMapStepDto,
  StoryMapSwimLaneDto,
  StoryMapTaskDto,
} from '@/src/services/wayd-api'
import { buildBoardLayout, cellKey } from './board-layout'

/**
 * The board is one CSS grid, so these placements decide what lines up with what. Two invariants
 * matter most and are asserted throughout:
 *
 *  - a goal header spans exactly the column tracks of its own steps, so goals stay aligned above
 *    the steps they own;
 *  - `stepColumnCount` counts a placeholder track for every step-less goal. Deriving the grid
 *    template from `steps.length` instead lets goal headers collide in too few tracks, and the
 *    browser then sizes the overflow columns by content rather than `1fr`, so goals stop sharing
 *    width equally.
 */

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
      const { steps, lastColumn, stepColumnCount } = buildBoardLayout(details)

      // Assert — column 1 is the label column, so steps start at 2.
      expect(steps.map((s) => [s.step.id, s.column])).toEqual([
        ['s1', 2],
        ['s2', 3],
        ['s3', 4],
      ])
      expect(lastColumn).toBe(4)
      expect(stepColumnCount).toBe(3)
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
      const { goals, steps, stepColumnCount } = buildBoardLayout(details)

      // Assert — three tracks for three goals, despite there being no steps at all.
      expect(steps).toHaveLength(0)
      expect(stepColumnCount).toBe(3)
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
      const { goals, steps, stepColumnCount } = buildBoardLayout(details)

      // Assert — g2 occupies column 4, so g3's step lands at 5.
      expect(
        goals.map((g) => [g.goal.id, g.columnStart, g.columnSpan]),
      ).toEqual([
        ['g1', 2, 2],
        ['g2', 4, 1],
        ['g3', 5, 1],
      ])
      expect(steps.map((s) => s.column)).toEqual([2, 3, 5])
      expect(stepColumnCount).toBe(4)
    })

    it('declares one track for a map with no goals at all', () => {
      // Arrange / Act
      const { stepColumnCount } = buildBoardLayout(map([]))

      // Assert — the grid template still needs a track to name.
      expect(stepColumnCount).toBe(1)
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
      expect(
        swimLanes.map((l) => [l.swimLane.id, l.headerRow, l.row]),
      ).toEqual([
        ['l0', 3, 4],
        ['l1', 5, 6],
      ])
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
