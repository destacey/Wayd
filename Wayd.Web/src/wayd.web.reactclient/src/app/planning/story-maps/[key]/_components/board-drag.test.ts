import {
  StoryMapDetailsDto,
  StoryMapGoalDto,
  StoryMapStepDto,
  StoryMapSwimLaneDto,
  StoryMapTaskDto,
} from '@/src/services/wayd-api'
import { buildBoardLayout } from './board-layout'
import {
  buildDragIndex,
  emptyStepSlotId,
  isValidDropTarget,
  resolveDrop,
  taskCellId,
  type DropResult,
  type DropSide,
} from './board-drag'

/**
 * `newOrder` is interpreted with remove-then-insert semantics: the dragged item is pulled out of its
 * list first, then spliced back in at `newOrder`. That makes the drop target's *current* index the
 * correct value in every case, including downward drags — the compensation people reach for is
 * already performed by the removal.
 *
 * Both directions are asserted for every reorderable kind, because getting this wrong is invisible
 * when dragging upward and silently off-by-one when dragging downward.
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

/**
 * Resolve a drop against a freshly built layout/index for the given map.
 *
 * `side` is which half of the target the pointer was over. It defaults to 'before' so tests that do
 * not care read cleanly, but the seam cases below always state it explicitly — 'after' the last step
 * of one goal and 'before' the first step of the next are the same pixel column on screen, and
 * telling them apart is the whole point of the parameter.
 */
const drop = (
  details: StoryMapDetailsDto,
  activeId: string,
  overId: string,
  side: DropSide = 'before',
): DropResult | null => {
  const layout = buildBoardLayout(details)
  return resolveDrop(layout, buildDragIndex(layout), activeId, overId, side)
}

