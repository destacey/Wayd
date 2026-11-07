import { ProjectPlanNodeDto } from '@/src/services/wayd-api'
import { linkedTasks, toTreeOptions } from './set-deliverable-tasks-form'

const node = (
  id: string,
  nodeType: 'Stage' | 'Task',
  deliverableIds: string[] = [],
  children: ProjectPlanNodeDto[] = [],
): ProjectPlanNodeDto =>
  ({
    id,
    nodeType,
    name: id,
    wbs: '1',
    deliverableIds,
    children,
  }) as unknown as ProjectPlanNodeDto

describe('toTreeOptions', () => {
  it('lets tasks be picked but not stages, which serve no deliverable', () => {
    // Arrange
    const plan = [node('build', 'Stage', [], [node('api', 'Task')])]

    // Act
    const [stage] = toTreeOptions(plan)

    // Assert
    expect(stage.selectable).toBe(false)
    expect(stage.children[0].selectable).toBe(true)
  })
})

describe('linkedTasks', () => {
  it('finds the linked tasks at every depth, in plan order', () => {
    // Arrange
    const plan = [
      node(
        'build',
        'Stage',
        [],
        [
          node('api', 'Task', ['portal'], [node('auth', 'Task', ['portal'])]),
          node('docs', 'Task', ['reports']),
        ],
      ),
    ]

    // Act
    const ids = linkedTasks(plan, 'portal').map((t) => t.id)

    // Assert
    expect(ids).toEqual(['api', 'auth'])
  })
})
