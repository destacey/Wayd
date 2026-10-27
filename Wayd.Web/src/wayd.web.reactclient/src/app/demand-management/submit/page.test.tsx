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
  members: [
    { employee: { id: 'e-9', key: 9, name: 'Riley Reviewer' }, role: 'Agent' },
  ],
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

const clickButton = (name: string) =>
  act(() => userEvent.click(screen.getByRole('button', { name })))

/** Continues past the ask and the why-and-when steps, then submits from the check. */
const submitThroughSteps = async () => {
  await clickButton('Continue')
  await clickButton('Continue')
  await clickButton('Submit request')
}

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
    expect(screen.getByText('Goes to the IT desk')).toBeInTheDocument()
    expect(screen.getByText('Laptops, access and software')).toBeInTheDocument()
    expect(
      screen.queryByText('What kind of request is this?'),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Change' }),
    ).not.toBeInTheDocument()
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
    expect(
      screen.getByText('What kind of request is this?'),
    ).toBeInTheDocument()
    expect(screen.getByText('Changes to what we sell')).toBeInTheDocument()
    expect(
      screen.getByText('Reviewed by the IT desk · Riley Reviewer'),
    ).toBeInTheDocument()
    expect(screen.queryByLabelText('Title')).not.toBeInTheDocument()

    // Act
    await act(() =>
      userEvent.click(screen.getByText('Changes to what we sell')),
    )

    // Assert — the form names the desk it goes to
    expect(await screen.findByLabelText('Title')).toBeInTheDocument()
    expect(screen.getByText('Goes to the Product desk')).toBeInTheDocument()
  })

  it('goes back to the desk question when the requester changes the desk', async () => {
    // Arrange
    mockDesks = [
      desk('d-1', 'IT', 'Laptops, access and software'),
      desk('d-2', 'Product', 'Changes to what we sell'),
    ]
    await renderPage()
    await act(() =>
      userEvent.click(screen.getByText('Changes to what we sell')),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Change' })),
    )

    // Assert
    expect(
      screen.getByText('What kind of request is this?'),
    ).toBeInTheDocument()
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
    await submitThroughSteps()

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
      .getByText('Requested by')
      .closest('.ant-form-item')!
      .querySelector('.ant-select')!
    await act(() => userEvent.click(requester.querySelector('input')!))
    await act(() => userEvent.click(screen.getByTitle('Blake Sample')))

    // Act
    await submitThroughSteps()

    // Assert
    expect(mockCreate).toHaveBeenCalledWith(
      expect.objectContaining({ deskId: 'd-2', requesterId: 'e-2' }),
    )
  })

  it('keeps the requester on the ask until it is complete', async () => {
    // Arrange
    mockDesks = [desk('d-1', 'IT', 'Laptops, access and software')]
    await renderPage()

    // Act
    await clickButton('Continue')

    // Assert
    expect(screen.getByText('What are you asking for?')).toBeVisible()
    expect(
      screen.getByText('Why does it matter, and by when?'),
    ).not.toBeVisible()
    expect(
      screen.queryByRole('button', { name: 'Submit request' }),
    ).not.toBeInTheDocument()
  })

  it('shows what will be sent before it is submitted', async () => {
    // Arrange
    mockDesks = [desk('d-1', 'IT', 'Laptops, access and software')]
    await renderPage()
    await userEvent.type(screen.getByLabelText('Title'), 'New laptop')
    await userEvent.type(screen.getAllByLabelText('markdown')[0], 'Old one')
    await act(() =>
      userEvent.click(screen.getByLabelText('Where did this come from?')),
    )
    await act(() => userEvent.click(screen.getByTitle('Security')))

    // Act
    await clickButton('Continue')
    await clickButton('Continue')

    // Assert
    expect(
      screen.getByRole('heading', { name: 'Check and submit' }),
    ).toBeInTheDocument()
    expect(screen.getAllByText('New laptop').length).toBeGreaterThan(0)
    expect(screen.getByText('No date given')).toBeInTheDocument()
    expect(mockCreate).not.toHaveBeenCalled()
  })

  it('takes the requester back to the step a rejected field is on', async () => {
    // Arrange
    mockDesks = [desk('d-1', 'IT', 'Laptops, access and software')]
    mockCreate.mockReturnValue({
      unwrap: () =>
        Promise.reject({
          status: 422,
          errors: { Title: ['A request with this title is already open.'] },
        }),
    })
    await renderPage()
    await userEvent.type(screen.getByLabelText('Title'), 'New laptop')
    await userEvent.type(screen.getAllByLabelText('markdown')[0], 'Old one')
    await act(() =>
      userEvent.click(screen.getByLabelText('Where did this come from?')),
    )
    await act(() => userEvent.click(screen.getByTitle('Security')))

    // Act
    await submitThroughSteps()

    // Assert
    expect(mockCreate).toHaveBeenCalled()
    expect(screen.getByText('What are you asking for?')).toBeVisible()
  })
})
