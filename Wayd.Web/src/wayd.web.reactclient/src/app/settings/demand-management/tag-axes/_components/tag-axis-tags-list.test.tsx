import { fireEvent, render, screen } from '@testing-library/react'
import {
  IntakeRequestTagCategoryDto,
  IntakeRequestTagOptionDto,
} from '@/src/services/wayd-api'
import TagAxisTagsList from './tag-axis-tags-list'

// The dialogs have their own tests; here they only need to be identifiable.
jest.mock('./add-tag-form', () => ({
  __esModule: true,
  default: ({ axisName }: { axisName: string }) => (
    <div>add dialog for {axisName}</div>
  ),
}))
jest.mock('./edit-tag-form', () => ({
  __esModule: true,
  default: () => <div>edit dialog</div>,
}))
jest.mock('./delete-tag-form', () => ({
  __esModule: true,
  default: () => <div>delete dialog</div>,
}))
jest.mock('./change-tag-active-form', () => ({
  __esModule: true,
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

const axis = (
  tags: IntakeRequestTagOptionDto[],
): IntakeRequestTagCategoryDto => ({
  id: 'axis-1',
  name: 'Area',
  allowsMany: true,
  order: 1,
  isActive: true,
  isSystem: false,
  tags,
})

describe('TagAxisTagsList', () => {
  it('shows how many requests carry each tag', () => {
    // Arrange
    const withCounts = axis([tag('Payments', { requestCount: 12 })])

    // Act
    render(<TagAxisTagsList axis={withCounts} canManageTags />)

    // Assert
    expect(screen.getByText('Requests')).toBeInTheDocument()
    expect(screen.getByText('12')).toBeInTheDocument()
  })

  it('opens the add form for this axis', () => {
    // Arrange
    render(<TagAxisTagsList axis={axis([tag('Payments')])} canManageTags />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Add Tag' }))

    // Assert
    expect(screen.getByText('add dialog for Area')).toBeInTheDocument()
  })

  it('offers no way in when the viewer cannot manage the axis', () => {
    // Arrange / Act — a viewer without the permission, or any viewer on a
    // platform-defined axis.
    render(
      <TagAxisTagsList axis={axis([tag('Payments')])} canManageTags={false} />,
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
    render(<TagAxisTagsList axis={axis([tag('Payments')])} canManageTags />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Tag actions' }))
    fireEvent.click(await screen.findByText('Delete'))

    // Assert
    expect(screen.getByText('delete dialog')).toBeInTheDocument()
  })

  it('does not offer Delete on a tag requests carry', async () => {
    // Arrange — the API refuses it; the row already knows the count.
    render(
      <TagAxisTagsList
        axis={axis([tag('Payments', { requestCount: 3 })])}
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
      <TagAxisTagsList
        axis={axis([tag('Payments', { isActive: false })])}
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
