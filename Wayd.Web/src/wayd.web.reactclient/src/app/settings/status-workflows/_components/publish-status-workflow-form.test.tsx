import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  StatusCategory,
  StatusWorkflowDetailsDto,
  TransitionMode,
} from '@/src/services/wayd-api'
import PublishStatusWorkflowForm from './publish-status-workflow-form'

const publishStatusWorkflow = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: jest.fn(), error: jest.fn() }),
}))

// Not spread from the real barrel: it re-exports store-bound hooks. The stand-in
// keeps the hook's contract — OK submits, and a successful submit completes.
jest.mock('@/src/hooks', () => ({
  useConfirmModal: (options: {
    onSubmit: () => Promise<boolean>
    onComplete: () => void
    onCancel: () => void
  }) => ({
    isOpen: true,
    isSaving: false,
    handleOk: async () => {
      if (await options.onSubmit()) options.onComplete()
    },
    handleCancel: options.onCancel,
  }),
}))

jest.mock('@/src/store/features/common/status-workflows-api', () => ({
  usePublishStatusWorkflowMutation: () => [publishStatusWorkflow],
}))

const workflow = (
  overrides: Partial<StatusWorkflowDetailsDto> = {},
): StatusWorkflowDetailsDto => ({
  id: '44444444-0000-4000-a000-000000000004',
  key: 7,
  name: 'Version Lifecycle',
  owner: { key: 'delivery.version', name: 'Version' },
  state: 'Draft',
  isSystem: false,
  isAssigned: false,
  statuses: [],
  missingRequiredAliases: [],
  missingRequiredCategories: [],
  misfiledAliases: [],
  transitionMode: TransitionMode.AnyToAny,
  transitions: [],
  availableConditions: [],
  declaredMoves: [],
  supportsRestrictedTransitions: false,
  transitionIssues: [],
  canEdit: true,
  canPublish: true,
  canArchive: false,
  ...overrides,
})

describe('PublishStatusWorkflowForm', () => {
  beforeEach(() => {
    publishStatusWorkflow.mockReset()
  })

  it('names each missing required alias and disables Publish', () => {
    // Arrange
    const draft = workflow({
      missingRequiredAliases: ['Ready', 'Withdrawn'],
      canPublish: false,
    })

    // Act
    render(
      <PublishStatusWorkflowForm
        statusWorkflow={draft}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    )

    // Assert
    expect(
      screen.getByText('Required meanings are not covered'),
    ).toBeInTheDocument()
    expect(screen.getByText('Ready')).toBeInTheDocument()
    expect(screen.getByText('Withdrawn')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Publish' })).toBeDisabled()
  })

  it('names each transition problem and disables Publish', () => {
    // Arrange
    const draft = workflow({
      transitionMode: TransitionMode.Restricted,
      transitionIssues: [
        "'Sunset' has no transition out of it, so a record that reaches it could not move on.",
      ],
      canPublish: false,
    })

    // Act
    render(
      <PublishStatusWorkflowForm
        statusWorkflow={draft}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    )

    // Assert
    expect(
      screen.getByText('Transitions would strand a status'),
    ).toBeInTheDocument()
    expect(
      screen.getByText(
        "'Sunset' has no transition out of it, so a record that reaches it could not move on.",
      ),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Publish' })).toBeDisabled()
  })

  it('names each missing required category and disables Publish', () => {
    // Arrange
    const draft = workflow({
      missingRequiredCategories: [StatusCategory.Done],
      canPublish: false,
    })

    // Act
    render(
      <PublishStatusWorkflowForm
        statusWorkflow={draft}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    )

    // Assert
    expect(
      screen.getByText('Required categories are not covered'),
    ).toBeInTheDocument()
    expect(screen.getByText('Done')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Publish' })).toBeDisabled()
  })

  it('names each meaning that needs fixing and disables Publish', () => {
    // Arrange
    const draft = workflow({
      misfiledAliases: [
        "'Shipped': 'Released' belongs under Done, not Active.",
      ],
      canPublish: false,
    })

    // Act
    render(
      <PublishStatusWorkflowForm
        statusWorkflow={draft}
        onFormComplete={jest.fn()}
        onFormCancel={jest.fn()}
      />,
    )

    // Assert
    expect(screen.getByText('Some meanings need fixing')).toBeInTheDocument()
    expect(
      screen.getByText("'Shipped': 'Released' belongs under Done, not Active."),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Publish' })).toBeDisabled()
  })

  it('publishes a draft that covers every required alias', async () => {
    // Arrange
    publishStatusWorkflow.mockResolvedValue({ data: undefined })
    const onFormComplete = jest.fn()
    const user = userEvent.setup()

    render(
      <PublishStatusWorkflowForm
        statusWorkflow={workflow()}
        onFormComplete={onFormComplete}
        onFormCancel={jest.fn()}
      />,
    )

    // Act
    await user.click(screen.getByRole('button', { name: 'Publish' }))

    // Assert
    expect(
      screen.queryByText('Required meanings are not covered'),
    ).not.toBeInTheDocument()
    expect(publishStatusWorkflow).toHaveBeenCalledWith(workflow().id)
    expect(onFormComplete).toHaveBeenCalled()
  })
})