describe('resolveDrop', () => {
  describe('goals', () => {
    const details = map([goal('g1', 0), goal('g2', 1), goal('g3', 2)])

    it('moves a goal after a later goal', () => {
      // Arrange / Act — g1 dropped on the trailing half of g3.
      const result = drop(details, 'g1', 'g3', 'after')

      // Assert — removing g1 first pulls g3 back to index 1, so the end is index 2.
      expect(result).toEqual({ kind: 'goal', goalId: 'g1', newOrder: 2 })
    })

    it('moves a goal before a later goal', () => {
      // Arrange / Act — the leading half of g3 means "between g2 and g3".
      const result = drop(details, 'g1', 'g3', 'before')

      // Assert
      expect(result).toEqual({ kind: 'goal', goalId: 'g1', newOrder: 1 })
    })

    it('moves a goal before an earlier goal', () => {
      // Arrange / Act
      const result = drop(details, 'g3', 'g1', 'before')

      // Assert — nothing ahead of g1 is removed, so it is a straight insert at 0.
      expect(result).toEqual({ kind: 'goal', goalId: 'g3', newOrder: 0 })
    })

    it('moves a goal after an earlier goal', () => {
      // Arrange / Act
      const result = drop(details, 'g3', 'g1', 'after')

      // Assert
      expect(result).toEqual({ kind: 'goal', goalId: 'g3', newOrder: 1 })
    })

    it('ignores a drop that lands a goal back where it started', () => {
      // Arrange / Act — the trailing half of g1 is g1's own position.
      const result = drop(details, 'g1', 'g2', 'before')

      // Assert
      expect(result).toBeNull()
    })

    it('ignores a goal dropped on itself', () => {
      // Arrange / Act / Assert
      expect(drop(details, 'g1', 'g1')).toBeNull()
    })

    it('ignores a goal dropped on a different kind of node', () => {
      // Arrange
      const withSteps = map([goal('g1', 0, [step('s1', 'g1', 0)]), goal('g2', 1)])

      // Act / Assert — a goal has no meaning as a child of a step.
      expect(drop(withSteps, 'g1', 's1')).toBeNull()
    })
  })

  describe('steps', () => {
    const details = map([
      goal('g1', 0, [
        step('s1', 'g1', 0),
        step('s2', 'g1', 1),
        step('s3', 'g1', 2),
      ]),
      goal('g2', 1, [step('s4', 'g2', 0)]),
    ])

    it('reorders a step to the end of its goal', () => {
      // Arrange / Act — the trailing half of g1's last step.
      const result = drop(details, 's1', 's3', 'after')

      // Assert — no targetGoalId: staying in the same goal is a reorder, not a move.
      expect(result).toEqual({ kind: 'step', stepId: 's1', newOrder: 2 })
    })

    it('reorders a step to the start of its goal', () => {
      // Arrange / Act
      const result = drop(details, 's3', 's1', 'before')

      // Assert
      expect(result).toEqual({ kind: 'step', stepId: 's3', newOrder: 0 })
    })

    /**
     * The seam between two goals. On screen the trailing edge of g1's last step and the leading
     * edge of g2's first step are the same pixel column, so the side is the only thing that says
     * which goal the step joins. These two cases must not collapse into one another.
     */
    describe('at the seam between two goals', () => {
      it('joins the earlier goal when dropped after its last step', () => {
        // Arrange / Act — s4 lives in g2; the trailing half of s3 means "last in g1".
        const result = drop(details, 's4', 's3', 'after')

        // Assert — g1 keeps all three of its steps, so the new one lands at index 3.
        expect(result).toEqual({
          kind: 'step',
          stepId: 's4',
          targetGoalId: 'g1',
          newOrder: 3,
        })
      })

      it('joins the later goal when dropped before its first step', () => {
        // Arrange / Act — s3 lives in g1; the leading half of s4 means "first in g2".
        const result = drop(details, 's3', 's4', 'before')

        // Assert
        expect(result).toEqual({
          kind: 'step',
          stepId: 's3',
          targetGoalId: 'g2',
          newOrder: 0,
        })
      })
    })

    it('moves a step onto another goal’s step, indexed within that goal', () => {
      // Arrange / Act — s1 lives in g1; s4 is the only step of g2.
      const result = drop(details, 's1', 's4')

      // Assert — the index is scoped to the destination goal, not the whole board.
      expect(result).toEqual({
        kind: 'step',
        stepId: 's1',
        targetGoalId: 'g2',
        newOrder: 0,
      })
    })

    it('indexes into the middle of a populated destination goal', () => {
      // Arrange
      const twoPopulated = map([
        goal('g1', 0, [step('s1', 'g1', 0), step('s2', 'g1', 1)]),
        goal('g2', 1, [
          step('s3', 'g2', 0),
          step('s4', 'g2', 1),
          step('s5', 'g2', 2),
        ]),
      ])

      // Act — s1 dropped on s4, the middle step of g2.
      const result = drop(twoPopulated, 's1', 's4')

      // Assert — s4's index within g2 is 1, not its board-wide index of 3.
      expect(result).toEqual({
        kind: 'step',
        stepId: 's1',
        targetGoalId: 'g2',
        newOrder: 1,
      })
    })

    it('moves a step backwards into an earlier goal', () => {
      // Arrange / Act — s4 lives in g2; s2 is the middle step of g1.
      const result = drop(details, 's4', 's2', 'before')

      // Assert — arriving from another goal, nothing is removed ahead of the target, so the
      // target's own index is the landing position.
      expect(result).toEqual({
        kind: 'step',
        stepId: 's4',
        targetGoalId: 'g1',
        newOrder: 1,
      })
    })

    it('ignores a step dropped on a goal header', () => {
      // Arrange / Act / Assert — the goals row is never a step target; the empty steps slot under a
      // step-less goal is what accepts the drop.
      expect(drop(details, 's1', 'g2')).toBeNull()
    })

    it('moves a step into a goal that has no steps via its empty slot', () => {
      // Arrange
      const withEmptyGoal = map([
        goal('g1', 0, [step('s1', 'g1', 0)]),
        goal('g2', 1),
      ])

      // Act
      const result = drop(withEmptyGoal, 's1', emptyStepSlotId('g2'))

      // Assert — it becomes the goal's first step.
      expect(result).toEqual({
        kind: 'step',
        stepId: 's1',
        targetGoalId: 'g2',
        newOrder: 0,
      })
    })

    it('ignores a step dropped on the empty slot of its own goal', () => {
      // Arrange — a single-step goal: the slot belongs to the goal the step is already in.
      const soleStep = map([goal('g1', 0, [step('s1', 'g1', 0)])])

      // Act / Assert
      expect(drop(soleStep, 's1', emptyStepSlotId('g1'))).toBeNull()
    })
  })

  describe('tasks', () => {
    const lane = 'lane-default'
    const other = 'lane-2'

    const details = map(
      [
        goal('g1', 0, [
          step('s1', 'g1', 0, [
            task('t1', 's1', lane, 0),
            task('t2', 's1', lane, 1),
            task('t3', 's1', lane, 2),
          ]),
          step('s2', 'g1', 1, [task('t4', 's2', lane, 0)]),
        ]),
      ],
      [swimLane(lane, 0, true), swimLane(other, 1)],
    )

    it('reorders a task to the end of its cell', () => {
      // Arrange / Act — the lower half of the last card. This is the case that regressed twice.
      const result = drop(details, 't1', 't3', 'after')

      // Assert
      expect(result).toEqual({
        kind: 'task',
        taskId: 't1',
        targetStepId: 's1',
        targetSwimLaneId: lane,
        newOrder: 2,
        changedCell: false,
      })
    })

    it('reorders a task into the middle of its cell', () => {
      // Arrange / Act — the upper half of the last card means "before t3".
      const result = drop(details, 't1', 't3', 'before')

      // Assert — removing t1 first pulls t3 back to index 1.
      expect(result).toEqual({
        kind: 'task',
        taskId: 't1',
        targetStepId: 's1',
        targetSwimLaneId: lane,
        newOrder: 1,
        changedCell: false,
      })
    })

    it('reorders a task to the start of its cell', () => {
      // Arrange / Act
      const result = drop(details, 't3', 't1', 'before')

      // Assert
      expect(result).toEqual({
        kind: 'task',
        taskId: 't3',
        targetStepId: 's1',
        targetSwimLaneId: lane,
        newOrder: 0,
        changedCell: false,
      })
    })

    it('ignores a drop that lands a task back where it started', () => {
      // Arrange / Act — the lower half of t1 is t1's own position.
      const result = drop(details, 't1', 't2', 'before')

      // Assert
      expect(result).toBeNull()
    })

    it('moves a task onto a card in another step, taking its index', () => {
      // Arrange / Act
      const result = drop(details, 't1', 't4', 'before')

      // Assert
      expect(result).toEqual({
        kind: 'task',
        taskId: 't1',
        targetStepId: 's2',
        targetSwimLaneId: lane,
        newOrder: 0,
        changedCell: true,
      })
    })

    it('appends when a task is dropped on a populated cell', () => {
      // Arrange / Act — dropping on the cell rather than a card means "add to the end".
      const result = drop(details, 't4', taskCellId('s1', lane))

      // Assert — s1's lane cell already holds three tasks.
      expect(result).toEqual({
        kind: 'task',
        taskId: 't4',
        targetStepId: 's1',
        targetSwimLaneId: lane,
        newOrder: 3,
        changedCell: true,
      })
    })

    it('moves a task into an empty cell in a different swim lane', () => {
      // Arrange / Act — an empty cell has no cards, so the cell itself is the only drop target.
      const result = drop(details, 't1', taskCellId('s1', other))

      // Assert — the step is unchanged but the swim lane is not.
      expect(result).toEqual({
        kind: 'task',
        taskId: 't1',
        targetStepId: 's1',
        targetSwimLaneId: other,
        newOrder: 0,
        changedCell: true,
      })
    })

    it('changes both step and swim lane in a single move', () => {
      // Arrange / Act
      const result = drop(details, 't1', taskCellId('s2', other))

      // Assert
      expect(result).toEqual({
        kind: 'task',
        taskId: 't1',
        targetStepId: 's2',
        targetSwimLaneId: other,
        newOrder: 0,
        changedCell: true,
      })
    })

    it('ignores a task dropped on the cell it already occupies', () => {
      // Arrange / Act / Assert — a no-op drop must not issue a request.
      expect(drop(details, 't1', taskCellId('s1', lane))).toBeNull()
    })

    it('ignores a task dropped on itself', () => {
      // Arrange / Act / Assert
      expect(drop(details, 't1', 't1')).toBeNull()
    })

    it('ignores a task dropped on a goal', () => {
      // Arrange / Act / Assert — goals are not task containers.
      expect(drop(details, 't1', 'g1')).toBeNull()
    })
  })

  describe('swim lanes', () => {
    const details = map(
      [goal('g1', 0)],
      [swimLane('l0', 0, true), swimLane('l1', 1), swimLane('l2', 2)],
    )

    it('moves a swim lane before another lane', () => {
      // Arrange / Act
      const result = drop(details, 'l2', 'l1', 'before')

      // Assert
      expect(result).toEqual({
        kind: 'swimLane',
        swimLaneId: 'l2',
        newOrder: 1,
      })
    })

    it('refuses to displace the default lane', () => {
      // Arrange / Act — the domain pins the default lane at position 0, so nothing may land before
      // it.
      const result = drop(details, 'l2', 'l0', 'before')

      // Assert
      expect(result).toBeNull()
    })

    it('allows a lane to land immediately after the default lane', () => {
      // Arrange / Act — position 1 is fine; only position 0 is reserved.
      const result = drop(details, 'l2', 'l0', 'after')

      // Assert
      expect(result).toEqual({
        kind: 'swimLane',
        swimLaneId: 'l2',
        newOrder: 1,
      })
    })

    it('ignores a swim lane dropped on itself', () => {
      // Arrange / Act / Assert
      expect(drop(details, 'l1', 'l1')).toBeNull()
    })
  })

  it('ignores a drag whose active id is not a board node', () => {
    // Arrange / Act / Assert
    expect(drop(map([goal('g1', 0)]), 'unknown', 'g1')).toBeNull()
  })
})

