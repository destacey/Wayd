import { buildVersionColumns } from './versions-grid'

const columnIds = (showProduct: boolean) =>
  buildVersionColumns(showProduct).map((column) => column.id)

describe('buildVersionColumns', () => {
  it('puts the product before the version', () => {
    // A version only means something once you know what it is a version of: 4.8.2 and 2026.04 say
    // nothing side by side without their products.
    // Arrange / Act
    const ids = columnIds(true)

    // Assert
    expect(ids.indexOf('product')).toBeLessThan(ids.indexOf('version'))
  })

  it('omits the product where every row shares one', () => {
    // On a product's own page the column would repeat the same value down every row.
    // Arrange / Act
    const ids = columnIds(false)

    // Assert
    expect(ids).not.toContain('product')
    expect(ids).toContain('version')
  })

  it('keeps the key first so a row reads left to right', () => {
    // Arrange / Act / Assert
    expect(columnIds(true)[0]).toBe('key')
  })
})
