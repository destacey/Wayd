import {
  StoryMapDetailsDto,
  StoryMapGoalDto,
  StoryMapStepDto,
  StoryMapSwimLaneDto,
  StoryMapTaskDto,
} from '@/src/services/wayd-api'
import {
  applyMoveStep,
  applyMoveTask,
  applyRemoveSwimLane,
  findTaskInDraft,
  recountChecklist,
  reorderInPlace,
  togglePersonaId,
} from './story-map-patches'

/**
 * These run before the server responds, so a mistake shows as the board jumping to a wrong state and
 * silently correcting on refetch. `order` is contiguous and parent-scoped, so every assertion checks
 * the full ordering of both the list a node left and the one it joined — not just where it landed.
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

/** Ids of a goal's steps in order, with their order values, e.g. ['s1:0', 's2:1']. */
const stepOrder = (details: StoryMapDetailsDto, goalId: string) =>
  [...(details.goals.find((g) => g.id === goalId)?.steps ?? [])]
    .sort((a, b) => a.order - b.order)
    .map((s) => `${s.id}:${s.order}`)

/** Ids of a (step × lane) cell's tasks in order, with their order values. */
const taskOrder = (
  details: StoryMapDetailsDto,
  stepId: string,
  swimLaneId: string,
) =>
  details.goals
    .flatMap((g) => g.steps)
    .filter((s) => s.id === stepId)
    .flatMap((s) => s.tasks)
    .filter((t) => t.swimLaneId === swimLaneId)
    .sort((a, b) => a.order - b.order)
    .map((t) => `${t.id}:${t.order}`)

describe('reorderInPlace', () => {
  it('moves an item forward and renumbers contiguously', () => {
    // Arrange
    const items = [
      { id: 'a', order: 0 },
      { id: 'b', order: 1 },
      { id: 'c', order: 2 },
    ]

    // Act
    reorderInPlace(items, 'a', 2)

    // Assert
    expect([...items].sort((x, y) => x.order - y.order).map((i) => i.id)).toEqual(
      ['b', 'c', 'a'],
    )
    expect(items.map((i) => i.order).sort()).toEqual([0, 1, 2])
  })

  it('moves an item backward', () => {
    // Arrange
    const items = [
      { id: 'a', order: 0 },
      { id: 'b', order: 1 },
      { id: 'c', order: 2 },
    ]

    // Act
    reorderInPlace(items, 'c', 0)

    // Assert
    expect([...items].sort((x, y) => x.order - y.order).map((i) => i.id)).toEqual(
      ['c', 'a', 'b'],
    )
  })

  it('clamps an out-of-range position to the end', () => {
    // Arrange
    const items = [
      { id: 'a', order: 0 },
      { id: 'b', order: 1 },
    ]

    // Act
    reorderInPlace(items, 'a', 99)

    // Assert
    expect([...items].sort((x, y) => x.order - y.order).map((i) => i.id)).toEqual(
      ['b', 'a'],
    )
  })

  it('leaves the list untouched for an unknown id', () => {
    // Arrange
    const items = [
      { id: 'a', order: 0 },
      { id: 'b', order: 1 },
    ]

    // Act
    reorderInPlace(items, 'missing', 0)

    // Assert
    expect(items).toEqual([
      { id: 'a', order: 0 },
      { id: 'b', order: 1 },
    ])
  })
})

