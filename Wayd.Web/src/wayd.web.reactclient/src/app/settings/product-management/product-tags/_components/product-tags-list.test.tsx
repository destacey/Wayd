import { render, screen } from '@testing-library/react'
import {
  ProductTagCategoryDto,
  ProductTagOptionDto,
} from '@/src/services/wayd-api'
import ProductTagsList from './product-tags-list'

const tag = (
  name: string,
  overrides: Partial<ProductTagOptionDto> = {},
): ProductTagOptionDto => ({
  id: `tag-${name}`,
  name,
  description: `${name} description`,
  isActive: true,
  productCount: 0,
  ...overrides,
})

const category = (tags: ProductTagOptionDto[]): ProductTagCategoryDto => ({
  id: 'category-1',
  key: 1,
  name: 'Platform',
  description: 'What a product runs on',
  allowsMany: true,
  order: 1,
  isActive: true,
  isSystem: false,
  tags,
})

describe('ProductTagsList', () => {
  it('lists the tags alphabetically, not in the order they were added', () => {
    // Arrange — a tag carries no position of its own and the API returns them
    // unordered, so the list is what decides how they read.
    const unordered = category([tag('web'), tag('ios'), tag('Android')])

    // Act
    render(<ProductTagsList category={unordered} canManageTags />)

    // Assert — case-insensitive, so Android does not lead purely by capital
    const names = screen
      .getAllByText(/^(ios|Android|web)$/)
      .map((n) => n.textContent)
    expect(names).toEqual(['Android', 'ios', 'web'])
  })

  it('shows how many products carry each tag', () => {
    // Arrange — the number deactivating or renaming would touch, on the row
    // itself rather than only in the confirmation.
    const withCounts = category([tag('ios', { productCount: 12 })])

    // Act
    render(<ProductTagsList category={withCounts} canManageTags />)

    // Assert
    expect(screen.getByText('12')).toBeInTheDocument()
  })

  it('offers Add Tag when the viewer can manage the axis', () => {
    // Arrange / Act
    render(<ProductTagsList category={category([tag('ios')])} canManageTags />)

    // Assert
    expect(screen.getByRole('button', { name: 'Add Tag' })).toBeInTheDocument()
  })

  it('offers no way in when the viewer cannot manage the axis', () => {
    // Arrange / Act — a read-only viewer, or any viewer on a platform-seeded
    // axis: the domain refuses those, so an action would only fail.
    render(
      <ProductTagsList category={category([tag('ios')])} canManageTags={false} />,
    )

    // Assert
    expect(screen.queryByRole('button', { name: 'Add Tag' })).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Tag actions' }),
    ).not.toBeInTheDocument()
  })
})
