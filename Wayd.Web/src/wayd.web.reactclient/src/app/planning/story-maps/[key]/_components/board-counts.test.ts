import {
  StoryMapDetailsDto,
  StoryMapGoalDto,
  StoryMapStepDto,
  StoryMapTaskDto,
} from '@/src/services/wayd-api'
import { countBoard, countTasksByLane } from './board-counts'

/**
 * The counts must agree with what the board shows as lit: steps and tasks match on their own persona
 * tags, a goal matches when anything beneath it does.
 */

const task = (
  id: string,
  personaIds: string[] = [],
  swimLaneId = 'lane-default',
): StoryMapTaskDto =>
  ({
    id,
    stepId: 's1',
    swimLaneId,
    title: id,
    order: 0,
    personaIds,
    checklist: [],
    checklistCompletedCount: 0,
    checklistTotalCount: 0,
  }) as StoryMapTaskDto

const step = (
  id: string,
  personaIds: string[] = [],
  tasks: StoryMapTaskDto[] = [],
): StoryMapStepDto =>
  ({
    id,
    goalId: 'g1',
    name: id,
    order: 0,
    personaIds,
    tasks,
  }) as StoryMapStepDto

const goal = (
  id: string,
  steps: StoryMapStepDto[] = [],
  personaIds: string[] = [],
): StoryMapGoalDto =>
  ({ id, name: id, order: 0, personaIds, steps }) as StoryMapGoalDto

const map = (goals: StoryMapGoalDto[]): StoryMapDetailsDto =>
  ({
    id: 'map-1',
    key: 1,
    name: 'Map',
    status: 'Active',
    goals,
    swimLanes: [],
    personas: [],
  }) as StoryMapDetailsDto

describe('countBoard', () => {
  describe('with no filter', () => {
    it('counts everything on the board', () => {
      // Arrange
      const details = map([
        goal('g1', [
          step('s1', [], [task('t1'), task('t2')]),
          step('s2', [], [task('t3')]),
        ]),
        goal('g2', [step('s3')]),
      ])

      // Act
      const counts = countBoard(details, null)

      // Assert
      expect(counts).toEqual({ goals: 2, steps: 3, tasks: 3 })
    })

    it('counts a goal with no steps', () => {
      // Arrange
      const details = map([goal('g1')])

      // Act
      const counts = countBoard(details, null)

      // Assert
      expect(counts).toEqual({ goals: 1, steps: 0, tasks: 0 })
    })

    it('returns zeroes for an empty board', () => {
      // Arrange
      const details = map([])

      // Act
      const counts = countBoard(details, null)

      // Assert
      expect(counts).toEqual({ goals: 0, steps: 0, tasks: 0 })
    })
  })

  describe('with a persona selected', () => {
    it('counts only steps and tasks tagged with that persona', () => {
      // Arrange
      const details = map([
        goal('g1', [
          step('s1', ['p1'], [task('t1', ['p1']), task('t2', ['p2'])]),
          step('s2', ['p2'], [task('t3', ['p1'])]),
        ]),
      ])

      // Act
      const counts = countBoard(details, 'p1')

      // Assert
      expect(counts).toEqual({ goals: 1, steps: 1, tasks: 2 })
    })

    it('counts a goal whose step matches even when no task does', () => {
      // Arrange
      const details = map([goal('g1', [step('s1', ['p1'], [task('t1')])])])

      // Act
      const counts = countBoard(details, 'p1')

      // Assert
      expect(counts).toEqual({ goals: 1, steps: 1, tasks: 0 })
    })

    it('counts a goal whose task matches even when no step does', () => {
      // Arrange
      const details = map([goal('g1', [step('s1', [], [task('t1', ['p1'])])])])

      // Act
      const counts = countBoard(details, 'p1')

      // Assert
      expect(counts).toEqual({ goals: 1, steps: 0, tasks: 1 })
    })

    it('counts a goal tagged directly even when nothing beneath it matches', () => {
      // Arrange
      const details = map([goal('g1', [step('s1', [], [task('t1')])], ['p1'])])

      // Act
      const counts = countBoard(details, 'p1')

      // Assert
      expect(counts).toEqual({ goals: 1, steps: 0, tasks: 0 })
    })

    it('counts a goal once however many descendants match', () => {
      // Arrange
      const details = map([
        goal('g1', [
          step('s1', ['p1'], [task('t1', ['p1']), task('t2', ['p1'])]),
          step('s2', ['p1']),
        ]),
      ])

      // Act
      const counts = countBoard(details, 'p1')

      // Assert
      expect(counts).toEqual({ goals: 1, steps: 2, tasks: 2 })
    })

    it('excludes a goal with nothing matching beneath it', () => {
      // Arrange
      const details = map([
        goal('g1', [step('s1', ['p1'])]),
        goal('g2', [step('s2', ['p2'], [task('t1', ['p2'])])]),
      ])

      // Act
      const counts = countBoard(details, 'p1')

      // Assert
      expect(counts).toEqual({ goals: 1, steps: 1, tasks: 0 })
    })

    it('counts a node tagged with several personas under each of them', () => {
      // Arrange
      const details = map([goal('g1', [step('s1', ['p1', 'p2'])])])

      // Act
      const forP1 = countBoard(details, 'p1')
      const forP2 = countBoard(details, 'p2')

      // Assert
      expect(forP1).toEqual({ goals: 1, steps: 1, tasks: 0 })
      expect(forP2).toEqual({ goals: 1, steps: 1, tasks: 0 })
    })

    it('returns zeroes when nothing carries the persona', () => {
      // Arrange
      const details = map([goal('g1', [step('s1', [], [task('t1')])])])

      // Act
      const counts = countBoard(details, 'p1')

      // Assert
      expect(counts).toEqual({ goals: 0, steps: 0, tasks: 0 })
    })
  })
})

