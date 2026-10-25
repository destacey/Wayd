import React from 'react'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import SubmitIntakeRequestPage from './page'

// jest.setup stubs dayjs down to formatting; the date pickers need the real module.
jest.unmock('dayjs')

const mockPush = jest.fn()
jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush, back: jest.fn() }),
}))

// The markdown editor pulls in ESM-only packages Jest cannot parse.
jest.mock('@/src/components/common/markdown', () => ({
  MarkdownRenderer: ({ markdown }: { markdown: string }) => (
    <div>{markdown}</div>
  ),
  MarkdownEditor: ({
    value,
    onChange,
  }: {
    value?: string
    onChange?: (v: string) => void
  }) => (
    <textarea
      aria-label="markdown"
      value={value ?? ''}
      onChange={(e) => onChange?.(e.target.value)}
    />
  ),
}))

jest.mock('@/src/components/common', () => ({
  PageTitle: ({ title }: { title: string }) => <h1>{title}</h1>,
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

let mockEmployeeId: string | null = 'e-1'
jest.mock('@/src/hooks', () => ({
  useDocumentTitle: jest.fn(),
  useLinkedEmployee: () => ({
    employeeId: mockEmployeeId,
    hasLinkedEmployee: mockEmployeeId !== null,
  }),
}))

jest.mock('@/src/components/hoc', () => ({
  authorizePage: (Component: React.ComponentType<any>) => Component,
  requireFeatureFlag: (Component: React.ComponentType<any>) => Component,
}))

const desk = (id: string, name: string, description: string) => ({
  id,
  key: 1,
  name,
  description,
  visibility: 'Open',
  isRequesterSelectable: true,
  isOpen: true,
  requesterMayAddParticipants: true,
  reviewers: [{ id: 'e-9', key: 9, name: 'Riley Reviewer' }],
})

let mockDesks: any[] = []
const mockCreate = jest.fn()

jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeDesksQuery: () => ({ data: mockDesks, isLoading: false }),
    useGetIntakeChannelsQuery: () => ({
      data: [{ id: 'c-1', name: 'Security', order: 1, isActive: true }],
    }),
    useGetIntakeEmployeeOptionsQuery: () => ({
      data: [
        { id: 'e-1', key: 1, name: 'Avery Example' },
        { id: 'e-2', key: 2, name: 'Blake Sample' },
      ],
    }),
    useGetNeedByBasesQuery: () => ({ data: [] }),
    useGetNeedByBasisFamiliesQuery: () => ({ data: [] }),
    useCreateIntakeRequestMutation: () => [mockCreate],
  }),
)

const renderPage = async () =>
  await act(async () => render(<SubmitIntakeRequestPage />))

