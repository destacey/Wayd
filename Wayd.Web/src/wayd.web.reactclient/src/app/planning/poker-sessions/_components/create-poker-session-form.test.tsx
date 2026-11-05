import { render, screen } from '@testing-library/react'
import CreatePokerSessionForm from './create-poker-session-form'
import useAuth from '@/src/components/contexts/auth'

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: jest.fn(),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock('@/src/components/contexts/theme', () => ({
  __esModule: true,
  default: () => ({ token: {} }),
}))

jest.mock('@/src/store/features/planning/poker-sessions-api', () => ({
  useCreatePokerSessionMutation: () => [jest.fn()],
}))

jest.mock('@/src/store/features/planning/estimation-scales-api', () => ({
  useGetEstimationScalesQuery: () => ({ data: [], isLoading: false }),
}))

jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeOptionsQuery: () => ({
    data: [
      { value: 'me', label: 'Mia Me' },
      { value: 'other', label: 'Oscar Other' },
    ],
  }),
}))

jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamOptionsQuery: () => ({ data: [] }),
}))

const signInAs = (employeeId: string) =>
  (useAuth as jest.Mock).mockReturnValue({
    user: { employeeId },
    hasPermissionClaim: () => true,
  })

const renderForm = () =>
  render(
    <CreatePokerSessionForm
      onFormCreate={jest.fn()}
      onFormCancel={jest.fn()}
    />,
  )

describe('CreatePokerSessionForm', () => {
  it('starts with the current employee as the facilitator', () => {
    signInAs('me')

    renderForm()

    expect(screen.getByText('Mia Me')).toBeInTheDocument()
    expect(screen.queryByText('Oscar Other')).not.toBeInTheDocument()
  })
})
