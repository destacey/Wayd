import React from 'react'
import { render } from '@testing-library/react'
import StatusWorkflowsPage from './page'

// The grid is not under test: the page hands its rows' actions to the actions column, captured here.
let mockGetItems: ((workflow: any) => any[]) | undefined
vi.mock('@/src/components/common/wayd-grid-core', () => ({
  createActionsColumn: ({
    getItems,
  }: {
    getItems: (workflow: any) => any[]
  }) => {
    mockGetItems = getItems
    return { id: 'actions' }
  },
}))
vi.mock('@/src/components/common/wayd-grid', () => ({
  WaydGrid: () => null,
}))
vi.mock('@/src/components/common', () => ({
  PageTitle: () => null,
}))
vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({ hasPermissionClaim: () => true }),
}))
vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))
vi.mock('@/src/hooks', () => ({ useDocumentTitle: vi.fn() }))
vi.mock('@/src/components/hoc', () => ({
  authorizePage: (Component: React.ComponentType<any>) => Component,
}))
vi.mock('./_components/clone-status-workflow-form', () => ({
  default: () => null,
}))
vi.mock('./_components/reassign-workflow-modal', () => ({
  default: () => null,
}))
vi.mock('./_components/create-status-workflow-form', () => ({
  default: () => null,
}))

const assignment = (workflowId: string, scopeId?: string) => ({
  id: `a-${workflowId}-${scopeId ?? 'org'}`,
  owner: { key: 'demand.intake-request', name: 'Intake Request' },
  scopeId,
  workflow: { id: workflowId, key: 1, name: workflowId },
})

const mockAssignments = [
  assignment('w-default'),
  assignment('w-board', 'desk-1'),
]
vi.mock('@/src/store/features/common/status-workflows-api', () => ({
  useGetStatusWorkflowsQuery: () => ({
    data: [],
    isLoading: false,
    error: undefined,
    refetch: vi.fn(),
  }),
  useGetWorkflowAssignmentsQuery: () => ({
    data: mockAssignments,
    refetch: vi.fn(),
  }),
}))

const workflow = (id: string) => ({ id, key: 1, name: id, isAssigned: true })

describe('StatusWorkflowsPage', () => {
  it('offers Change workflow on the workflow an owner type uses organization-wide', () => {
    // Arrange
    render(<StatusWorkflowsPage />)

    // Act
    const items = mockGetItems!(workflow('w-default'))

    // Assert
    expect(items.map((i) => i.key)).toContain('change')
  })

  it('does not offer Change workflow on a workflow only one intake desk chose', () => {
    // Arrange — the desk changes it from its own Settings
    render(<StatusWorkflowsPage />)

    // Act
    const items = mockGetItems!(workflow('w-board'))

    // Assert
    expect(items.map((i) => i.key)).not.toContain('change')
  })
})
