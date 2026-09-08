import { QueryTags } from '../query-tags'
import { projectActivityTag } from './project-activity-tags'

/**
 * Pins the cache entry a change to a project has to refresh.
 *
 * The activity log is a separate query from the project itself, so a mutation that refreshes the
 * record without this tag leaves the Activity section showing the history from before the change.
 * Nothing errors — it only shows up by driving the app.
 */
const PROJECT_ID = '019f0969-5692-767a-ae3e-d03f623971ee'
const PROJECT_KEY = 'REALTIME'

describe('project activity cache tags', () => {
  it('refreshes the activity log for the project that changed', () => {
    // Arrange / Act
    const tag = projectActivityTag(PROJECT_ID)

    // Assert
    expect(tag).toEqual({ type: QueryTags.ActivityLog, id: PROJECT_ID })
  })

  it('keys the tag by id, not by key', () => {
    // Arrange / Act
    const tag = projectActivityTag(PROJECT_ID)

    // Assert — getProjectActivities is called with the project's id, so a tag built from its key
    // matches no cache entry and fails silently.
    expect(tag.id).not.toBe(PROJECT_KEY)
  })
})
