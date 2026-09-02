import { QueryTags } from '../query-tags'
import { deploymentTags } from './deployments-api'
import { packageTags } from './release-packages-api'
import { versionTags } from './versions-api'

/**
 * Pins the identifiers a status-history cache entry is invalidated under.
 *
 * The two sides of the cache address a record differently: a detail page queries its history by the
 * short key from its URL, so that is the id the query publishes its tag under, while a mutation only
 * ever holds the record's GUID. Tag one and not the other and nothing errors — the history simply
 * never refreshes, and the staleness only shows up by driving the app.
 *
 * `cacheKey` being a required parameter is itself part of the guard: a new mutation cannot omit it
 * and quietly reintroduce the bug, because it will not compile.
 */
const historyIds = (tags: { type: unknown; id?: unknown }[]): string[] =>
  tags
    .filter((tag) => tag.type === QueryTags.StatusHistory)
    .map((tag) => String(tag.id))

const ID = '11111111-1111-1111-1111-111111111111'
const KEY = 8

describe('delivery status-history cache tags', () => {
  it.each([
    ['releases', versionTags],
    ['release packages', packageTags],
    ['deployments', deploymentTags],
  ])('invalidates a %s history by key as well as id', (_area, tagsFor) => {
    // Arrange / Act
    const invalidated = historyIds(tagsFor(ID, KEY))

    // Assert — a detail page routed by key would otherwise keep serving a stale history.
    expect(invalidated).toContain(String(KEY))
    expect(invalidated).toContain(ID)
  })

  it.each([
    ['releases', versionTags],
    ['release packages', packageTags],
    ['deployments', deploymentTags],
  ])('invalidates a %s history by id as well, for a page reached by id', (_area, tagsFor) => {
    // Arrange — the record page and the history are separate cache entries, and a caller may have
    // arrived at either by id rather than by key.
    // Act
    const invalidated = historyIds(tagsFor(ID, KEY))

    // Assert
    expect(invalidated).toEqual([ID, String(KEY)])
  })

  it('keeps the delivery metrics tag on a deployment outcome', () => {
    // Arrange — recording an outcome moves the measures too, and adding the history key must not
    // displace that.
    // Act
    const tags = deploymentTags(ID, KEY)

    // Assert
    expect(tags).toContainEqual({
      type: QueryTags.DeliveryMetrics,
      id: 'LIST',
    })
  })
})
