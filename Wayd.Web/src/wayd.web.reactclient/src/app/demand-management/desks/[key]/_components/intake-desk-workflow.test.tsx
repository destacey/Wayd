import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import IntakeDeskWorkflow from './intake-desk-workflow'

let mockWorkflow: any
let mockCanViewWorkflows = false

jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeDeskWorkflowQuery: () => ({
      data: mockWorkflow,
      isLoading: false,
    }),
  }),
)

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    hasPermissionClaim: (claim: string) =>
      claim === 'Permissions.StatusWorkflows.View' && mockCanViewWorkflows,
  }),
}))

// The form is covered on its own; here only whether the setting opens it.
jest.mock('./change-intake-desk-workflow-form', () => ({
  __esModule: true,
  default: () => <div>Change Workflow form</div>,
}))

const workflow = (overrides: Record<string, unknown> = {}) => ({
  workflow: { id: 'w-1', key: 3, name: 'Default Intake Workflow' },
  followsOrganizationDefault: true,
  hasRequests: false,
  canChange: false,
  ...overrides,
})

const renderSetting = () =>
  render(<IntakeDeskWorkflow deskId="d-1" deskName="IT" />)

describe('IntakeDeskWorkflow', () => {
  beforeEach(() => {
    mockWorkflow = workflow()
    mockCanViewWorkflows = false
  })

  it('names the workflow and says when the desk follows the default', () => {
    // Act
    renderSetting()

    // Assert
    expect(screen.getByText('Default Intake Workflow')).toBeInTheDocument()
    expect(screen.getByText('Organization default')).toBeInTheDocument()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })

  it('links to the workflow for someone who can open it in Settings', () => {
    // Arrange
    mockCanViewWorkflows = true

    // Act
    renderSetting()

    // Assert
    expect(
      screen.getByRole('link', { name: 'Default Intake Workflow' }),
    ).toHaveAttribute('href', '/settings/status-workflows/3')
  })

  it('offers the change only where the server allows it, and opens the form', async () => {
    // Arrange
    mockWorkflow = workflow({ canChange: true })
    renderSetting()

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: 'Change Workflow' }),
    )

    // Assert
    expect(screen.getByText('Change Workflow form')).toBeInTheDocument()
  })

  it('says why the workflow cannot change while requests are on the desk', () => {
    // Arrange
    mockWorkflow = workflow({
      hasRequests: true,
      followsOrganizationDefault: false,
    })

    // Act
    renderSetting()

    // Assert
    expect(
      screen.getByText('It cannot change while requests are on the desk.'),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Change Workflow' }),
    ).not.toBeInTheDocument()
    expect(screen.queryByText('Organization default')).not.toBeInTheDocument()
  })
})
