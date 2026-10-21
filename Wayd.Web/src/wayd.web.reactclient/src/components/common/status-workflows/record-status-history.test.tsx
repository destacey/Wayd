import { act, fireEvent, render, screen } from '@testing-library/react'
import {
  StatusCategory,
  StatusTransitionDto,
  StatusWorkflowDetailsDto,
  TransitionMode,
} from '@/src/services/wayd-api'
import RecordStatusHistory, {
  RecordStatusHistoryProps,
} from './record-status-history'
import type { WaydWorkflowDiagramProps } from './wayd-workflow-diagram'

// The canvas is React Flow's; what matters here is what it is handed.
jest.mock('./wayd-workflow-diagram', () => ({
  __esModule: true,
  default: ({ workflow, path }: WaydWorkflowDiagramProps) => (
    <div data-testid="workflow-diagram">
      {workflow.name}: current {path?.currentStatusId ?? 'none'}, taken{' '}
      {[...(path?.takenMoves ?? [])].join(' ')}
    </div>
  ),
}))

const workflow: StatusWorkflowDetailsDto = {
  id: 'wf-2',
  key: 9,
  name: 'Gated Lifecycle',
  owner: { key: 'product.product', name: 'Product' },
  state: 'Published',
  isSystem: false,
  isAssigned: true,
  statuses: [
    {
      id: 'concept',
      name: 'Concept',
      category: { id: 0, name: 'Proposed' },
      alias: 0,
      order: 0,
    },
    {
      id: 'active',
      name: 'Active',
      category: { id: 1, name: 'Active' },
      alias: 1,
      order: 1,
    },
  ],
  transitionMode: TransitionMode.Restricted,
  transitions: [
    { id: 't1', fromStatusId: 'concept', toStatusId: 'active', conditions: [] },
  ],
  availableConditions: [],
  declaredMoves: [],
  supportsRestrictedTransitions: true,
  transitionIssues: [],
  missingRequiredAliases: [],
  canEdit: false,
  canPublish: false,
  canArchive: false,
}

const entry = (
  sequence: number,
  from: [string, string] | undefined,
  to: [string, string],
  workflowId: string,
): StatusTransitionDto => ({
  id: `h-${sequence}`,
  sequence,
  fromStatus: from
    ? { id: from[0], name: from[1], category: StatusCategory.Proposed }
    : undefined,
  toStatus: {
    id: to[0],
    name: to[1],
    category: StatusCategory.Active,
    alias: 0,
  },
  workflowId,
  actorKind: { id: 0, name: 'User' },
  changedBySystem: false,
  changedOn: new Date(Date.UTC(2026, 4, sequence + 1, 10)),
})

// Newest first, as the API returns it: one change under an earlier workflow, the switch onto this one,
// and a move under it.
const history = [
  entry(3, ['concept', 'Concept'], ['active', 'Active'], 'wf-2'),
  entry(2, ['draft', 'Draft'], ['concept', 'Concept'], 'wf-2'),
  entry(1, undefined, ['draft', 'Draft'], 'wf-1'),
]

const renderHistory = (overrides: Partial<RecordStatusHistoryProps> = {}) =>
  render(
    <RecordStatusHistory
      transitions={history}
      isLoading={false}
      view="timeline"
      onViewChange={jest.fn()}
      workflow={undefined}
      isWorkflowLoading={false}
      isWorkflowError={false}
      {...overrides}
    />,
  )

describe('RecordStatusHistory', () => {
  it('shows the timeline by default and asks for the diagram when chosen', async () => {
    // Arrange
    const onViewChange = jest.fn()
    renderHistory({ onViewChange })

    // Act
    await act(async () => {
      fireEvent.click(screen.getByText('Diagram'))
    })

    // Assert
    expect(onViewChange).toHaveBeenCalledWith('diagram')
    expect(screen.getAllByText('Draft').length).toBeGreaterThan(0)
    expect(screen.queryByTestId('workflow-diagram')).not.toBeInTheDocument()
  })

  it('draws the path under the current workflow and lists what came before it', async () => {
    // Act
    renderHistory({ view: 'diagram', workflow })

    // Assert
    expect(await screen.findByTestId('workflow-diagram')).toHaveTextContent(
      'Gated Lifecycle: current active, taken concept:active',
    )
    expect(screen.getByText('Under an earlier workflow')).toBeInTheDocument()
    // The switch (Draft → Concept) and the change before it are listed; the drawn move is not.
    expect(screen.getAllByText('Draft')).toHaveLength(2)
    expect(screen.queryByText('Active')).not.toBeInTheDocument()
  })

  it('says the workflow could not be loaded, pointing back to the timeline', () => {
    // Act
    renderHistory({ view: 'diagram', isWorkflowError: true })

    // Assert
    expect(
      screen.getByText('The workflow could not be loaded'),
    ).toBeInTheDocument()
    expect(screen.queryByTestId('workflow-diagram')).not.toBeInTheDocument()
  })
})
