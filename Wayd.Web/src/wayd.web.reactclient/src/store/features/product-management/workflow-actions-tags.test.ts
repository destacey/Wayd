import { QueryTags } from '../query-tags'
import { correctedVersionDatesTags } from './versions-api'

/**
 * Pins what a version date correction invalidates.
 *
 * Whether a version's workflow allows reverting it depends on whether it was cut, so correcting the
 * cut moment changes the answer without moving the status. A correction that missed the workflow
 * actions would leave the page offering a revert the server refuses, or hiding one it allows, until
 * a reload.
 */
const ID = '11111111-1111-1111-1111-111111111111'
const KEY = 8

const idsOf = (type: QueryTags) =>
  correctedVersionDatesTags(ID, KEY)
    .filter((tag) => tag.type === type)
    .map((tag) => String(tag.id))

describe('version date-correction cache tags', () => {
  it('invalidates the workflow actions by id and by key', () => {
    // Arrange / Act
    const actions = idsOf(QueryTags.WorkflowActions)

    // Assert — the detail page queries the actions by the key in its URL.
    expect(actions).toEqual([ID, String(KEY)])
  })

  it('leaves the status history alone, which a correction does not change', () => {
    // Arrange / Act
    const history = idsOf(QueryTags.StatusHistory)

    // Assert
    expect(history).toEqual([])
  })
})
