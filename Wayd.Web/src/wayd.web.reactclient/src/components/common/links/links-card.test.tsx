import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import LinksCard from './links-card'

const mockGetLinks = vi.fn()
const mockDeleteLink = vi.fn()
const mockMessage = { success: vi.fn(), error: vi.fn() }

vi.mock('@/src/store/features/common/links-api', () => ({
  useGetLinksQuery: (...args: unknown[]) => mockGetLinks(...args),
  useDeleteLinkMutation: () => [mockDeleteLink],
}))

vi.mock('./link-form', () => ({
  default: ({ link }: { link?: { name: string } }) => (
    <div data-testid="link-form">{link ? `edit ${link.name}` : 'create'}</div>
  ),
}))

vi.mock('../../contexts/messaging', () => ({
  useMessage: () => mockMessage,
}))

const owner = { ownerType: 'ppm.project' as const, ownerId: 'project-1' }

const link = (id: string, name: string) => ({
  id,
  ownerType: owner.ownerType,
  ownerId: owner.ownerId,
  name,
  url: `https://example.com/${id}`,
})

const answer = (
  canManage: boolean,
  links = [link('b', 'Beta'), link('a', 'alpha'), link('c', 'Charlie')],
) =>
  mockGetLinks.mockReturnValue({
    currentData: { canManage, links },
    isFetching: false,
    error: undefined,
  })

const turnOnEditMode = () =>
  userEvent.click(screen.getByRole('button', { name: 'Turn on edit mode' }))

describe('LinksCard', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockDeleteLink.mockResolvedValue({ data: null })
  })

  it("asks for the record's links by owner type and id", () => {
    answer(false)

    render(<LinksCard {...owner} />)

    expect(mockGetLinks).toHaveBeenCalledWith(owner, { skip: false })
  })

  it('lists the links by name, whatever their case, opening each address', () => {
    answer(false)

    render(<LinksCard {...owner} />)

    // A case-sensitive sort would put the capitals first: Beta, Charlie, alpha.
    const links = screen.getAllByRole('link')
    expect(links.map((l) => l.textContent)).toEqual([
      'alpha',
      'Beta',
      'Charlie',
    ])
    expect(links[0]).toHaveAttribute('href', 'https://example.com/a')
  })

  it('offers nothing to change when the server says the viewer cannot manage the links', () => {
    answer(false)

    render(<LinksCard {...owner} />)

    expect(
      screen.queryByRole('button', { name: 'Add new link' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /edit mode/ }),
    ).not.toBeInTheDocument()
  })

  it('lets a viewer who can manage the links add one', async () => {
    answer(true)

    render(<LinksCard {...owner} />)
    await userEvent.click(screen.getByRole('button', { name: 'Add new link' }))

    expect(screen.getByTestId('link-form')).toHaveTextContent('create')
  })

  it('shows edit and delete for each link only in edit mode', async () => {
    answer(true)

    render(<LinksCard {...owner} />)
    expect(
      screen.queryByRole('button', { name: 'Edit alpha' }),
    ).not.toBeInTheDocument()

    await turnOnEditMode()
    await userEvent.click(screen.getByRole('button', { name: 'Edit alpha' }))

    expect(screen.getByTestId('link-form')).toHaveTextContent('edit alpha')
    expect(
      screen.getByRole('button', { name: 'Delete Beta' }),
    ).toBeInTheDocument()
  })

  it('deletes a link from the record it is on, and says so', async () => {
    answer(true, [link('a', 'alpha')])

    render(<LinksCard {...owner} />)
    await turnOnEditMode()
    await userEvent.click(screen.getByRole('button', { name: 'Delete alpha' }))
    await userEvent.click(screen.getByRole('button', { name: 'Yes' }))

    expect(mockDeleteLink).toHaveBeenCalledWith({ ...owner, linkId: 'a' })
    expect(mockMessage.success).toHaveBeenCalledWith(
      'Successfully deleted Link.',
    )
  })

  it("shows the server's reason when a delete is refused", async () => {
    answer(true, [link('a', 'alpha')])
    mockDeleteLink.mockResolvedValue({
      error: { status: 403, detail: "You cannot change this record's links." },
    })

    render(<LinksCard {...owner} />)
    await turnOnEditMode()
    await userEvent.click(screen.getByRole('button', { name: 'Delete alpha' }))
    await userEvent.click(screen.getByRole('button', { name: 'Yes' }))

    expect(mockMessage.error).toHaveBeenCalledWith(
      "You cannot change this record's links.",
    )
    expect(mockMessage.success).not.toHaveBeenCalled()
  })

  it("shows a spinner, and none of another record's links, while the links load", () => {
    mockGetLinks.mockReturnValue({
      currentData: undefined,
      isFetching: true,
      error: undefined,
    })

    const { container } = render(<LinksCard {...owner} />)

    expect(container.querySelector('.ant-spin')).toBeInTheDocument()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Add new link' }),
    ).not.toBeInTheDocument()
  })

  it('says so when the links cannot be loaded', () => {
    mockGetLinks.mockReturnValue({
      currentData: undefined,
      isFetching: false,
      error: { status: 500 },
    })

    render(<LinksCard {...owner} />)

    expect(screen.getByText('Failed to load links')).toBeInTheDocument()
  })

  it('leaves edit mode when the viewer can no longer manage the links', async () => {
    answer(true)
    const { rerender } = render(<LinksCard {...owner} />)
    await turnOnEditMode()

    answer(false)
    rerender(<LinksCard {...owner} />)

    expect(
      screen.queryByRole('button', { name: 'Edit alpha' }),
    ).not.toBeInTheDocument()
    expect(screen.getAllByRole('link')).toHaveLength(3)
  })

  it('is not already in edit mode for a link added after the last one was removed', async () => {
    answer(true, [link('a', 'alpha')])
    const { rerender } = render(<LinksCard {...owner} />)
    await turnOnEditMode()

    answer(true, [])
    rerender(<LinksCard {...owner} />)
    answer(true, [link('b', 'Beta')])
    rerender(<LinksCard {...owner} />)

    expect(
      screen.getByRole('button', { name: 'Turn on edit mode' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Edit Beta' }),
    ).not.toBeInTheDocument()
  })

  it('offers no edit mode when there is nothing to edit', () => {
    answer(true, [])

    render(<LinksCard {...owner} />)

    expect(screen.getByText('No links found')).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Add new link' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /edit mode/ }),
    ).not.toBeInTheDocument()
  })
})
