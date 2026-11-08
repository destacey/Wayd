import { render } from '@testing-library/react'
import MyTeamSprints from './my-team-sprints'

const mockUser = { employeeId: 'emp-1' as string | null }

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ user: mockUser }),
}))

jest.mock('@/src/store/features/organization/team-members-api', () => ({
  useGetEmployeeTeamMembershipsQuery: jest.fn(),
}))

jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetActiveSprintQuery: jest.fn(() => ({
    data: undefined,
    isLoading: true,
  })),
}))

jest.mock(
  './active-team-sprint',
  () =>
    function MockActiveTeamSprint() {
      return null
    },
)

import { useGetEmployeeTeamMembershipsQuery } from '@/src/store/features/organization/team-members-api'

const mockMemberships = useGetEmployeeTeamMembershipsQuery as jest.Mock

describe('MyTeamSprints', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockUser.employeeId = 'emp-1'
  })

  it('reports nothing while the team memberships are still loading', () => {
    // Arrange
    mockMemberships.mockReturnValue({
      data: undefined,
      isSuccess: false,
      isError: false,
    })
    const onHasSprintsChange = jest.fn()

    // Act
    render(<MyTeamSprints onHasSprintsChange={onHasSprintsChange} />)

    // Assert
    expect(onHasSprintsChange).not.toHaveBeenCalled()
  })

  it('reports no sprints once the memberships arrive empty', () => {
    // Arrange
    mockMemberships.mockReturnValue({
      data: [],
      isSuccess: true,
      isError: false,
    })
    const onHasSprintsChange = jest.fn()

    // Act
    render(<MyTeamSprints onHasSprintsChange={onHasSprintsChange} />)

    // Assert
    expect(onHasSprintsChange).toHaveBeenCalledWith(false)
  })

  it('reports no sprints at once for an unlinked account', () => {
    // Arrange
    mockUser.employeeId = null
    mockMemberships.mockReturnValue({
      data: undefined,
      isSuccess: false,
      isError: false,
    })
    const onHasSprintsChange = jest.fn()

    // Act
    render(<MyTeamSprints onHasSprintsChange={onHasSprintsChange} />)

    // Assert
    expect(onHasSprintsChange).toHaveBeenCalledWith(false)
  })

  it('reports a failure to load the memberships as an error, not as no sprints', () => {
    // Arrange
    mockMemberships.mockReturnValue({
      data: undefined,
      isSuccess: false,
      isError: true,
    })
    const onHasSprintsChange = jest.fn()
    const onLoadError = jest.fn()

    // Act
    render(
      <MyTeamSprints
        onHasSprintsChange={onHasSprintsChange}
        onLoadError={onLoadError}
      />,
    )

    // Assert
    expect(onLoadError).toHaveBeenCalled()
    expect(onHasSprintsChange).not.toHaveBeenCalled()
  })
})
