import { fireEvent, render, screen } from '@testing-library/react'
import FreeTagsList from './free-tags-list'

// The dialogs have their own tests; here they only need to be identifiable.
jest.mock('./edit-free-tag-form', () => ({
  __esModule: true,
  default: ({ tag }: { tag: { name: string } }) => (
    <div>edit dialog for {tag.name}</div>
  ),
}))
jest.mock('./merge-free-tag-form', () => ({
  __esModule: true,
  default: ({ tag }: { tag: { name: string } }) => (
    <div>merge dialog for {tag.name}</div>
  ),
}))
jest.mock('./delete-free-tag-form', () => ({
  __esModule: true,
  default: ({ tag }: { tag: { name: string } }) => (
    <div>delete dialog for {tag.name}</div>
  ),
}))

const mockSearch = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useSearchIntakeRequestTagsQuery: (args: unknown) => mockSearch(args),
  }),
)

describe('FreeTagsList', () => {
  beforeEach(() => {
    mockSearch.mockReset().mockReturnValue({
      data: [
        {
          id: 't-followup',
          name: 'follow up',
          description: 'Revisit next quarter',
          isFree: true,
          isActive: true,
          requestCount: 7,
        },
      ],
      isLoading: false,
      refetch: jest.fn(),
    })
  })

  it('lists only free tags, with how many requests carry each', () => {
    // Act
    render(<FreeTagsList />)

    // Assert
    expect(mockSearch).toHaveBeenCalledWith({ isFree: true })
    expect(screen.getByText('follow up')).toBeInTheDocument()
    expect(screen.getByText('7')).toBeInTheDocument()
  })

  it.each([
    ['Edit', 'edit dialog for follow up'],
    ['Merge into…', 'merge dialog for follow up'],
    ['Delete', 'delete dialog for follow up'],
  ])('opens the %s dialog for the tag', async (action, dialog) => {
    // Arrange
    render(<FreeTagsList />)

    // Act
    fireEvent.click(screen.getByRole('button', { name: 'Free tag actions' }))
    fireEvent.click(await screen.findByText(action))

    // Assert — Delete is offered on a tag in use, unlike a tag on an axis
    expect(screen.getByText(dialog)).toBeInTheDocument()
  })

  it('says the free tags could not be loaded rather than showing an empty list', () => {
    // Arrange
    mockSearch.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
      refetch: jest.fn(),
    })

    // Act
    render(<FreeTagsList />)

    // Assert
    expect(
      screen.getByText(
        'Could not load the free tags. Use refresh to try again.',
      ),
    ).toBeInTheDocument()
  })
})
