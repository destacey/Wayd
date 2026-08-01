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
 * Both drag directions are asserted for every reorderable kind: the index arithmetic is asymmetric
 * under remove-then-insert (see `landingIndex`), so an error there is invisible in one direction and
 * off by one in the other.
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
      const withSteps = map([
        goal('g1', 0, [step('s1', 'g1', 0)]),
        goal('g2', 1),
      ])

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

    it('moves a task to the end of its own cell when dropped on the cell', () => {
      // Arrange / Act — the empty space below the last card means "append", including within the
      // cell the task already occupies.
      const result = drop(details, 't1', taskCellId('s1', lane))

      // Assert — removing t1 first leaves two tasks, so the end is index 2.
      expect(result).toEqual({
        kind: 'task',
        taskId: 't1',
        targetStepId: 's1',
        targetSwimLaneId: lane,
        newOrder: 2,
        changedCell: false,
      })
    })

    it('ignores appending a task that already ends its cell', () => {
      // Arrange / Act / Assert — t3 is already last, so a no-op must not issue a request.
      expect(drop(details, 't3', taskCellId('s1', lane))).toBeNull()
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

describe('collapsed swim lanes', () => {
  const open = 'lane-default'
  const folded = 'l1'

  const details = map(
    [
      goal('g1', 0, [
        step('s1', 'g1', 0, [
          task('t-open', 's1', open, 0),
          task('t-folded', 's1', folded, 0),
        ]),
      ]),
    ],
    [swimLane(open, 0, true), swimLane(folded, 1)],
  )

  const layout = buildBoardLayout(details, { swimLaneIds: new Set([folded]) })
  const index = buildDragIndex(layout)

  it('does not index a task in a collapsed lane', () => {
    // Arrange / Act / Assert — it renders no card, so it is not a draggable node.
    expect(index.kindById.get('t-folded')).toBeUndefined()
    expect(index.kindById.get('t-open')).toBe('task')
  })

  it('rejects a collapsed lane task as a drag source', () => {
    // Arrange / Act / Assert
    expect(isValidDropTarget(index, 't-folded', taskCellId('s1', open))).toBe(
      false,
    )
  })

  it('rejects a collapsed lane task as a drop target', () => {
    // Arrange / Act / Assert — dropping onto a hidden card would land the task out of sight.
    expect(isValidDropTarget(index, 't-open', 't-folded')).toBe(false)
    expect(
      resolveDrop(layout, index, 't-open', 't-folded', 'before'),
    ).toBeNull()
  })

  it('appends to an empty cell without counting the collapsed lane task', () => {
    // Arrange / Act — the open lane's own cell still resolves normally.
    const result = resolveDrop(
      layout,
      index,
      't-open',
      taskCellId('s1', open),
      'before',
    )

    // Assert — already the only (and last) task there, so appending changes nothing.
    expect(result).toBeNull()
  })

  it('still allows a collapsed lane itself to be reordered', () => {
    // Arrange — a third lane, so there is somewhere for the collapsed one to actually go.
    const threeLanes = map(
      [goal('g1', 0, [step('s1', 'g1', 0)])],
      [swimLane(open, 0, true), swimLane(folded, 1), swimLane('l2', 2)],
    )
    const threeLaneLayout = buildBoardLayout(threeLanes, {
      swimLaneIds: new Set([folded]),
    })

    // Act — folding a lane away must not pin it in place.
    const result = resolveDrop(
      threeLaneLayout,
      buildDragIndex(threeLaneLayout),
      folded,
      'l2',
      'after',
    )

    // Assert — removing l1 first pulls l2 back to index 1, so the end is index 2.
    expect(result).toEqual({
      kind: 'swimLane',
      swimLaneId: folded,
      newOrder: 2,
    })
  })
})

describe('collapsed goals', () => {
  const lane = 'lane-default'

  const details = map([
    goal('g1', 0, [step('s1', 'g1', 0, [task('t1', 's1', lane, 0)])]),
    goal('g2', 1, [step('s2', 'g2', 0, [task('t2', 's2', lane, 0)])]),
    goal('g3', 2, [step('s3', 'g3', 0)]),
  ])

  const layout = buildBoardLayout(details, { goalIds: new Set(['g2']) })
  const index = buildDragIndex(layout)

  it('does not index the steps or tasks of a collapsed goal', () => {
    // Arrange / Act / Assert — neither renders a cell, so neither is a drag node.
    expect(index.kindById.get('s2')).toBeUndefined()
    expect(index.kindById.get('t2')).toBeUndefined()
    expect(index.kindById.get('s1')).toBe('step')
    expect(index.kindById.get('t1')).toBe('task')
  })

  it('rejects a collapsed goal’s step as a drop target', () => {
    // Arrange / Act / Assert — dropping there would land the step out of sight.
    expect(isValidDropTarget(index, 's1', 's2')).toBe(false)
    expect(resolveDrop(layout, index, 's1', 's2', 'before')).toBeNull()
  })

  it('rejects a task dropped into a collapsed goal’s cell', () => {
    // Arrange / Act / Assert
    expect(isValidDropTarget(index, 't1', taskCellId('s2', lane))).toBe(false)
  })

  it('still allows the collapsed goal itself to be reordered', () => {
    // Arrange / Act — folding a goal away must not pin it in place.
    const result = resolveDrop(layout, index, 'g2', 'g1', 'before')

    // Assert
    expect(result).toEqual({ kind: 'goal', goalId: 'g2', newOrder: 0 })
  })

  it('still allows an expanded goal to reorder across a collapsed one', () => {
    // Arrange / Act — g1 past g3, with the collapsed g2 in between.
    const result = resolveDrop(layout, index, 'g1', 'g3', 'after')

    // Assert — removing g1 first pulls g3 back to index 1, so the end is index 2.
    expect(result).toEqual({ kind: 'goal', goalId: 'g1', newOrder: 2 })
  })

  it('still reorders steps among the goals that remain expanded', () => {
    // Arrange / Act — s1 joins g3, across the collapsed g2 sitting between them on screen.
    const result = resolveDrop(layout, index, 's1', 's3', 'after')

    // Assert
    expect(result).toEqual({
      kind: 'step',
      stepId: 's1',
      targetGoalId: 'g3',
      newOrder: 1,
    })
  })
})

/** A well-formed cell id can still name a cell no longer on the board once a collapse hides it. */
describe('task cells that are not rendered', () => {
  const details = map(
    [
      goal('g1', 0, [step('s1', 'g1', 0, [task('t1', 's1', 'l0', 0)])]),
      goal('g2', 1, [step('s2', 'g2', 0)]),
    ],
    [swimLane('l0', 0, true), swimLane('l1', 1)],
  )

  const layout = buildBoardLayout(details, {
    goalIds: new Set(['g2']),
    swimLaneIds: new Set(['l1']),
  })
  const index = buildDragIndex(layout)

  it('accepts a cell on a rendered step crossed with an expanded lane', () => {
    // Arrange / Act / Assert — the control case: this cell really is on the board.
    expect(isValidDropTarget(index, 't1', taskCellId('s1', 'l0'))).toBe(true)
  })

  it('rejects a well-formed cell id naming a collapsed lane', () => {
    // Arrange / Act / Assert — s1 renders, but l1 owns no task row.
    const hidden = taskCellId('s1', 'l1')
    expect(isValidDropTarget(index, 't1', hidden)).toBe(false)
    expect(resolveDrop(layout, index, 't1', hidden)).toBeNull()
  })

  it('rejects a well-formed cell id naming a collapsed goal’s step', () => {
    // Arrange / Act / Assert — l0 is expanded, but s2 lives inside the collapsed g2.
    const hidden = taskCellId('s2', 'l0')
    expect(isValidDropTarget(index, 't1', hidden)).toBe(false)
    expect(resolveDrop(layout, index, 't1', hidden)).toBeNull()
  })

  it('rejects a cell id for a step that does not exist at all', () => {
    // Arrange / Act / Assert
    expect(isValidDropTarget(index, 't1', taskCellId('nope', 'l0'))).toBe(false)
  })
})
