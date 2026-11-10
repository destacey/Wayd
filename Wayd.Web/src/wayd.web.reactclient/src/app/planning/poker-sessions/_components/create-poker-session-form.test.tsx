import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import CreatePokerSessionForm from './create-poker-session-form'
import useAuth from '@/src/components/contexts/auth'

vi.mock('@/src/components/contexts/auth', () => ({
  default: vi.fn(),
}))

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

vi.mock('@/src/components/contexts/theme', () => ({
  default: () => ({ token: {} }),
}))

vi.mock('@/src/store/features/planning/poker-sessions-api', () => ({
  useCreatePokerSessionMutation: () => [vi.fn()],
}))

vi.mock('@/src/store/features/planning/estimation-scales-api', () => ({
  useGetEstimationScalesQuery: () => ({ data: [], isLoading: false }),
}))

vi.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeOptionsQuery: () => ({
    data: [
      { value: 'me', label: 'Mia Me' },
      { value: 'other', label: 'Oscar Other' },
    ],
  }),
}))

vi.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamOptionsQuery: () => ({ data: [] }),
}))

const signInAs = (employeeId: string) =>
  (useAuth as Mock).mockReturnValue({
    user: { employeeId },
    hasPermissionClaim: () => true,
  })

const renderForm = () =>
  render(
    <CreatePokerSessionForm onFormCreate={vi.fn()} onFormCancel={vi.fn()} />,
  )

describe('CreatePokerSessionForm', () => {
  it('starts with the current employee as the facilitator', () => {
    signInAs('me')

    renderForm()

    expect(screen.getByText('Mia Me')).toBeInTheDocument()
    expect(screen.queryByText('Oscar Other')).not.toBeInTheDocument()
  })
})