/**
 * Collision detection is filtered through this, so an illegal target never highlights, opens a gap,
 * or draws an insertion line during the drag. Every kind-to-kind pairing is asserted: a gap here
 * means a forbidden drop that looks accepted right up until release, which reads as a broken drag.
 */
describe('isValidDropTarget', () => {
  const lane = 'lane-default'
  const details = map(
    [
      goal('g1', 0, [
        step('s1', 'g1', 0, [task('t1', 's1', lane, 0)]),
        step('s2', 'g1', 1),
      ]),
      goal('g2', 1),
    ],
    [swimLane(lane, 0, true), swimLane('l1', 1)],
  )

  const layout = buildBoardLayout(details)
  const index = buildDragIndex(layout)

  const isValid = (activeId: string, overId: string) =>
    isValidDropTarget(index, activeId, overId)

  const cell = taskCellId('s1', lane)

  describe.each([
    // active kind, active id, then what it may and may not land on.
    {
      kind: 'goal',
      activeId: 'g1',
      allowed: [['another goal', 'g2']],
      blocked: [
        ['a step', 's1'],
        ['a task', 't1'],
        ['a task cell', cell],
        ['a swim lane', 'l1'],
        ['an empty steps slot', emptyStepSlotId('g2')],
      ],
    },
    {
      kind: 'step',
      activeId: 's1',
      allowed: [
        ['another step', 's2'],
        ['the empty steps slot of a step-less goal', emptyStepSlotId('g2')],
      ],
      blocked: [
        ['a goal header', 'g2'],
        ['a task', 't1'],
        ['a task cell', cell],
        ['a swim lane', 'l1'],
      ],
    },
    {
      kind: 'task',
      activeId: 't1',
      allowed: [['a task cell', cell]],
      blocked: [
        ['a goal', 'g1'],
        ['a step', 's1'],
        ['a swim lane', 'l1'],
        ['an empty steps slot', emptyStepSlotId('g2')],
      ],
    },
    {
      kind: 'swimLane',
      activeId: 'l1',
      allowed: [],
      blocked: [
        ['a goal', 'g1'],
        ['a step', 's1'],
        ['a task', 't1'],
        ['a task cell', cell],
        ['an empty steps slot', emptyStepSlotId('g2')],
      ],
    },
  ])('a dragged $kind', ({ activeId, allowed, blocked }) => {
    if (allowed.length > 0) {
      it.each(allowed)('may be dropped on %s', (_label, overId) => {
        // Arrange / Act / Assert
        expect(isValid(activeId, overId)).toBe(true)
      })
    }

    it.each(blocked)('may not be dropped on %s', (_label, overId) => {
      // Arrange / Act / Assert
      expect(isValid(activeId, overId)).toBe(false)
    })
  })

  it('lets a task land on another task', () => {
    // Arrange — a second task, so there is a sibling to target.
    const twoTasks = map([
      goal('g1', 0, [
        step('s1', 'g1', 0, [
          task('t1', 's1', lane, 0),
          task('t2', 's1', lane, 1),
        ]),
      ]),
    ])
    const twoTaskIndex = buildDragIndex(buildBoardLayout(twoTasks))

    // Act / Assert
    expect(isValidDropTarget(twoTaskIndex, 't1', 't2')).toBe(true)
  })

  it('lets a swim lane land on another swim lane', () => {
    // Arrange / Act / Assert
    expect(isValid('l1', lane)).toBe(true)
  })

  it('treats a node as a valid target for itself, leaving no-ops to resolveDrop', () => {
    // Arrange / Act / Assert — the hovered item must stay responsive under its own cursor; the
    // no-op is rejected at drop time instead.
    expect(isValid('g1', 'g1')).toBe(true)
  })

  it('rejects an unknown active id', () => {
    // Arrange / Act / Assert
    expect(isValid('unknown', 'g1')).toBe(false)
  })

  it('rejects an unknown target id', () => {
    // Arrange / Act / Assert
    expect(isValid('g1', 'unknown')).toBe(false)
  })
})
