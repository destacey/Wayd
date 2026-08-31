import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
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
      renderGrid(true)

      // Assert
      // Counts only the spacers drawn *before* the expander slot, which are the depth-driven ones. A
      // plain total cannot tell them from the single spacer that stands in for a missing expander, so
      // it stays green even when every row is rendered at the same depth — the exact bug this guards.
      const depthSpacersIn = (name: string) => {
        const row = screen.getByText(name).closest('tr')
        expect(row).not.toBeNull()

        const marks = row!.querySelectorAll(
          'span[class*="indentSpacer"], button[class*="expanderBtn"]',
        )

        let depth = 0
        for (const mark of marks) {
          if (mark.tagName === 'BUTTON') break
          depth++
        }

        // A leaf has no expander button, so its trailing stand-in spacer is counted above and has to
        // come back off.
        const hasExpander = row!.querySelector('button[class*="expanderBtn"]') !== null
        return hasExpander ? depth : depth - 1
      }

      expect(depthSpacersIn('Trio WFS')).toBe(0)
      expect(depthSpacersIn('Trio VMS')).toBe(1)
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

  it('offers the same qualified values in the tag filter as in the cell', async () => {
    // The set panel derives its checkboxes by splitting the cell's joined value,
    // so a filter option cannot drift from what the column displays. Both come
    // from getValues, which is what makes one custom filter unnecessary.
    // Arrange
    const products = [
      tagged(PRODUCTS[0], [
        {
          tagId: 't1',
          tagName: 'ios',
          categoryId: 'c1',
          categoryName: 'Platform',
        },
      ]),
      tagged(PRODUCTS[1], [
        {
          tagId: 't2',
          tagName: 'gold',
          categoryId: 'c2',
          categoryName: 'Tier',
        },
      ]),
    ]

    render(
      <ProductsGrid
        products={products}
        isLoading={false}
        refetch={() => {}}
        asTree={false}
      />,
    )

    // Act
    const filterToggles = document.querySelectorAll(
      '[class*="filterButton"], .anticon-filter',
    )
    await userEvent.click(filterToggles[filterToggles.length - 1])

    // Assert
    // Both appear twice — once in a cell, once as a checkbox — which is the
    // point: the panel is built from the same values the cells render.
    expect(await screen.findAllByText('Platform | ios')).toHaveLength(2)
    expect(await screen.findAllByText('Tier | gold')).toHaveLength(2)
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
