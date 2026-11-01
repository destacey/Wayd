import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  WorkflowSchemeChangePreviewDto,
  WorkflowSchemeDetailsDto,
} from '@/src/services/wayd-api'
import ChangeSchemeWorkflowModal from './change-scheme-workflow-modal'

const changeDefault = jest.fn()
const setEntry = jest.fn()
let preview: WorkflowSchemeChangePreviewDto | undefined

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: jest.fn(), error: jest.fn() }),
}))

jest.mock('@/src/store/features/common/status-workflows-api', () => ({
  useGetStatusWorkflowsQuery: () => ({
    data: [
      { id: 'standard', key: 1, name: 'Standard' },
      { id: 'lean', key: 2, name: 'Lean' },
    ],
  }),
  useGetStatusWorkflowQuery: () => ({ data: { statuses: [] } }),
}))

jest.mock('@/src/store/features/common/workflow-schemes-api', () => ({
  usePreviewWorkflowSchemeDefaultQuery: (
    _: unknown,
    options: { skip: boolean },
  ) => ({
    data: options.skip ? undefined : preview,
    isFetching: false,
  }),
  usePreviewWorkflowSchemeEntryQuery: () => ({
    data: undefined,
    isFetching: false,
  }),
  useChangeWorkflowSchemeDefaultMutation: () => [
    changeDefault,
    { isLoading: false },
  ],
  useSetWorkflowSchemeEntryMutation: () => [setEntry, { isLoading: false }],
}))

const scheme: WorkflowSchemeDetailsDto = {
  id: 'scheme-1',
  key: 3,
  name: 'Engineering',
  owner: { key: 'work.item', name: 'Work Item' },
  ownership: 'Owned',
  defaultWorkflow: { id: 'standard', key: 1, name: 'Standard' },
  entries: [],
  assignedScopeIds: [],
}

const move = {
  recordType: undefined,
  remap: {
    from: { id: 'standard', key: 1, name: 'Standard' },
    to: { id: 'lean', key: 2, name: 'Lean' },
    entries: [],
    isComplete: true,
    affectedRecordCount: 0,
  },
}

const renderModal = () =>
  render(
    <ChangeSchemeWorkflowModal
      scheme={scheme}
      change={{ kind: 'default' }}
      recordTypes={[]}
      onFormComplete={jest.fn()}
      onFormCancel={jest.fn()}
    />,
  )

/** Picks Lean as the new default and moves on to the review step. */
const chooseLeanAndReview = async (
  user: ReturnType<typeof userEvent.setup>,
) => {
  await user.click(screen.getByRole('combobox', { name: 'Workflow' }))
  await user.click(await screen.findByTitle('Lean'))
  await user.click(screen.getByRole('button', { name: 'Next' }))
}

describe('ChangeSchemeWorkflowModal', () => {
  beforeEach(() => {
    changeDefault.mockReset().mockResolvedValue({ data: 0 })
    setEntry.mockReset()
    preview = undefined
  })

  it('refuses to apply a change that would move records nothing can move', async () => {
    // Arrange
    const user = userEvent.setup()
    preview = {
      assignedScopeCount: 2,
      canMoveRecords: false,
      moves: [move],
      affectedRecordCount: 0,
    }
    renderModal()

    // Act
    await chooseLeanAndReview(user)

    // Assert
    expect(
      screen.getByText(/cannot be moved between workflows by record type yet/),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Apply' })).toBeDisabled()
  })

  it('applies a change to a scheme no scope follows, saying no records move', async () => {
    // Arrange
    const user = userEvent.setup()
    preview = {
      assignedScopeCount: 0,
      canMoveRecords: true,
      moves: [move],
      affectedRecordCount: 0,
    }
    renderModal()

    // Act
    await chooseLeanAndReview(user)
    await user.click(screen.getByRole('button', { name: 'Apply' }))

    // Assert
    expect(
      screen.getByText('No scope follows this scheme yet, so no records move.'),
    ).toBeInTheDocument()
    expect(changeDefault).toHaveBeenCalledWith({
      schemeId: 'scheme-1',
      request: { workflowId: 'lean', decisions: [] },
    })
  })

  it('does not offer the workflow the scheme already defaults to', async () => {
    // Arrange
    const user = userEvent.setup()
    renderModal()

    // Act
    await user.click(screen.getByRole('combobox', { name: 'Workflow' }))

    // Assert
    expect(await screen.findByTitle('Lean')).toBeInTheDocument()
    expect(screen.queryByTitle('Standard')).not.toBeInTheDocument()
  })
})
