import { QueryTags } from '../query-tags'
import { ppmActivityTag } from './ppm-activity-tags'

/**
 * Pins the cache entry a change to a PPM record has to refresh.
 *
 * The activity log is a separate query from the record itself, so a mutation that refreshes the
 * record without this tag leaves the Activity section showing the history from before the change.
 * Nothing errors — it only shows up by driving the app.
 */
const RECORD_ID = '019f0969-5692-767a-ae3e-d03f623971ee'
const RECORD_KEY = 'REALTIME'

describe('ppm activity cache tags', () => {
  it('refreshes the activity log for the record that changed', () => {
    // Arrange / Act
    const tag = ppmActivityTag(RECORD_ID)

    // Assert
    expect(tag).toEqual({ type: QueryTags.ActivityLog, id: RECORD_ID })
  })

  it('keys the tag by id, not by key', () => {
    // Arrange / Act
    const tag = ppmActivityTag(RECORD_ID)

    // Assert — each get*Activities query is called with the record's id, so a tag built from its key
    // matches no cache entry and fails silently.
    expect(tag.id).not.toBe(RECORD_KEY)
  })

  it('builds the same tag for every PPM record type', () => {
    // Arrange — programs and portfolios read their logs the same way projects do, so one helper keyed
    // by id serves all three; a second per-type helper would only invite one of them to drift.
    const portfolioId = '019f0969-5692-767a-ae3e-d03f62397200'

    // Act
    const projectTag = ppmActivityTag(RECORD_ID)
    const portfolioTag = ppmActivityTag(portfolioId)

    // Assert
    expect(projectTag.type).toBe(portfolioTag.type)
    expect(portfolioTag.id).toBe(portfolioId)
  })
})
