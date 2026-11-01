import { render, screen, within } from '@testing-library/react'
import {
  StatusCategory,
  StatusWorkflowDetailsDto,
  TransitionConditionKind,
  TransitionMode,
} from '@/src/services/wayd-api'
import WaydWorkflowDiagram from './wayd-workflow-diagram'
import { buildRecordPath } from './workflow-diagram-layout'

jest.mock('@/src/components/contexts/theme/use-theme', () => ({
  __esModule: true,
  default: () => ({ currentMode: 'light' }),
}))

// React Flow reads the viewport transform through DOMMatrix, which jsdom does not have.
beforeAll(() => {
  if (typeof window.DOMMatrixReadOnly === 'undefined') {
    class DOMMatrixReadOnly {
      m22 = 1
      constructor() {}
    }
    Object.defineProperty(window, 'DOMMatrixReadOnly', {
      value: DOMMatrixReadOnly,
      writable: true,
    })
  }
})

const workflow = (
  overrides: Partial<StatusWorkflowDetailsDto> = {},
): StatusWorkflowDetailsDto => ({
  id: 'wf-1',
  key: 9,
  name: 'Lifecycle',
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
      aliasName: 'Live',
      order: 1,
    },
    {
      id: 'retired',
      name: 'Retired',
      category: { id: 2, name: 'Done' },
      alias: 2,
      aliasName: 'Retired',
      order: 2,
    },
  ],
  transitionMode: TransitionMode.Restricted,
  transitions: [
    { id: 't1', fromStatusId: 'concept', toStatusId: 'active', conditions: [] },
    { id: 't2', fromStatusId: 'active', toStatusId: 'retired', conditions: [] },
  ],
  availableConditions: [],
  declaredMoves: [],
  supportsRestrictedTransitions: true,
  transitionIssues: [],
  missingRequiredAliases: [],
  missingRequiredCategories: [],
  misfiledAliases: [],
  canEdit: false,
  canPublish: false,
  canArchive: false,
  ...overrides,
})

const statusNode = (name: string) =>
  screen
    .getAllByTestId('workflow-diagram-status')
    .find((node) => within(node).queryByText(name))!

describe('WaydWorkflowDiagram', () => {
  it('draws a node for each status, with its alias and the initial status marked', () => {
    // Act
    render(<WaydWorkflowDiagram workflow={workflow()} />)

    // Assert
    expect(screen.getAllByTestId('workflow-diagram-status')).toHaveLength(3)
    expect(within(statusNode('Concept')).getByText('Start')).toBeInTheDocument()
    expect(
      within(statusNode('Active')).getByText('Alias: Live'),
    ).toBeInTheDocument()
    expect(
      within(statusNode('Active')).queryByText('Start'),
    ).not.toBeInTheDocument()
    expect(screen.getByText('Proposed')).toBeInTheDocument()
    expect(screen.getByText('Done')).toBeInTheDocument()
    expect(screen.queryByText(/Any move is allowed/)).not.toBeInTheDocument()
  })

  it('notes that any move is allowed instead of drawing every pair', () => {
    // Act
    render(
      <WaydWorkflowDiagram
        workflow={workflow({ transitionMode: TransitionMode.AnyToAny })}
      />,
    )

    // Assert
    expect(
      screen.getByText(
        'Any move is allowed: a record can move from any status to any other.',
      ),
    ).toBeInTheDocument()
    expect(screen.getAllByTestId('workflow-diagram-status')).toHaveLength(3)
  })

  it("marks a record's current status and sets back the statuses off its path", () => {
    // Arrange
    const path = buildRecordPath(workflow(), [
      {
        id: 'h1',
        sequence: 0,
        toStatus: {
          id: 'concept',
          name: 'Concept',
          category: StatusCategory.Proposed,
          categoryLabel: 'Proposed',
          alias: 0,
        },
        workflowId: 'wf-1',
        actorKind: { id: 0, name: 'User' },
        changedBySystem: false,
        changedOn: new Date('2026-05-01T10:00:00Z'),
      },
      {
        id: 'h2',
        sequence: 1,
        fromStatus: {
          id: 'concept',
          name: 'Concept',
          category: StatusCategory.Proposed,
          categoryLabel: 'Proposed',
        },
        toStatus: {
          id: 'active',
          name: 'Active',
          category: StatusCategory.Active,
          categoryLabel: 'Active',
          alias: 1,
        },
        workflowId: 'wf-1',
        actorKind: { id: 0, name: 'User' },
        changedBySystem: false,
        changedOn: new Date('2026-05-02T10:00:00Z'),
      },
    ])

    // Act
    render(<WaydWorkflowDiagram workflow={workflow()} path={path} />)

    // Assert
    expect(statusNode('Active')).toHaveAttribute('aria-current', 'step')
    expect(
      within(statusNode('Active')).getByText('Current'),
    ).toBeInTheDocument()
    expect(statusNode('Concept')).not.toHaveAttribute('aria-current')
    expect(screen.getAllByText('Current')).toHaveLength(1)
    expect(
      screen.getByText('Highlighted: the path this record has taken'),
    ).toBeInTheDocument()
    expect(screen.getByText(/Current status: Active\./)).toBeInTheDocument()
  })

  it('lists every move for a screen reader, with what hovering it would say', () => {
    // Arrange
    const gated = workflow({
      transitions: [
        {
          id: 't1',
          fromStatusId: 'concept',
          toStatusId: 'active',
          conditions: [],
        },
        {
          id: 't2',
          fromStatusId: 'active',
          toStatusId: 'retired',
          conditions: [TransitionConditionKind.RequiresReason],
        },
      ],
    })

    // Act
    render(<WaydWorkflowDiagram workflow={gated} />)

    // Assert
    const moves = within(screen.getByRole('list', { name: 'Moves' }))
      .getAllByRole('listitem')
      .map((item) => item.textContent)
    expect(moves).toEqual([
      'Concept → Active',
      'Active → Retired. Requires a reason',
    ])
    expect(screen.getByText(/Lifecycle: 3 statuses\./)).toBeInTheDocument()
  })

  it('says a workflow with no statuses has nothing to draw', () => {
    // Act
    render(
      <WaydWorkflowDiagram
        workflow={workflow({ statuses: [], transitions: [] })}
      />,
    )

    // Assert
    expect(
      screen.getByText('This workflow has no statuses yet.'),
    ).toBeInTheDocument()
    expect(
      screen.queryByTestId('workflow-diagram-status'),
    ).not.toBeInTheDocument()
  })
})
