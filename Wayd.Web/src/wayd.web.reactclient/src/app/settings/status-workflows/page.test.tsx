import React from 'react'
import { render } from '@testing-library/react'
import StatusWorkflowsPage from './page'

// The grid is not under test: the page hands its rows' actions to the actions column, captured here.
let mockGetItems: ((workflow: any) => any[]) | undefined
jest.mock('@/src/components/common/wayd-grid-core', () => ({
  createActionsColumn: ({
    getItems,
  }: {
    getItems: (workflow: any) => any[]
  }) => {
    mockGetItems = getItems
    return { id: 'actions' }
  },
}))
jest.mock('@/src/components/common/wayd-grid', () => ({
  WaydGrid: () => null,
}))
jest.mock('@/src/components/common', () => ({
  PageTitle: () => null,
}))
jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: () => true }),
}))
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))
jest.mock('@/src/hooks', () => ({ useDocumentTitle: jest.fn() }))
jest.mock('@/src/components/hoc', () => ({
  authorizePage: (Component: React.ComponentType<any>) => Component,
}))
jest.mock('./_components/clone-status-workflow-form', () => () => null)
jest.mock('./_components/reassign-workflow-modal', () => () => null)
jest.mock('./_components/create-status-workflow-form', () => () => null)

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
jest.mock('@/src/store/features/common/status-workflows-api', () => ({
  useGetStatusWorkflowsQuery: () => ({
    data: [],
    isLoading: false,
    error: undefined,
    refetch: jest.fn(),
  }),
  useGetWorkflowAssignmentsQuery: () => ({
    data: mockAssignments,
    refetch: jest.fn(),
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
