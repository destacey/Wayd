import { render, screen } from '@testing-library/react'
import { IntakeRequestTagCategoryDto } from '@/src/services/wayd-api'
import TagAxisFacts from './tag-axis-facts'

const axis = (
  overrides: Partial<IntakeRequestTagCategoryDto> = {},
): IntakeRequestTagCategoryDto => ({
  id: 'axis-1',
  name: 'Area',
  description: 'The part of the business a request touches',
  allowsMany: true,
  order: 1,
  isActive: true,
  isSystem: false,
  tags: [],
  ...overrides,
})

describe('TagAxisFacts', () => {
  it('shows whether the axis accepts several tags', () => {
    // Arrange / Act — fixed once set, so it is here to read rather than on the
    // edit form to change.
    render(<TagAxisFacts axis={axis({ allowsMany: false, isActive: true })} />)

    // Assert
    const allowsMany = screen.getByText('Allows Many')
    expect(allowsMany.parentElement).toHaveTextContent('No')
  })

  it('says a platform-defined axis is read-only', () => {
    // Arrange / Act — otherwise the missing Edit action looks like a bug.
    render(<TagAxisFacts axis={axis({ isSystem: true })} />)

    // Assert
    expect(screen.getByText(/read-only/)).toBeInTheDocument()
  })

  it('says nothing about system on an axis the organization added itself', () => {
    // Arrange / Act
    render(<TagAxisFacts axis={axis()} />)

    // Assert
    expect(screen.queryByText('System')).not.toBeInTheDocument()
  })
})
