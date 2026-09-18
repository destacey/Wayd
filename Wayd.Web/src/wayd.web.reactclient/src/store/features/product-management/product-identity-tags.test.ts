import { QueryTags } from '../query-tags'
import { productIdentityTags } from './products-api'

const ID = '11111111-1111-1111-1111-111111111111'

describe('productIdentityTags', () => {
  it('invalidates every dependency list, which prints the name and path of both ends', () => {
    // Act
    const tags = productIdentityTags(ID)

    // Assert — a renamed or moved product would otherwise keep its old name and boxes on every map
    // and grid that names it, and a move would keep rolling links up onto its old parent.
    expect(tags).toContainEqual({
      type: QueryTags.ProductDependency,
      id: 'LIST',
    })
  })

  it('still invalidates the product and the product lists', () => {
    // Act
    const tags = productIdentityTags(ID)

    // Assert
    expect(tags).toContainEqual({ type: QueryTags.Product, id: 'LIST' })
    expect(tags).toContainEqual({ type: QueryTags.Product, id: ID })
  })
})