describe('SubmitIntakeRequestPage', () => {
  beforeEach(() => {
    mockEmployeeId = 'e-1'
    mockDesks = []
    mockCreate.mockReset()
    mockPush.mockReset()
  })

  it('says submitting needs a linked employee when the account has none', async () => {
    // Arrange
    mockEmployeeId = null
    mockDesks = [desk('d-1', 'IT', 'Laptops, access and software')]

    // Act
    await renderPage()

    // Assert
    expect(
      screen.getByText(/needs your account linked to an employee record/),
    ).toBeInTheDocument()
    expect(screen.queryByLabelText('Title')).not.toBeInTheDocument()
  })

  it('says no desk is taking requests when none is open', async () => {
    // Arrange / Act
    await renderPage()

    // Assert
    expect(
      screen.getByText('No desk is taking requests right now.'),
    ).toBeInTheDocument()
  })

  it('asks no desk question when one desk is open, and says where the request goes', async () => {
    // Arrange
    mockDesks = [desk('d-1', 'IT', 'Laptops, access and software')]

    // Act
    await renderPage()

    // Assert
    expect(screen.getByText('Where this request goes')).toBeInTheDocument()
    expect(screen.getByText('Laptops, access and software')).toBeInTheDocument()
    expect(screen.queryByRole('radio')).not.toBeInTheDocument()
    expect(screen.getByLabelText('Title')).toBeInTheDocument()
  })

  it('asks for the desk first when several are open, in their own words and with their reviewers', async () => {
    // Arrange
    mockDesks = [
      desk('d-1', 'IT', 'Laptops, access and software'),
      desk('d-2', 'Product', 'Changes to what we sell'),
    ]

    // Act
    await renderPage()

    // Assert — the rest of the form waits for the answer
    expect(screen.getAllByRole('radio')).toHaveLength(2)
    expect(screen.getByText('Changes to what we sell')).toBeInTheDocument()
    expect(screen.getAllByText('IT · Reviewed by Riley Reviewer')).toHaveLength(
      1,
    )
    expect(screen.queryByLabelText('Title')).not.toBeInTheDocument()

    // Act
    await act(() =>
      userEvent.click(screen.getByText('Changes to what we sell')),
    )

    // Assert
    expect(await screen.findByLabelText('Title')).toBeInTheDocument()
  })

  it('tells the requester the title is visible to everyone', async () => {
    // Arrange
    mockDesks = [desk('d-1', 'IT', 'Laptops, access and software')]

    // Act
    await renderPage()

    // Assert
    expect(
      screen.getByText(
        /sees the title, even when the detail is kept to the desk/,
      ),
    ).toBeInTheDocument()
  })

  it('submits for the signed-in employee without naming them as requester, then opens the request', async () => {
    // Arrange
    mockDesks = [desk('d-1', 'IT', 'Laptops, access and software')]
    mockCreate.mockReturnValue({
      unwrap: () => Promise.resolve({ id: 'r-1', key: 42 }),
    })
    await renderPage()
    await userEvent.type(screen.getByLabelText('Title'), 'New laptop')
    await userEvent.type(
      screen.getAllByLabelText('markdown')[0],
      'Mine is five years old',
    )
    await act(() =>
      userEvent.click(screen.getByLabelText('Where did this come from?')),
    )
    await act(() => userEvent.click(screen.getByTitle('Security')))

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Submit Request' })),
    )

    // Assert
    expect(mockCreate).toHaveBeenCalledWith(
      expect.objectContaining({
        deskId: 'd-1',
        requesterId: undefined,
        title: 'New laptop',
        description: 'Mine is five years old',
        channelId: 'c-1',
        needBy: undefined,
      }),
    )
    expect(mockPush).toHaveBeenCalledWith('/demand-management/requests/42')
  })

  it('sends the chosen desk and the person it is for when submitting on their behalf', async () => {
    // Arrange
    mockDesks = [
      desk('d-1', 'IT', 'Laptops, access and software'),
      desk('d-2', 'Product', 'Changes to what we sell'),
    ]
    mockCreate.mockReturnValue({
      unwrap: () => Promise.resolve({ id: 'r-2', key: 43 }),
    })
    await renderPage()
    await act(() =>
      userEvent.click(screen.getByText('Changes to what we sell')),
    )
    await userEvent.type(await screen.findByLabelText('Title'), 'Bulk export')
    await userEvent.type(screen.getAllByLabelText('markdown')[0], 'For finance')
    await act(() =>
      userEvent.click(screen.getByLabelText('Where did this come from?')),
    )
    await act(() => userEvent.click(screen.getByTitle('Security')))
    const requester = screen
      .getByText('Who is this request for?')
      .closest('.ant-form-item')!
      .querySelector('.ant-select')!
    await act(() => userEvent.click(requester.querySelector('input')!))
    await act(() => userEvent.click(screen.getByTitle('Blake Sample')))

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Submit Request' })),
    )

    // Assert
    expect(mockCreate).toHaveBeenCalledWith(
      expect.objectContaining({ deskId: 'd-2', requesterId: 'e-2' }),
    )
  })
})
