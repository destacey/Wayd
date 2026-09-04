import { QueryTags } from '../query-tags'
import { projectTaskMutationTags } from './project-tasks-api'

/**
 * Pins the cache entries a task mutation refreshes.
 *
 * The plan summaries are separate queries from the plan tree, so editing a
 * task's dates or status has to invalidate them explicitly. Drop one of these
 * and nothing errors — the grid updates while the Schedule counts beside it
 * keep serving stale numbers, which only shows up by driving the app.
 */
const PROJECT_KEY = 'NETMIGRATION'

const tagsFor = (key: string) =>
  projectTaskMutationTags(key).map((tag) => ({
    type: tag.type,
    id: 'id' in tag ? tag.id : undefined,
  }))

describe('project task mutation cache tags', () => {
  it('refreshes the plan tree that feeds the grid', () => {
    // Arrange / Act
    const tags = tagsFor(PROJECT_KEY)

    // Assert
    expect(tags).toContainEqual({
      type: QueryTags.ProjectTaskTree,
      id: `TREE-${PROJECT_KEY}`,
    })
  })

  it('refreshes this project plan summary', () => {
    // Arrange / Act
    const tags = tagsFor(PROJECT_KEY)

    // Assert — the Schedule counts on the project page.
    expect(tags).toContainEqual({
      type: QueryTags.ProjectPlanTree,
      id: PROJECT_KEY,
    })
  })

  it('refreshes the multi-project summaries keyed by other ids', () => {
    // Arrange / Act
    const tags = tagsFor(PROJECT_KEY)

    // Assert — those publish a tag per project id, not for the edited one, so
    // only the untyped entry reaches them.
    expect(tags).toContainEqual({
      type: QueryTags.ProjectPlanTree,
      id: undefined,
    })
  })

  it('refreshes the dashboard task metric cards', () => {
    // Arrange / Act
    const tags = tagsFor(PROJECT_KEY)

    // Assert
    expect(tags).toContainEqual({
      type: QueryTags.Project,
      id: 'MY_TASK_METRICS',
    })
  })
})