describe('countTasksByLane', () => {
  it('counts each lane separately, across every goal and step', () => {
    // Arrange
    const details = map([
      goal('g1', [
        step('s1', [], [task('t1', [], 'lane-a'), task('t2', [], 'lane-b')]),
        step('s2', [], [task('t3', [], 'lane-a')]),
      ]),
      goal('g2', [step('s3', [], [task('t4', [], 'lane-a')])]),
    ])

    // Act
    const counts = countTasksByLane(details, null)

    // Assert
    expect(counts.get('lane-a')).toBe(3)
    expect(counts.get('lane-b')).toBe(1)
  })

  it('omits a lane holding no tasks', () => {
    // Arrange
    const details = map([goal('g1', [step('s1', [], [task('t1', [], 'lane-a')])])])

    // Act
    const counts = countTasksByLane(details, null)

    // Assert — the caller defaults a missing lane to zero.
    expect(counts.has('lane-b')).toBe(false)
  })

  it('counts only tasks tagged with the selected persona', () => {
    // Arrange
    const details = map([
      goal('g1', [
        step(
          's1',
          [],
          [
            task('t1', ['p1'], 'lane-a'),
            task('t2', ['p2'], 'lane-a'),
            task('t3', ['p1'], 'lane-b'),
          ],
        ),
      ]),
    ])

    // Act
    const counts = countTasksByLane(details, 'p1')

    // Assert
    expect(counts.get('lane-a')).toBe(1)
    expect(counts.get('lane-b')).toBe(1)
  })

  it('drops a lane entirely when the filter leaves it empty', () => {
    // Arrange
    const details = map([
      goal('g1', [
        step('s1', [], [task('t1', ['p1'], 'lane-a'), task('t2', [], 'lane-b')]),
      ]),
    ])

    // Act
    const counts = countTasksByLane(details, 'p1')

    // Assert
    expect(counts.get('lane-a')).toBe(1)
    expect(counts.has('lane-b')).toBe(false)
  })

  it('ignores a step persona tag when counting its tasks', () => {
    // Arrange — a step carrying the persona does not make its untagged tasks match.
    const details = map([
      goal('g1', [step('s1', ['p1'], [task('t1', [], 'lane-a')])]),
    ])

    // Act
    const counts = countTasksByLane(details, 'p1')

    // Assert
    expect(counts.has('lane-a')).toBe(false)
  })

  it('returns an empty map for a board with no tasks', () => {
    // Arrange
    const details = map([goal('g1', [step('s1')])])

    // Act
    const counts = countTasksByLane(details, null)

    // Assert
    expect(counts.size).toBe(0)
  })
})