describe('applyMoveStep', () => {
  const details = () =>
    map([
      goal('g1', 0, [
        step('s1', 'g1', 0),
        step('s2', 'g1', 1),
        step('s3', 'g1', 2),
      ]),
      goal('g2', 1, [step('s4', 'g2', 0), step('s5', 'g2', 1)]),
    ])

  it('re-parents a step and renumbers both goals', () => {
    // Arrange
    const draft = details()

    // Act — s1 becomes the first step of g2.
    applyMoveStep(draft, 's1', { targetGoalId: 'g2', newOrder: 0 })

    // Assert — the gap s1 left in g1 closes, and g2 renumbers around the arrival.
    expect(stepOrder(draft, 'g1')).toEqual(['s2:0', 's3:1'])
    expect(stepOrder(draft, 'g2')).toEqual(['s1:0', 's4:1', 's5:2'])
  })

  it('updates the moved step’s goalId', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveStep(draft, 's1', { targetGoalId: 'g2', newOrder: 0 })

    // Assert — a stale goalId would misplace the step on the next render.
    const moved = draft.goals
      .flatMap((g) => g.steps)
      .find((s) => s.id === 's1')
    expect(moved?.goalId).toBe('g2')
  })

  it('inserts into the middle of the destination goal', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveStep(draft, 's1', { targetGoalId: 'g2', newOrder: 1 })

    // Assert
    expect(stepOrder(draft, 'g2')).toEqual(['s4:0', 's1:1', 's5:2'])
  })

  it('appends when the position is past the end', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveStep(draft, 's1', { targetGoalId: 'g2', newOrder: 99 })

    // Assert
    expect(stepOrder(draft, 'g2')).toEqual(['s4:0', 's5:1', 's1:2'])
  })

  it('reorders rather than duplicating when the goal is unchanged', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveStep(draft, 's1', { targetGoalId: 'g1', newOrder: 2 })

    // Assert — a naive remove-then-insert across the same list could leave the step in twice.
    expect(stepOrder(draft, 'g1')).toEqual(['s2:0', 's3:1', 's1:2'])
  })

  it('ignores an unknown step', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveStep(draft, 'missing', { targetGoalId: 'g2', newOrder: 0 })

    // Assert
    expect(stepOrder(draft, 'g1')).toEqual(['s1:0', 's2:1', 's3:2'])
    expect(stepOrder(draft, 'g2')).toEqual(['s4:0', 's5:1'])
  })

  it('ignores an unknown destination goal', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveStep(draft, 's1', { targetGoalId: 'missing', newOrder: 0 })

    // Assert — the step must not vanish from its current goal.
    expect(stepOrder(draft, 'g1')).toEqual(['s1:0', 's2:1', 's3:2'])
  })
})

describe('applyMoveTask', () => {
  const lane = 'lane-default'
  const other = 'lane-2'

  const details = () =>
    map(
      [
        goal('g1', 0, [
          step('s1', 'g1', 0, [
            task('t1', 's1', lane, 0),
            task('t2', 's1', lane, 1),
            task('t3', 's1', lane, 2),
            // A second lane in the same step, which must be left alone by same-lane moves.
            task('t4', 's1', other, 0),
          ]),
          step('s2', 'g1', 1, [task('t5', 's2', lane, 0)]),
        ]),
      ],
      [swimLane(lane, 0, true), swimLane(other, 1)],
    )

  it('reorders within a cell', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveTask(draft, 't1', {
      targetStepId: 's1',
      targetSwimLaneId: lane,
      newOrder: 2,
    })

    // Assert
    expect(taskOrder(draft, 's1', lane)).toEqual(['t2:0', 't3:1', 't1:2'])
  })

  it('leaves the other lane of the same step untouched', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveTask(draft, 't1', {
      targetStepId: 's1',
      targetSwimLaneId: lane,
      newOrder: 2,
    })

    // Assert — order is scoped per cell, so the neighbouring lane must not renumber.
    expect(taskOrder(draft, 's1', other)).toEqual(['t4:0'])
  })

  it('moves a task to another step, renumbering both cells', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveTask(draft, 't1', {
      targetStepId: 's2',
      targetSwimLaneId: lane,
      newOrder: 0,
    })

    // Assert
    expect(taskOrder(draft, 's1', lane)).toEqual(['t2:0', 't3:1'])
    expect(taskOrder(draft, 's2', lane)).toEqual(['t1:0', 't5:1'])
  })

  it('moves a task to another swim lane in the same step', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveTask(draft, 't1', {
      targetStepId: 's1',
      targetSwimLaneId: other,
      newOrder: 0,
    })

    // Assert — the vacated lane closes its gap, the destination renumbers around the arrival.
    expect(taskOrder(draft, 's1', lane)).toEqual(['t2:0', 't3:1'])
    expect(taskOrder(draft, 's1', other)).toEqual(['t1:0', 't4:1'])
  })

  it('changes both step and swim lane in one move', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveTask(draft, 't1', {
      targetStepId: 's2',
      targetSwimLaneId: other,
      newOrder: 0,
    })

    // Assert
    expect(taskOrder(draft, 's1', lane)).toEqual(['t2:0', 't3:1'])
    expect(taskOrder(draft, 's2', other)).toEqual(['t1:0'])
  })

  it('updates the moved task’s stepId and swimLaneId', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveTask(draft, 't1', {
      targetStepId: 's2',
      targetSwimLaneId: other,
      newOrder: 0,
    })

    // Assert — stale keys would bucket the task into the wrong cell on the next render.
    const moved = draft.goals
      .flatMap((g) => g.steps)
      .flatMap((s) => s.tasks)
      .find((t) => t.id === 't1')
    expect(moved?.stepId).toBe('s2')
    expect(moved?.swimLaneId).toBe(other)
  })

  it('moves a task into an empty cell', () => {
    // Arrange
    const draft = details()

    // Act — s2 has nothing in the non-default lane.
    applyMoveTask(draft, 't1', {
      targetStepId: 's2',
      targetSwimLaneId: other,
      newOrder: 0,
    })

    // Assert
    expect(taskOrder(draft, 's2', other)).toEqual(['t1:0'])
  })

  it('keeps every task on the board exactly once', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveTask(draft, 't1', {
      targetStepId: 's2',
      targetSwimLaneId: other,
      newOrder: 0,
    })

    // Assert — the rebuild of toStep.tasks partitions by lane, so a slip could drop or clone one.
    const ids = draft.goals
      .flatMap((g) => g.steps)
      .flatMap((s) => s.tasks)
      .map((t) => t.id)
      .sort()
    expect(ids).toEqual(['t1', 't2', 't3', 't4', 't5'])
  })

  it('ignores an unknown task', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveTask(draft, 'missing', {
      targetStepId: 's2',
      targetSwimLaneId: lane,
      newOrder: 0,
    })

    // Assert
    expect(taskOrder(draft, 's1', lane)).toEqual(['t1:0', 't2:1', 't3:2'])
    expect(taskOrder(draft, 's2', lane)).toEqual(['t5:0'])
  })

  it('ignores an unknown destination step', () => {
    // Arrange
    const draft = details()

    // Act
    applyMoveTask(draft, 't1', {
      targetStepId: 'missing',
      targetSwimLaneId: lane,
      newOrder: 0,
    })

    // Assert — the task must not be removed from where it already lives.
    expect(taskOrder(draft, 's1', lane)).toEqual(['t1:0', 't2:1', 't3:2'])
  })
})

