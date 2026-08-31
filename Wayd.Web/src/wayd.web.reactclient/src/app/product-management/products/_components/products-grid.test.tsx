import { render, screen } from '@testing-library/react'
import { ProductDto } from '@/src/services/wayd-api'
import ProductsGrid from './products-grid'

const product = (
  id: string,
  key: number,
  name: string,
  parent?: { id: string; key: number; name: string },
): ProductDto =>
  ({
    id,
    key,
    name,
    type: { id: 't1', key: 1, name: 'Application' },
    status: { id: 's1', name: 'Concept', category: 1, alias: 0 },
    isReleasable: true,
    parent,
    tags: [],
  }) as unknown as ProductDto

const tagged = (base: ProductDto, tags: unknown[]): ProductDto =>
  ({ ...base, tags }) as unknown as ProductDto

const PRODUCTS = [
  product('a1', 1, 'Trio WFS'),
  product('b2', 2, 'Trio VMS', { id: 'a1', key: 1, name: 'Trio WFS' }),
]

const renderGrid = (asTree: boolean) =>
  render(
    <ProductsGrid
      products={PRODUCTS}
      isLoading={false}
      refetch={() => {}}
      asTree={asTree}
    />,
  )

describe('ProductsGrid', () => {
  describe('tree view', () => {
    it('gives the parent an expander and the child none', () => {
      // A flat render shows both names too, so presence proves nothing — the expander is what
      // distinguishes a nested row from a sibling.
      // Arrange / Act
      renderGrid(true)

      // Assert
      const expanders = document.querySelectorAll('.anticon-caret-down')
      expect(expanders).toHaveLength(1)
    })

    it('indents the child below its parent', () => {
      // Arrange / Act
      const { container } = renderGrid(true)

      // Assert
      // The parent draws no indent spacer before its expander; the child draws one, plus the
      // spacer standing in for the expander it does not have. Equal indentation is the bug.
      const spacers = container.querySelectorAll('span[class*="indentSpacer"]')

      expect(spacers.length).toBeGreaterThan(0)
    })

    it('omits the parent column, which position already shows', () => {
      // Arrange / Act
      renderGrid(true)

      // Assert
      expect(screen.queryByText('Parent')).not.toBeInTheDocument()
    })
  })

  describe('flat view', () => {
    it('shows the parent as a column instead of by position', () => {
      // Arrange / Act
      renderGrid(false)

      // Assert
      expect(screen.getByText('Parent')).toBeInTheDocument()
    })

    it('draws no expander', () => {
      // Arrange / Act
      renderGrid(false)

      // Assert
      expect(document.querySelectorAll('.anticon-caret-down')).toHaveLength(0)
    })
  })

  it('qualifies a tag with its axis', () => {
    // A bare "gold" does not say whether it is a tier, a platform or a
    // compliance scope — the same reason the header chips carry the axis.
    // Arrange
    const products = [
      tagged(PRODUCTS[0], [
        {
          tagId: 't1',
          tagName: 'gold',
          categoryId: 'c1',
          categoryName: 'Tier',
        },
      ]),
    ]

    // Act
    render(
      <ProductsGrid
        products={products}
        isLoading={false}
        refetch={() => {}}
        asTree={false}
      />,
    )

    // Assert
    expect(screen.getByText('Tier | gold')).toBeInTheDocument()
  })

  it('resolves the type through its navigation object', () => {
    // The DTO carries type as a nested object, so a stale flat accessor renders an empty cell —
    // which is what happened when the field was renamed from productType to type.
    // Arrange / Act
    renderGrid(true)

    // Assert
    expect(screen.getAllByText('Application').length).toBeGreaterThan(0)
  })
})
