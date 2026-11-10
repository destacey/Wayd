import { render, screen } from '@testing-library/react'

// Mock WaydGrid with a light stand-in that renders the toolbar slots under test.
vi.mock('../wayd-grid', () => ({
  WaydGrid: vi.fn(({ leftSlot }) => (
    <div data-testid="wayd-grid">
      <div data-testid="left-slot">{leftSlot}</div>
    </div>
  )),
  createActionsColumn: vi.fn(() => ({ id: 'actions' })),
  renderTeamLink: vi.fn(() => null),
}))

vi.mock('../control-items-menu', () => ({
  ControlItemsMenu: () => null,
  ControlItemSwitch: () => null,
}))

vi.mock('./create-risk-form', () => ({
  default: () => null,
}))

vi.mock('./edit-risk-form', () => ({
  default: () => null,
}))

const mockAuth = {
  employeeId: 'emp-1' as string | null,
  permissions: ['Permissions.Risks.Create', 'Permissions.Risks.Update'],
}

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({
    user: { employeeId: mockAuth.employeeId },
    hasPermissionClaim: (permission: string) =>
      mockAuth.permissions.includes(permission),
  }),
}))

import RisksGrid from './risks-grid'

describe('RisksGrid', () => {
  const renderGrid = () =>
    render(
      <RisksGrid
        risks={[]}
        updateIncludeClosed={vi.fn()}
        isLoadingRisks={false}
        refreshRisks={vi.fn()}
        newRisksAllowed
      />,
    )

  beforeEach(() => {
    mockAuth.employeeId = 'emp-1'
    mockAuth.permissions = [
      'Permissions.Risks.Create',
      'Permissions.Risks.Update',
    ]
  })

  it('shows Create Risk when the user has the permission and a linked employee', () => {
    renderGrid()

    expect(
      screen.getByRole('button', { name: 'Create Risk' }),
    ).toBeInTheDocument()
  })

  it('hides Create Risk when the user has no linked employee', () => {
    // Creating a risk records the reporter, so the API rejects an unlinked account with 403.
    // Offering the button would walk the user into a form that cannot be submitted.
    mockAuth.employeeId = null

    renderGrid()

    expect(
      screen.queryByRole('button', { name: 'Create Risk' }),
    ).not.toBeInTheDocument()
  })

  it('hides Create Risk when the user lacks the create permission', () => {
    mockAuth.permissions = ['Permissions.Risks.Update']

    renderGrid()

    expect(
      screen.queryByRole('button', { name: 'Create Risk' }),
    ).not.toBeInTheDocument()
  })
})