describe('applyRemoveSwimLane', () => {
  const lane = 'lane-default'
  const doomed = 'lane-2'

  const details = () =>
    map(
      [
        goal('g1', 0, [
          step('s1', 'g1', 0, [
            task('t1', 's1', lane, 0),
            task('t2', 's1', doomed, 0),
            task('t3', 's1', doomed, 1),
          ]),
        ]),
      ],
      [swimLane(lane, 0, true), swimLane(doomed, 1), swimLane('lane-3', 2)],
    )

  it('removes the lane and renumbers the rest', () => {
    // Arrange
    const draft = details()

    // Act
    applyRemoveSwimLane(draft, doomed)

    // Assert
    expect(draft.swimLanes.map((l) => `${l.id}:${l.order}`)).toEqual([
      `${lane}:0`,
      'lane-3:1',
    ])
  })

  it('reassigns the lane’s tasks to the default lane rather than deleting them', () => {
    // Arrange
    const draft = details()

    // Act
    applyRemoveSwimLane(draft, doomed)

    // Assert — the domain moves them; deleting here would lose work the server keeps.
    const tasks = draft.goals.flatMap((g) => g.steps).flatMap((s) => s.tasks)
    expect(tasks.map((t) => t.id).sort()).toEqual(['t1', 't2', 't3'])
    expect(tasks.every((t) => t.swimLaneId === lane)).toBe(true)
  })

  it('appends reassigned tasks after the ones already in the default lane', () => {
    // Arrange — t1 already occupies the default cell at order 0.
    const draft = details()

    // Act
    applyRemoveSwimLane(draft, doomed)

    // Assert — matching Step.ReassignTasksToSwimLane. Keeping the moved tasks' old orders would
    // collide with t1 and interleave the cell, then jump when the refetch lands.
    expect(taskOrder(draft, 's1', lane)).toEqual(['t1:0', 't2:1', 't3:2'])
  })

  it('appends per step, so each cell numbers from its own existing tasks', () => {
    // Arrange — two steps, each holding one default-lane task and one doomed-lane task.
    const draft = map(
      [
        goal('g1', 0, [
          step('s1', 'g1', 0, [
            task('t1', 's1', lane, 0),
            task('t2', 's1', doomed, 0),
          ]),
          step('s2', 'g1', 1, [
            task('t3', 's2', lane, 0),
            task('t4', 's2', doomed, 0),
          ]),
        ]),
      ],
      [swimLane(lane, 0, true), swimLane(doomed, 1)],
    )

    // Act
    applyRemoveSwimLane(draft, doomed)

    // Assert
    expect(taskOrder(draft, 's1', lane)).toEqual(['t1:0', 't2:1'])
    expect(taskOrder(draft, 's2', lane)).toEqual(['t3:0', 't4:1'])
  })

  it('preserves the relative order of the tasks it moves', () => {
    // Arrange — the doomed lane's tasks are stored out of order.
    const draft = map(
      [
        goal('g1', 0, [
          step('s1', 'g1', 0, [
            task('second', 's1', doomed, 1),
            task('first', 's1', doomed, 0),
          ]),
        ]),
      ],
      [swimLane(lane, 0, true), swimLane(doomed, 1)],
    )

    // Act
    applyRemoveSwimLane(draft, doomed)

    // Assert
    expect(taskOrder(draft, 's1', lane)).toEqual(['first:0', 'second:1'])
  })

  it('leaves a step with nothing in the removed lane untouched', () => {
    // Arrange
    const draft = map(
      [
        goal('g1', 0, [
          step('s1', 'g1', 0, [
            task('t1', 's1', lane, 0),
            task('t2', 's1', lane, 1),
          ]),
        ]),
      ],
      [swimLane(lane, 0, true), swimLane(doomed, 1)],
    )

    // Act
    applyRemoveSwimLane(draft, doomed)

    // Assert
    expect(taskOrder(draft, 's1', lane)).toEqual(['t1:0', 't2:1'])
  })

  it('refuses to remove the default lane', () => {
    // Arrange
    const draft = details()

    // Act
    applyRemoveSwimLane(draft, lane)

    // Assert — there would be nowhere to reassign to.
    expect(draft.swimLanes).toHaveLength(3)
  })

  it('ignores a map with no default lane', () => {
    // Arrange
    const draft = map([goal('g1', 0)], [swimLane('l1', 0), swimLane('l2', 1)])

    // Act
    applyRemoveSwimLane(draft, 'l1')

    // Assert
    expect(draft.swimLanes).toHaveLength(2)
  })
})

