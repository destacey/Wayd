import { render, screen } from '@testing-library/react'
import { ProductTagCategoryDto } from '@/src/services/wayd-api'
import ProductTagCategoryFacts from './product-tag-category-facts'

const category = (
  overrides: Partial<ProductTagCategoryDto> = {},
): ProductTagCategoryDto => ({
  id: 'category-1',
  key: 4,
  name: 'Platform',
  description: 'What a product runs on',
  allowsMany: true,
  order: 1,
  isActive: true,
  isSystem: false,
  tags: [],
  ...overrides,
})

describe('ProductTagCategoryFacts', () => {
  it('shows whether the axis accepts several tags', () => {
    // Arrange / Act — fixed once set, so it is here to read rather than on the
    // edit form to change.
    render(<ProductTagCategoryFacts category={category()} />)

    // Assert
    expect(screen.getByText('Allows Many')).toBeInTheDocument()
  })

  it('says a platform-seeded axis is read-only', () => {
    // Arrange / Act — otherwise the missing Edit action looks like a bug.
    render(<ProductTagCategoryFacts category={category({ isSystem: true })} />)

    // Assert
    expect(screen.getByText(/read-only/)).toBeInTheDocument()
  })

  it('says nothing about system on an axis the organization added itself', () => {
    // Arrange / Act
    render(<ProductTagCategoryFacts category={category()} />)

    // Assert
    expect(screen.queryByText('System')).not.toBeInTheDocument()
  })
})
