import { QueryTags } from '../query-tags'
import { teamLinkChangeTags } from './products-api'

const ID = '11111111-1111-1111-1111-111111111111'

describe('teamLinkChangeTags', () => {
  it('invalidates every team link list: the product, each ancestor rolling it up, and the team', () => {
    // Act
    const tags = teamLinkChangeTags(ID)

    // Assert
    expect(tags).toContainEqual({ type: QueryTags.ProductTeamLink, id: 'LIST' })
  })

  it('invalidates the product and the product lists, which carry its own teams', () => {
    // Act
    const tags = teamLinkChangeTags(ID)

    // Assert — the catalog's Teams column and the link dialog's already-linked teams read these
    expect(tags).toContainEqual({ type: QueryTags.Product, id: 'LIST' })
    expect(tags).toContainEqual({ type: QueryTags.Product, id: ID })
  })

  it("invalidates the product's Activity, which records the change", () => {
    // Act
    const tags = teamLinkChangeTags(ID)

    // Assert
    expect(tags).toContainEqual({ type: QueryTags.ActivityLog, id: ID })
  })
})