describe('togglePersonaId', () => {
  it('adds an unlinked persona', () => {
    // Arrange / Act / Assert
    expect(togglePersonaId(['a'], 'b')).toEqual(['a', 'b'])
  })

  it('removes a linked persona', () => {
    // Arrange / Act / Assert
    expect(togglePersonaId(['a', 'b'], 'a')).toEqual(['b'])
  })

  it('does not mutate the input', () => {
    // Arrange
    const ids = ['a']

    // Act
    togglePersonaId(ids, 'b')

    // Assert
    expect(ids).toEqual(['a'])
  })
})

describe('findTaskInDraft', () => {
  it('finds a task in a later goal and step', () => {
    // Arrange
    const details = map([
      goal('g1', 0, [step('s1', 'g1', 0, [task('t1', 's1', 'lane-default', 0)])]),
      goal('g2', 1, [step('s2', 'g2', 0, [task('t2', 's2', 'lane-default', 0)])]),
    ])

    // Act
    const found = findTaskInDraft(details, 't2')

    // Assert
    expect(found?.id).toBe('t2')
  })

  it('returns undefined when the task is not on the map', () => {
    // Arrange
    const details = map([goal('g1', 0, [step('s1', 'g1', 0)])])

    // Act
    const found = findTaskInDraft(details, 'missing')

    // Assert
    expect(found).toBeUndefined()
  })
})

describe('recountChecklist', () => {
  it('recomputes both counts from the items', () => {
    // Arrange
    const subject = task('t1', 's1', 'lane-default', 0)
    subject.checklist = [
      { id: 'i1', name: 'one', isChecked: true, order: 0 },
      { id: 'i2', name: 'two', isChecked: false, order: 1 },
      { id: 'i3', name: 'three', isChecked: true, order: 2 },
    ]

    // Act
    recountChecklist(subject)

    // Assert
    expect(subject.checklistTotalCount).toBe(3)
    expect(subject.checklistCompletedCount).toBe(2)
  })

  it('zeroes both counts for an empty checklist', () => {
    // Arrange
    const subject = task('t1', 's1', 'lane-default', 0)
    subject.checklist = []
    subject.checklistTotalCount = 4
    subject.checklistCompletedCount = 2

    // Act
    recountChecklist(subject)

    // Assert
    expect(subject.checklistTotalCount).toBe(0)
    expect(subject.checklistCompletedCount).toBe(0)
  })
})
