import { act, fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  StatusCategory,
  StatusWorkflowDetailsDto,
  TransitionMode,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'
import WorkflowTransitionsEditor from './workflow-transitions-editor'

const setTransitionMode = jest.fn()
const setTransitions = jest.fn()

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: jest.fn(), error: jest.fn() }),
}))

jest.mock('@/src/store/features/common/status-workflows-api', () => ({
  useSetWorkflowTransitionModeMutation: () => [
    setTransitionMode,
    { isLoading: false },
  ],
  useSetWorkflowTransitionsMutation: () => [
    setTransitions,
    { isLoading: false },
  ],
}))

const status = (
  id: string,
  name: string,
  category: StatusCategory,
  order: number,
): WorkflowStatusDto => ({
  id,
  name,
  category: { id: 0, name: category },
  alias: 0,
  order,
})

const CONCEPT = '11111111-0000-4000-a000-000000000001'
const ACTIVE = '11111111-0000-4000-a000-000000000002'
const RETIRED = '11111111-0000-4000-a000-000000000003'

const workflow = (
  overrides: Partial<StatusWorkflowDetailsDto> = {},
): StatusWorkflowDetailsDto => ({
  id: '44444444-0000-4000-a000-000000000004',
  key: 7,
  name: 'Gated Lifecycle',
  owner: { key: 'product.product', name: 'Product' },
  state: 'Draft',
  isSystem: false,
  isAssigned: false,
  // Out of order on purpose: the grid follows the workflow's order.
  statuses: [
    status(RETIRED, 'Retired', StatusCategory.Done, 3),
    status(CONCEPT, 'Concept', StatusCategory.Proposed, 1),
    status(ACTIVE, 'Active', StatusCategory.Active, 2),
  ],
  transitionMode: TransitionMode.Restricted,
  transitions: [
    { id: 't1', fromStatusId: CONCEPT, toStatusId: ACTIVE, conditions: [] },
  ],
  availableConditions: [],
  supportsRestrictedTransitions: true,
  transitionIssues: [],
  missingRequiredAliases: [],
  canEdit: true,
  canPublish: true,
  canArchive: false,
  ...overrides,
})

