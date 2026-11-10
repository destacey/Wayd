import type { ProjectPlanNodeDto } from '@/src/services/wayd-api'
import { parseCalendarDate } from '@/src/utils'
import {
  computeProjectPlanGanttDomain,
  isMilestoneNode,
  isStageNode,
} from './project-plan-gantt'

const DAY = 86_400_000
const PAD = 14 * DAY
const at = (iso: string) => parseCalendarDate(iso).valueOf()

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
            start: '2026-03-01',
            end: '2026-03-10',
          }),
          node({
            id: 'task-2',
            start: '2026-03-05',
            end: '2026-03-20',
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
        plannedDate: '2026-06-15',
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
      node({ id: 'task-1', start: '2026-03-01', end: undefined }),
      node({
        id: 'task-2',
        start: '2026-04-01',
        end: '2026-04-10',
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
        start: '2026-03-01',
        end: '2026-03-10',
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
