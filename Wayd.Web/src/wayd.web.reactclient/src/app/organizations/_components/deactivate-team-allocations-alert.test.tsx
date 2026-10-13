import { render, screen } from '@testing-library/react'
import DeactivateTeamAllocationsAlert from './deactivate-team-allocations-alert'

const mockUseFeatureFlag = jest.fn()
jest.mock('@/src/hooks', () => ({
  useFeatureFlag: (name: string) => mockUseFeatureFlag(name),
}))

describe('DeactivateTeamAllocationsAlert', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('warns that allocations end when Team Capacity Management is on', () => {
    mockUseFeatureFlag.mockReturnValue({ isEnabled: true, isLoading: false })

    render(<DeactivateTeamAllocationsAlert />)

    expect(mockUseFeatureFlag).toHaveBeenCalledWith('team-capacity-management')
    expect(screen.getByRole('alert')).toHaveTextContent(
      "People's allocations to this team end on the inactive date.",
    )
    expect(screen.getByRole('alert')).toHaveTextContent(
      'Reactivating the team does not bring them back.',
    )
  })

  it('renders nothing when Team Capacity Management is off', () => {
    mockUseFeatureFlag.mockReturnValue({ isEnabled: false, isLoading: false })

    const { container } = render(<DeactivateTeamAllocationsAlert />)

    expect(container).toBeEmptyDOMElement()
  })
})
