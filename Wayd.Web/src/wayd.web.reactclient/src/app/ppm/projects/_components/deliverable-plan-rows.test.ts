import {
  ProjectDeliverableDto,
  ProjectPlanNodeDto,
} from '@/src/services/wayd-api'
import {
  buildDeliverablePlanRows,
  NO_DELIVERABLE,
} from './deliverable-plan-rows'

const task = (
  id: string,
  deliverableIds: string[] = [],
  children: ProjectPlanNodeDto[] = [],
): ProjectPlanNodeDto =>
  ({
    id,
    nodeType: 'Task',
    name: id,
    deliverableIds,
    children,
  }) as unknown as ProjectPlanNodeDto

const stage = (children: ProjectPlanNodeDto[]): ProjectPlanNodeDto =>
  ({
    id: 'stage',
    nodeType: 'Stage',
    name: 'Build',
    deliverableIds: [],
    children,
  }) as unknown as ProjectPlanNodeDto

const deliverable = (id: string, name: string): ProjectDeliverableDto =>
  ({ id, name }) as unknown as ProjectDeliverableDto

const portal = deliverable('portal', 'Portal')
const reports = deliverable('reports', 'Reports')

const summarize = (plan: ProjectPlanNodeDto[]) =>
  buildDeliverablePlanRows(plan, [portal, reports]).map(
    (r) => `${r.deliverableName}: ${r.task.id}`,
  )

describe('buildDeliverablePlanRows', () => {
  it('lists each deliverable in its order, with unlinked work last', () => {
    // Arrange
    const plan = [
      stage([
        task('test'),
        task('build', ['reports']),
        task('design', ['portal']),
      ]),
    ]

    // Act
    const rows = summarize(plan)

    // Assert
    expect(rows).toEqual([
      'Portal: design',
      'Reports: build',
      `${NO_DELIVERABLE}: test`,
    ])
  })

  it('lists a task serving two deliverables under both', () => {
    // Arrange
    const plan = [stage([task('build', ['portal', 'reports'])])]

    // Act
    const rows = summarize(plan)

    // Assert
    expect(rows).toEqual(['Portal: build', 'Reports: build'])
  })

  it('lets a linked summary task stand for its subtasks', () => {
    // Arrange
    const plan = [
      stage([task('build', ['portal'], [task('api'), task('ui', ['portal'])])]),
    ]

    // Act
    const rows = summarize(plan)

    // Assert
    expect(rows).toEqual(['Portal: build'])
  })

  it('lists a subtask serving a deliverable its parent does not', () => {
    // Arrange
    const plan = [
      stage([task('build', ['portal'], [task('export', ['reports'])])]),
    ]

    // Act
    const rows = summarize(plan)

    // Assert
    expect(rows).toEqual(['Portal: build', 'Reports: export'])
  })

  it('looks inside an unlinked parent of linked work', () => {
    // Arrange
    const plan = [
      stage([task('build', [], [task('api', ['portal']), task('docs')])]),
    ]

    // Act
    const rows = summarize(plan)

    // Assert
    expect(rows).toEqual(['Portal: api', `${NO_DELIVERABLE}: docs`])
  })

  it('gives each row an id unique across headings', () => {
    // Arrange
    const plan = [stage([task('build', ['portal', 'reports'])])]

    // Act
    const ids = buildDeliverablePlanRows(plan, [portal, reports]).map(
      (r) => r.rowId,
    )

    // Assert
    expect(new Set(ids).size).toBe(ids.length)
  })
})
