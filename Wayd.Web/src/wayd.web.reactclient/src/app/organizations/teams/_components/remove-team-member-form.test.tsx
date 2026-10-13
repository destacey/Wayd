import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import RemoveTeamMemberForm from './remove-team-member-form'

jest.unmock('dayjs')

// Mock window.getComputedStyle for Ant Design Modal
Object.defineProperty(window, 'getComputedStyle', {
  value: () => ({
    getPropertyValue: () => '',
  }),
})

const mockMessageSuccess = jest.fn()
const mockMessageError = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({
    success: mockMessageSuccess,
    error: mockMessageError,
  }),
}))

let mockCapacityEnabled = true
jest.mock('@/src/hooks', () => ({
  useFeatureFlag: () => ({ isEnabled: mockCapacityEnabled, isLoading: false }),
}))

const mockRemoveTeamMember = jest.fn()
jest.mock('@/src/store/features/organization/team-members-api', () => ({
  useRemoveTeamMemberMutation: () => [mockRemoveTeamMember],
  useRemoveTeamOfTeamsMemberMutation: () => [jest.fn()],
}))

const mockChangeAllocations = jest.fn()
let mockAllocations: unknown[] = []
jest.mock('@/src/store/features/organization/employee-allocations-api', () => ({
  useGetEmployeeAllocationsQuery: (
    _arg: unknown,
    options: { skip?: boolean },
  ) => ({ data: options?.skip ? undefined : mockAllocations }),
  useChangeEmployeeAllocationsMutation: () => [mockChangeAllocations],
}))

const member = {
  employee: { id: 'employee-1', key: 1, name: 'Ada Lovelace' },
  team: { id: 'team-1', key: 1, name: 'Cards', code: 'CARDS', type: 'Team' },
  roles: [],
}

const allocation = (overrides: Record<string, unknown> = {}) => ({
  id: 'allocation-1',
  team: { id: 'team-1', key: 1, name: 'Cards', code: 'CARDS', type: 'Team' },
  role: { id: 'role-1', key: 1, name: 'Engineer' },
  start: '2020-01-01',
  end: undefined,
  percent: 50,
  canManage: true,
  ...overrides,
})

const renderForm = (onFormComplete = jest.fn()) =>
  render(
    <RemoveTeamMemberForm
      teamId="team-1"
      teamType="Team"
      member={member as never}
      onFormComplete={onFormComplete}
      onFormCancel={jest.fn()}
    />,
  )

const endAllocationLabel = 'Also end their allocation to this team from today'

describe('RemoveTeamMemberForm', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockCapacityEnabled = true
    mockAllocations = []
    mockRemoveTeamMember.mockResolvedValue({ data: undefined })
    mockChangeAllocations.mockResolvedValue({ data: undefined })
  })

  it('removes the member without touching allocations when they have none on the team', async () => {
    const onFormComplete = jest.fn()
    renderForm(onFormComplete)

    expect(screen.queryByText(endAllocationLabel)).not.toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Remove' }))

    await waitFor(() => expect(onFormComplete).toHaveBeenCalled())
    expect(mockRemoveTeamMember).toHaveBeenCalledWith({
      teamId: 'team-1',
      employeeId: 'employee-1',
    })
    expect(mockChangeAllocations).not.toHaveBeenCalled()
  })

  it('ends the allocation in each role from today when the option is left on', async () => {
    mockAllocations = [
      allocation(),
      allocation({ id: 'allocation-2', role: undefined }),
      allocation({ id: 'allocation-3', team: { id: 'team-2' } }),
    ]
    const onFormComplete = jest.fn()
    renderForm(onFormComplete)

    expect(screen.getByText(endAllocationLabel)).toBeInTheDocument()
    await userEvent.click(screen.getByRole('button', { name: 'Remove' }))

    await waitFor(() => expect(onFormComplete).toHaveBeenCalled())
    const { employeeId, request } = mockChangeAllocations.mock.calls[0][0]
    expect(employeeId).toBe('employee-1')
    expect(request.changes).toEqual([
      expect.objectContaining({
        teamId: 'team-1',
        roleId: 'role-1',
        percent: 0,
      }),
      expect.objectContaining({
        teamId: 'team-1',
        roleId: undefined,
        percent: 0,
      }),
    ])
    expect(request.changes[0].start).toMatch(/^\d{4}-\d{2}-\d{2}$/)
    expect(request.changes[0].end).toBeUndefined()
  })

  it('leaves the allocation in place when the option is turned off', async () => {
    mockAllocations = [allocation()]
    const onFormComplete = jest.fn()
    renderForm(onFormComplete)

    await userEvent.click(screen.getByText(endAllocationLabel))
    await userEvent.click(screen.getByRole('button', { name: 'Remove' }))

    await waitFor(() => expect(onFormComplete).toHaveBeenCalled())
    expect(mockChangeAllocations).not.toHaveBeenCalled()
  })

  it('does not offer to end an allocation that has already ended or cannot be managed', () => {
    mockAllocations = [
      allocation({ end: '2020-12-31' }),
      allocation({ id: 'allocation-2', canManage: false }),
    ]
    renderForm()

    expect(screen.queryByText(endAllocationLabel)).not.toBeInTheDocument()
  })

  it('does not read allocations when capacity management is off', () => {
    mockCapacityEnabled = false
    mockAllocations = [allocation()]
    renderForm()

    expect(screen.queryByText(endAllocationLabel)).not.toBeInTheDocument()
  })
})
