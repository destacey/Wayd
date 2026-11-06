import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import ChangeIntakeDeskWorkflowForm from './change-intake-desk-workflow-form'

const mockSuccess = jest.fn()
const mockError = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockError, success: mockSuccess }),
}))

const status = (id: string, name: string, order: number) => ({
  id,
  name,
  order,
  isRetired: false,
  category: { id: 1, name: 'Proposed' },
})

let mockPreview: any
const mockChangeWorkflow = jest.fn()
const mockRefetch = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useChangeIntakeDeskWorkflowMutation: () => [
      mockChangeWorkflow,
      { isLoading: false },
    ],
    useGetIntakeWorkflowChoicesQuery: () => ({
      data: [
        { id: 'w-1', key: 3, name: 'Default Intake Workflow' },
        { id: 'w-2', key: 4, name: 'Board Review' },
      ],
      isLoading: false,
    }),
    usePreviewIntakeDeskWorkflowChangeQuery: (
      _args: unknown,
      { skip }: { skip: boolean },
    ) => ({
      data: skip ? undefined : mockPreview,
      isFetching: false,
      error: undefined,
      refetch: mockRefetch,
    }),
  }),
)

const preview = (unresolved: boolean) => ({
  remap: {
    from: { id: 'w-1', key: 3, name: 'Default Intake Workflow' },
    to: { id: 'w-2', key: 4, name: 'Board Review' },
    entries: [
      {
        from: status('s-submitted', 'Submitted', 1),
        to: status('t-submitted', 'Submitted', 1),
        matchedBy: 'Alias',
        recordCount: 2,
      },
      {
        from: status('s-review', 'In Review', 2),
        to: unresolved ? undefined : status('t-triage', 'Triage', 2),
        matchedBy: unresolved ? 'Unresolved' : 'Category',
        recordCount: 1,
      },
    ],
    isComplete: !unresolved,
    affectedRecordCount: 3,
  },
  targetStatuses: [
    status('t-submitted', 'Submitted', 1),
    status('t-triage', 'Triage', 2),
  ],
})

const renderForm = async () =>
  await act(async () =>
    render(
      <ChangeIntakeDeskWorkflowForm
        deskId="d-1"
        deskName="IT"
        workflow={
          {
            workflow: { id: 'w-1', key: 3, name: 'Default Intake Workflow' },
            followsOrganizationDefault: true,
            canChange: true,
          } as any
        }
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    ),
  )

const chooseBoardReview = async () => {
  await userEvent.click(screen.getByRole('combobox', { name: 'Workflow' }))
  await userEvent.click(await screen.findByTitle('Board Review'))
}

const next = () => userEvent.click(screen.getByRole('button', { name: 'Next' }))

describe('ChangeIntakeDeskWorkflowForm', () => {
  beforeEach(() => {
    mockPreview = preview(false)
    mockChangeWorkflow.mockReset()
    mockChangeWorkflow.mockReturnValue({ unwrap: () => Promise.resolve(3) })
    mockSuccess.mockReset()
    mockRefetch.mockReset()
  })

  it('offers only the other workflows, and waits for a choice', async () => {
    // Act
    await renderForm()
    await userEvent.click(screen.getByRole('combobox', { name: 'Workflow' }))

    // Assert
    expect(await screen.findByTitle('Board Review')).toBeInTheDocument()
    expect(
      screen.queryByTitle('Default Intake Workflow'),
    ).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
  })

  it('says how many requests on the desk will move', async () => {
    // Arrange
    await renderForm()

    // Act
    await chooseBoardReview()

    // Assert
    expect(
      screen.getByText(
        '3 requests on the desk will move to a status in Board Review.',
      ),
    ).toBeInTheDocument()
  })

  it('holds the mapping step until every status has somewhere to go', async () => {
    // Arrange
    mockPreview = preview(true)
    await renderForm()
    await chooseBoardReview()

    // Act
    await next()

    // Assert
    expect(screen.getByText('In Review')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
  })

  it('moves the desk with the mapping it was shown, and says how many moved', async () => {
    // Arrange
    await renderForm()
    await chooseBoardReview()
    await next()
    await next()

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Change' })),
    )

    // Assert
    expect(mockChangeWorkflow).toHaveBeenCalledWith({
      deskId: 'd-1',
      workflowId: 'w-2',
      decisions: [
        { fromStatusId: 's-submitted', toStatusId: 't-submitted' },
        { fromStatusId: 's-review', toStatusId: 't-triage' },
      ],
    })
    expect(mockSuccess).toHaveBeenCalledWith(
      'Workflow changed; 3 requests moved.',
    )
  })

  it('goes straight to confirming for a desk with no request, having nothing to map', async () => {
    // Arrange
    mockPreview = {
      ...preview(true),
      remap: {
        ...preview(true).remap,
        entries: preview(true).remap.entries.map((entry) => ({
          ...entry,
          recordCount: 0,
        })),
        affectedRecordCount: 0,
      },
    }
    await renderForm()
    await chooseBoardReview()

    // Act
    await next()

    // Assert
    expect(screen.queryByText('In Review')).not.toBeInTheDocument()
    expect(screen.queryByText('Mapping')).not.toBeInTheDocument()
    expect(screen.getByText('No request will move.')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Change' })).toBeEnabled()
  })

  it('says nothing about status history for a desk with no request', async () => {
    // Arrange
    mockPreview = {
      ...preview(false),
      remap: { ...preview(false).remap, affectedRecordCount: 0 },
    }
    await renderForm()

    // Act
    await chooseBoardReview()

    // Assert
    expect(
      screen.getByText('No request is on the desk, so none will move.'),
    ).toBeInTheDocument()
    expect(
      screen.queryByText(
        "Each move is recorded in the request's status history.",
      ),
    ).not.toBeInTheDocument()
  })

  it('shows all three steps until a preview says the desk is empty', async () => {
    // Act
    await renderForm()

    // Assert
    expect(screen.getByText('Mapping')).toBeInTheDocument()
  })

  it('shows a refusal on the mapping, with the choices kept and the counts refreshed', async () => {
    // Arrange
    const refusal =
      "Request 12 would move to 'Triage', which needs a reason it does not have. Choose another status for 'In Review'."
    mockChangeWorkflow.mockReturnValue({
      unwrap: () => Promise.reject({ status: 400, detail: refusal }),
    })
    await renderForm()
    await chooseBoardReview()
    await next()
    await next()

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Change' })),
    )

    // Assert
    expect(screen.getByText(refusal)).toBeInTheDocument()
    expect(screen.getByText('In Review')).toBeInTheDocument()
    expect(mockRefetch).toHaveBeenCalled()
    expect(mockSuccess).not.toHaveBeenCalled()
  })
})
