import React from 'react'
import { render } from '@testing-library/react'
import { ProjectDetailsDto } from '@/src/services/wayd-api'
import type { MentionCandidate } from '@/src/components/common/discussions'
import ProjectDiscussion from './project-discussion'

interface CapturedProps {
  channelType: string
  ownerId: string
  mentionCandidates: MentionCandidate[]
  noteMentionCandidates?: MentionCandidate[]
  audience: Record<string, string>
}
let captured: CapturedProps
jest.mock('@/src/components/common/discussions', () => ({
  RecordDiscussion: (props: CapturedProps) => {
    captured = props
    return null
  },
}))

jest.mock('@/src/store/features/common/discussions-api', () => ({
  DiscussionChannelTypes: { Project: 'ppm.project' },
}))

const mockHasPermissionClaim = jest.fn()
jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: mockHasPermissionClaim }),
}))

const mockUseEmployees = jest.fn()
jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeesQuery: (...args: unknown[]) => mockUseEmployees(...args),
}))

const owner = { id: 'e-1', key: 1, name: 'Priya Natarajan' }
const manager = { id: 'e-2', key: 2, name: 'Tom Becker' }

const project = {
  id: 'p-1',
  key: 'APOLLO',
  projectSponsors: [],
  projectOwners: [owner],
  projectManagers: [manager, owner],
  projectMembers: [],
} as unknown as ProjectDetailsDto

describe('ProjectDiscussion', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockHasPermissionClaim.mockReturnValue(false)
    mockUseEmployees.mockReturnValue({ data: undefined })
  })

  it("holds the project's conversation, with no internal notes", () => {
    // Act
    render(<ProjectDiscussion project={project} />)

    // Assert
    expect(captured.channelType).toBe('ppm.project')
    expect(captured.ownerId).toBe('p-1')
    expect(captured.noteMentionCandidates).toBeUndefined()
    expect(Object.keys(captured.audience)).toEqual(['Public'])
  })

  it("offers the project's own people for mentions to a reader who cannot see employees", () => {
    // Act
    render(<ProjectDiscussion project={project} />)

    // Assert
    expect(mockUseEmployees).toHaveBeenCalledWith(false, { skip: true })
    expect(captured.mentionCandidates.map((c) => c.name)).toEqual([
      'Priya Natarajan',
      'Tom Becker',
    ])
  })

  it('offers every active employee for mentions to a reader who can see them', () => {
    // Arrange
    mockHasPermissionClaim.mockImplementation(
      (claim: string) => claim === 'Permissions.Employees.View',
    )
    mockUseEmployees.mockReturnValue({
      data: [{ id: 'e-9', key: 9, displayName: 'Marcus Lee' }],
    })

    // Act
    render(<ProjectDiscussion project={project} />)

    // Assert
    expect(mockUseEmployees).toHaveBeenCalledWith(false, { skip: false })
    expect(captured.mentionCandidates).toEqual([
      { id: 'e-9', key: 9, name: 'Marcus Lee' },
    ])
  })
})
