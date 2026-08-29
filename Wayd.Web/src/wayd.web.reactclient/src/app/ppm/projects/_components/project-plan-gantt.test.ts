import type { ProjectPlanNodeDto } from '@/src/services/wayd-api'
import {
  applyOptimisticPlanDates,
  computeProjectPlanGanttDomain,
  isMilestoneNode,
  isStageNode,
} from './project-plan-gantt'

const DAY = 86_400_000
const PAD = 14 * DAY
const at = (iso: string) => Date.parse(iso)

const node = (over: Partial<ProjectPlanNodeDto>): ProjectPlanNodeDto =>
  ({
    id: 'id',
    nodeType: 'Task',
    name: 'Node',
    status: { id: 1, name: 'Not Started' },
    order: 1,
    wbs: '1',
    progress: 0,
    assignees: [],
    children: [],
    ...over,
  }) as ProjectPlanNodeDto

describe('isStageNode', () => {
  it('identifies a stage by its node type', () => {
    // Arrange / Act / Assert
    expect(isStageNode(node({ nodeType: 'Stage' }))).toBe(true)
    expect(isStageNode(node({ nodeType: 'Task' }))).toBe(false)
  })
})

describe('isMilestoneNode', () => {
  it('identifies a milestone by its task type, not its node type', () => {
    // Arrange — a milestone is a Task whose type is "Milestone".
    const milestone = node({
      nodeType: 'Task',
      type: { id: 2, name: 'Milestone' },
    })
    // Act / Assert
    expect(isMilestoneNode(milestone)).toBe(true)
  })

  it('does not treat a regular task as a milestone', () => {
    // Arrange
    const task = node({ nodeType: 'Task', type: { id: 1, name: 'Task' } })
    // Act / Assert
    expect(isMilestoneNode(task)).toBe(false)
  })

  it('does not treat an untyped node as a milestone', () => {
    // Arrange — stages carry no task type at all.
    const stage = node({ nodeType: 'Stage', type: undefined })
    // Act / Assert
    expect(isMilestoneNode(stage)).toBe(false)
  })
})

describe('computeProjectPlanGanttDomain', () => {
  it('spans stages and their nested tasks', () => {
    // Arrange — an undated stage whose tasks run Mar 1 → Mar 20.
    const tree = [
      node({
        id: 'stage-1',
        nodeType: 'Stage',
        children: [
          node({
            id: 'task-1',
            start: new Date(at('2026-03-01')),
            end: new Date(at('2026-03-10')),
          }),
          node({
            id: 'task-2',
            start: new Date(at('2026-03-05')),
            end: new Date(at('2026-03-20')),
          }),
        ],
      }),
    ]
    // Act
    const domain = computeProjectPlanGanttDomain(tree)
    // Assert
    expect(domain.domainStart).toBe(at('2026-03-01') - PAD)
    expect(domain.domainEnd).toBe(at('2026-03-20') + PAD)
  })

  it('uses plannedDate for a milestone rather than start/end', () => {
    // Arrange — a milestone carries only plannedDate.
    const tree = [
      node({
        id: 'ms-1',
        type: { id: 2, name: 'Milestone' },
        plannedDate: new Date(at('2026-06-15')),
      }),
    ]
    // Act
    const domain = computeProjectPlanGanttDomain(tree)
    // Assert — the milestone anchors both ends of the domain.
    expect(domain.domainStart).toBe(at('2026-06-15') - PAD)
    expect(domain.domainEnd).toBe(at('2026-06-15') + PAD)
  })

  it('ignores a task that is missing one endpoint', () => {
    // Arrange — a half-dated task must not drag the axis to epoch 0.
    const tree = [
      node({ id: 'task-1', start: new Date(at('2026-03-01')), end: undefined }),
      node({
        id: 'task-2',
        start: new Date(at('2026-04-01')),
        end: new Date(at('2026-04-10')),
      }),
    ]
    // Act
    const domain = computeProjectPlanGanttDomain(tree)
    // Assert — only the fully dated task contributes.
    expect(domain.domainStart).toBe(at('2026-04-01') - PAD)
    expect(domain.domainEnd).toBe(at('2026-04-10') + PAD)
  })

  it('widens to the project window when it is supplied', () => {
    // Arrange — one short task inside a longer project window.
    const tree = [
      node({
        id: 'task-1',
        start: new Date(at('2026-03-01')),
        end: new Date(at('2026-03-10')),
      }),
    ]
    // Act
    const domain = computeProjectPlanGanttDomain(
      tree,
      '2026-01-01',
      '2026-12-31',
    )
    // Assert
    expect(domain.domainStart).toBe(at('2026-01-01') - PAD)
    expect(domain.domainEnd).toBe(at('2026-12-31') + PAD)
  })
})

describe('applyOptimisticPlanDates', () => {
  it('writes a dragged range onto the matching task', () => {
    // Arrange
    const tree = [node({ id: 't1', start: undefined, end: undefined })]
    // Act
    const found = applyOptimisticPlanDates(
      tree,
      't1',
      false,
      '2026-03-01',
      '2026-03-10',
    )
    // Assert — stored as YYYY-MM-DD to match the post-refetch shape.
    expect(found).toBe(true)
    expect(tree[0].start).toBe('2026-03-01')
    expect(tree[0].end).toBe('2026-03-10')
  })

  it('writes only plannedDate for a milestone', () => {
    // Arrange
    const tree = [
      node({ id: 'ms1', type: { id: 2, name: 'Milestone' } }),
    ]
    // Act
    const found = applyOptimisticPlanDates(
      tree,
      'ms1',
      true,
      '2026-06-15',
      '2026-06-15',
    )
    // Assert — a milestone has no range to write.
    expect(found).toBe(true)
    expect(tree[0].plannedDate).toBe('2026-06-15')
    expect(tree[0].start).toBeUndefined()
    expect(tree[0].end).toBeUndefined()
  })

  it('finds a task nested under a stage', () => {
    // Arrange — the dragged bar is a child, not a root node.
    const tree = [
      node({
        id: 'stage-1',
        nodeType: 'Stage',
        children: [node({ id: 'deep' })],
      }),
    ]
    // Act
    const found = applyOptimisticPlanDates(
      tree,
      'deep',
      false,
      '2026-04-01',
      '2026-04-05',
    )
    // Assert
    expect(found).toBe(true)
    expect(tree[0].children[0].start).toBe('2026-04-01')
  })

  it('reports when the id is not in the tree', () => {
    // Arrange
    const tree = [node({ id: 't1' })]
    // Act
    const found = applyOptimisticPlanDates(
      tree,
      'missing',
      false,
      '2026-04-01',
      '2026-04-05',
    )
    // Assert — the caller must not assume a patch was applied.
    expect(found).toBe(false)
  })

  it('leaves other nodes untouched', () => {
    // Arrange
    const tree = [
      node({ id: 't1' }),
      node({ id: 't2', start: new Date(at('2026-01-01')) }),
    ]
    // Act
    applyOptimisticPlanDates(tree, 't1', false, '2026-04-01', '2026-04-05')
    // Assert — only the dragged bar moves.
    expect(tree[1].start).toEqual(new Date(at('2026-01-01')))
  })

  it('tolerates an undefined tree', () => {
    // Arrange / Act / Assert — the cache may be empty when a drag commits.
    expect(
      applyOptimisticPlanDates(undefined, 't1', false, '2026-04-01', '2026-04-05'),
    ).toBe(false)
  })
})
