import { render, screen } from '@testing-library/react'
import { IntakeRequestTagCategoryDto } from '@/src/services/wayd-api'
import TagCategoryFacts from './tag-category-facts'

const category = (
  overrides: Partial<IntakeRequestTagCategoryDto> = {},
): IntakeRequestTagCategoryDto => ({
  id: 'category-1',
  name: 'Area',
  description: 'The part of the business a request touches',
  allowsMany: true,
  order: 1,
  isActive: true,
  isSystem: false,
  tags: [],
  ...overrides,
})

describe('TagCategoryFacts', () => {
  it('shows whether the category accepts several tags', () => {
    // Arrange / Act — fixed once set, so it is here to read rather than on the
    // edit form to change.
    render(
      <TagCategoryFacts
        category={category({ allowsMany: false, isActive: true })}
      />,
    )

    // Assert
    const allowsMany = screen.getByText('Allows Many')
    expect(allowsMany.parentElement).toHaveTextContent('No')
  })

  it('says a platform-defined category is read-only', () => {
    // Arrange / Act — otherwise the missing Edit action looks like a bug.
    render(<TagCategoryFacts category={category({ isSystem: true })} />)

    // Assert
    expect(screen.getByText(/read-only/)).toBeInTheDocument()
  })

  it('says nothing about system on a category the organization added itself', () => {
    // Arrange / Act
    render(<TagCategoryFacts category={category()} />)

    // Assert
    expect(screen.queryByText('System')).not.toBeInTheDocument()
  })
})
