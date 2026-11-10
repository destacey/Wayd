import { fireEvent, render, screen } from '@testing-library/react'
import {
  IntakeRequestTagCategoryDto,
  IntakeRequestTagOptionDto,
} from '@/src/services/wayd-api'
import TagCategoryTagsList from './tag-category-tags-list'

// The dialogs have their own tests; here they only need to be identifiable.
vi.mock('./add-tag-form', () => ({
  default: ({ categoryName }: { categoryName: string }) => (
    <div>add dialog for {categoryName}</div>
  ),
}))
vi.mock('./edit-tag-form', () => ({
  default: () => <div>edit dialog</div>,
}))
vi.mock('./delete-tag-form', () => ({
  default: () => <div>delete dialog</div>,
}))
vi.mock('./change-tag-active-form', () => ({
  default: ({ isActive }: { isActive: boolean }) => (
    <div>{isActive ? 'activate' : 'deactivate'} dialog</div>
  ),
}))

const tag = (
  name: string,
  overrides: Partial<IntakeRequestTagOptionDto> = {},
): IntakeRequestTagOptionDto => ({
  id: `tag-${name}`,
  name,
  description: `${name} description`,
  isActive: true,
  requestCount: 0,
  ...overrides,
})

const category = (
  tags: IntakeRequestTagOptionDto[],
): IntakeRequestTagCategoryDto => ({
  id: 'category-1',
  name: 'Area',
  allowsMany: true,
  order: 1,
  isActive: true,
  isSystem: false,
  tags,
})

describe('TagCategoryTagsList', () => {
  it('shows how many requests carry each tag', () => {
    // Arrange
    const withCounts = category([tag('Payments', { requestCount: 12 })])

    // Act
    render(<TagCategoryTagsList category={withCounts} canManageTags />)

    // Assert
    expect(screen.getByText('Requests')).toBeInTheDocument()
    expect(screen.getByText('12')).toBeInTheDocument()
  })

  it('opens the add form for this category', () => {
    // Arrange
    render(
      <TagCategoryTagsList
        category={category([tag('Payments')])}
        canManageTags
      />,
    )

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Add Tag' }))

    // Assert
    expect(screen.getByText('add dialog for Area')).toBeInTheDocument()
  })

  it('offers no way in when the viewer cannot manage the category', () => {
    // Arrange / Act — a viewer without the permission, or any viewer on a
    // platform-defined category.
    render(
      <TagCategoryTagsList
        category={category([tag('Payments')])}
        canManageTags={false}
      />,
    )

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Add Tag' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Tag actions' }),
    ).not.toBeInTheDocument()
  })

  it('offers Delete on a tag no request carries', async () => {
    // Arrange
    render(
      <TagCategoryTagsList
        category={category([tag('Payments')])}
        canManageTags
      />,
    )

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Tag actions' }))
    fireEvent.click(await screen.findByText('Delete'))

    // Assert
    expect(screen.getByText('delete dialog')).toBeInTheDocument()
  })

  it('does not offer Delete on a tag requests carry', async () => {
    // Arrange — the API refuses it; the row already knows the count.
    render(
      <TagCategoryTagsList
        category={category([tag('Payments', { requestCount: 3 })])}
        canManageTags
      />,
    )

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Tag actions' }))

    // Assert
    expect(await screen.findByText('Deactivate')).toBeInTheDocument()
    expect(screen.queryByText('Delete')).not.toBeInTheDocument()
  })

  it('offers Activate on an inactive tag', async () => {
    // Arrange
    render(
      <TagCategoryTagsList
        category={category([tag('Payments', { isActive: false })])}
        canManageTags
      />,
    )

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Tag actions' }))
    fireEvent.click(await screen.findByText('Activate'))

    // Assert
    expect(screen.getByText('activate dialog')).toBeInTheDocument()
  })
})