describe('WorkflowTransitionsEditor', () => {
  beforeEach(() => {
    setTransitionMode.mockReset().mockResolvedValue({ data: undefined })
    setTransitions.mockReset().mockResolvedValue({ data: undefined })
  })

  it('says an any-to-any workflow allows any move, with no grid', () => {
    // Arrange
    const anyToAny = workflow({
      transitionMode: TransitionMode.AnyToAny,
      transitions: [],
    })

    // Act
    render(<WorkflowTransitionsEditor statusWorkflow={anyToAny} canUpdate />)

    // Assert
    expect(
      screen.getByText('This workflow allows any move'),
    ).toBeInTheDocument()
    expect(screen.queryAllByRole('checkbox')).toHaveLength(0)
  })

  it('checks exactly the moves a restricted workflow allows', () => {
    // Arrange & Act
    render(<WorkflowTransitionsEditor statusWorkflow={workflow()} canUpdate />)

    // Assert
    expect(screen.getByLabelText('Concept to Active')).toBeChecked()
    expect(screen.getByLabelText('Concept to Retired')).not.toBeChecked()
    expect(screen.getByLabelText('Active to Retired')).not.toBeChecked()
    expect(screen.queryByLabelText('Concept to Concept')).toBeNull()
    expect(screen.getAllByRole('checkbox')).toHaveLength(6)
  })

  it('saves the whole set with the move added when a box is checked', async () => {
    // Arrange
    const user = userEvent.setup()
    render(<WorkflowTransitionsEditor statusWorkflow={workflow()} canUpdate />)

    // Act
    await user.click(screen.getByLabelText('Active to Retired'))

    // Assert
    expect(setTransitions).toHaveBeenCalledWith({
      workflowId: '44444444-0000-4000-a000-000000000004',
      request: {
        transitions: [
          { fromStatusId: CONCEPT, toStatusId: ACTIVE },
          { fromStatusId: ACTIVE, toStatusId: RETIRED },
        ],
      },
    })
  })

  it('builds each save on the ticks before it, before the workflow is refetched', async () => {
    // Arrange
    // The prop is never refreshed here, as between a save returning and its
    // refetch landing: a set built from it would drop the first tick.
    const user = userEvent.setup()
    render(<WorkflowTransitionsEditor statusWorkflow={workflow()} canUpdate />)

    // Act
    await user.click(screen.getByLabelText('Active to Retired'))
    await user.click(screen.getByLabelText('Concept to Retired'))

    // Assert
    expect(setTransitions).toHaveBeenLastCalledWith({
      workflowId: '44444444-0000-4000-a000-000000000004',
      request: {
        transitions: [
          { fromStatusId: CONCEPT, toStatusId: ACTIVE },
          { fromStatusId: ACTIVE, toStatusId: RETIRED },
          { fromStatusId: CONCEPT, toStatusId: RETIRED },
        ],
      },
    })
    expect(screen.getByLabelText('Active to Retired')).toBeChecked()
  })

  it('puts a tick back when the save is refused', async () => {
    // Arrange
    setTransitions.mockResolvedValue({ error: { detail: 'Refused.' } })
    render(<WorkflowTransitionsEditor statusWorkflow={workflow()} canUpdate />)

    // Act
    // Inside act: the revert lands after the awaited save, and jest.setup's
    // MessageChannel stub never flushes an update made outside it.
    await act(async () => {
      fireEvent.click(screen.getByLabelText('Active to Retired'))
    })

    // Assert
    expect(setTransitions).toHaveBeenCalled()
    expect(screen.getByLabelText('Active to Retired')).not.toBeChecked()
  })

  it('saves the whole set without the move when a box is cleared', async () => {
    // Arrange
    const user = userEvent.setup()
    render(<WorkflowTransitionsEditor statusWorkflow={workflow()} canUpdate />)

    // Act
    await user.click(screen.getByLabelText('Concept to Active'))

    // Assert
    expect(setTransitions).toHaveBeenCalledWith({
      workflowId: '44444444-0000-4000-a000-000000000004',
      request: { transitions: [] },
    })
  })

  it('switches the mode', async () => {
    // Arrange
    const user = userEvent.setup()
    const anyToAny = workflow({ transitionMode: TransitionMode.AnyToAny })
    render(<WorkflowTransitionsEditor statusWorkflow={anyToAny} canUpdate />)

    // Act
    await user.click(screen.getByText('Only allowed moves'))

    // Assert
    expect(setTransitionMode).toHaveBeenCalledWith({
      workflowId: '44444444-0000-4000-a000-000000000004',
      request: { mode: TransitionMode.Restricted },
    })
  })

  it('locks the grid on a workflow that is no longer a draft', () => {
    // Arrange & Act
    render(
      <WorkflowTransitionsEditor
        statusWorkflow={workflow({ state: 'Published', canEdit: false })}
        canUpdate
      />,
    )

    // Assert
    screen
      .getAllByRole('checkbox')
      .forEach((checkbox) => expect(checkbox).toBeDisabled())
    expect(
      screen.getByText(/Clone this workflow into a draft/),
    ).toBeInTheDocument()
  })

  it('locks the grid without the update permission', () => {
    // Arrange & Act
    render(
      <WorkflowTransitionsEditor
        statusWorkflow={workflow()}
        canUpdate={false}
      />,
    )

    // Assert
    screen
      .getAllByRole('checkbox')
      .forEach((checkbox) => expect(checkbox).toBeDisabled())
  })

  it('names what stops a restricted workflow being published', () => {
    // Arrange & Act
    render(
      <WorkflowTransitionsEditor
        statusWorkflow={workflow({
          transitionIssues: [
            "'Retired' cannot be reached from 'Concept', the initial status.",
          ],
        })}
        canUpdate
      />,
    )

    // Assert
    expect(
      screen.getByText(
        "'Retired' cannot be reached from 'Concept', the initial status.",
      ),
    ).toBeInTheDocument()
  })

  it('explains that an owner type without support always allows any move', () => {
    // Arrange & Act
    render(
      <WorkflowTransitionsEditor
        statusWorkflow={workflow({
          owner: { key: 'delivery.deployment', name: 'Deployment' },
          transitionMode: TransitionMode.AnyToAny,
          supportsRestrictedTransitions: false,
        })}
        canUpdate
      />,
    )

    // Assert
    expect(
      screen.getByText('Deployment workflows always allow any move.'),
    ).toBeInTheDocument()
  })
})
