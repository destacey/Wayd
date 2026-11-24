import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import EditTeamMemberForm from './edit-team-member-form'

// Mock window.getComputedStyle for Ant Design Modal
Object.defineProperty(window, 'getComputedStyle', {
  value: () => ({
    getPropertyValue: () => '',
  }),
})

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: vi.fn(), error: vi.fn() }),
}))

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({ hasPermissionClaim: () => true }),
}))

let mockActiveRoles: { id: string; key: number; name: string }[] | undefined
vi.mock('@/src/store/features/organization/team-member-roles-api', () => ({
  useGetTeamMemberRolesQuery: () => ({ data: mockActiveRoles }),
}))

const mockUpdateTeamMember = vi.fn()
vi.mock('@/src/store/features/organization/team-members-api', () => ({
  useUpdateTeamMemberMutation: () => [mockUpdateTeamMember],
  useUpdateTeamOfTeamsMemberMutation: () => [vi.fn()],
}))

const engineer = { id: 'role-1', key: 1, name: 'Engineer' }
const archivist = { id: 'role-2', key: 2, name: 'Archivist' }

const member = {
  employee: { id: 'employee-1', key: 1, name: 'Ada Lovelace' },
  team: { id: 'team-1', key: 1, name: 'Cards', code: 'CARDS', type: 'Team' },
  roles: [engineer, archivist],
}

const renderForm = (onFormComplete = vi.fn()) =>
  render(
    <EditTeamMemberForm
      teamId="team-1"
      teamType="Team"
      member={member as never}
      onFormComplete={onFormComplete}
      onFormCancel={vi.fn()}
    />,
  )

describe('EditTeamMemberForm', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockActiveRoles = [engineer]
    mockUpdateTeamMember.mockResolvedValue({ data: undefined })
  })

  it('shows a held role that has since been deactivated by name, marked inactive', () => {
    renderForm()

    expect(screen.getByText('Engineer')).toBeInTheDocument()
    expect(screen.getByText('Archivist (inactive)')).toBeInTheDocument()
  })

  it('offers a held inactive role again after it is removed, and saves it', async () => {
    const onFormComplete = vi.fn()
    renderForm(onFormComplete)

    const archivistTag = screen
      .getByText('Archivist (inactive)')
      .closest('.ant-select-selection-item') as HTMLElement
    await userEvent.click(within(archivistTag).getByLabelText('close'))
    expect(screen.queryByText('Archivist (inactive)')).not.toBeInTheDocument()

    await userEvent.click(screen.getByRole('combobox'))
    // antd's role="option" nodes are an accessibility mirror; the item that selects is the titled one
    await waitFor(() =>
      expect(
        document.querySelector('.ant-select-item-option[title="Archivist (inactive)"]'),
      ).not.toBeNull(),
    )
    await userEvent.click(
      document.querySelector(
        '.ant-select-item-option[title="Archivist (inactive)"]',
      ) as HTMLElement,
    )
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    await waitFor(() => expect(onFormComplete).toHaveBeenCalled())
    expect(mockUpdateTeamMember).toHaveBeenCalledWith({
      teamId: 'team-1',
      employeeId: 'employee-1',
      request: { roleIds: ['role-1', 'role-2'] },
    })
  })

  it('does not mark held roles inactive before the active roles have loaded', () => {
    mockActiveRoles = undefined
    renderForm()

    expect(screen.getByText('Archivist')).toBeInTheDocument()
    expect(screen.queryByText(/\(inactive\)/)).not.toBeInTheDocument()
  })
})
