import { render, screen } from '@testing-library/react'

const mockAuth = {
  employeeId: 'emp-1' as string | null,
  permissions: ['Permissions.Roadmaps.View', 'Permissions.Roadmaps.Create'],
}

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({
    hasClaim: vi.fn(() => true),
    hasPermissionClaim: (permission: string) =>
      mockAuth.permissions.includes(permission),
  }),
}))

vi.mock('@/src/hooks', () => ({
  useLinkedEmployee: () => ({
    employeeId: mockAuth.employeeId,
    hasLinkedEmployee: mockAuth.employeeId !== null,
  }),
}))

vi.mock('@/src/hooks/use-document-title', () => ({
  useDocumentTitle: vi.fn(),
}))

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

// The page is exported wrapped in authorizePage; render the inner page directly.
vi.mock('@/src/components/hoc', () => ({
  authorizePage: (component: unknown) => component,
}))

vi.mock('@/src/store/features/planning/roadmaps-api', () => ({
  useGetRoadmapsQuery: () => ({
    data: [],
    isLoading: false,
    error: undefined,
    refetch: vi.fn(),
  }),
}))

vi.mock('./_components', () => ({
  CreateRoadmapForm: () => null,
  RoadmapsFilterBar: () => null,
  RoadmapsGrid: () => null,
}))

import RoadmapsPage from './page'

describe('RoadmapsPage', () => {
  beforeEach(() => {
    mockAuth.employeeId = 'emp-1'
    mockAuth.permissions = [
      'Permissions.Roadmaps.View',
      'Permissions.Roadmaps.Create',
    ]
  })

  it('shows Create Roadmap when the user has the permission and a linked employee', () => {
    render(<RoadmapsPage />)

    expect(
      screen.getByRole('button', { name: 'Create Roadmap' }),
    ).toBeInTheDocument()
  })

  it('hides Create Roadmap when the user has no linked employee', () => {
    // Creating a roadmap records the creator as its manager, so the API rejects an unlinked
    // account with 403.
    mockAuth.employeeId = null

    render(<RoadmapsPage />)

    expect(
      screen.queryByRole('button', { name: 'Create Roadmap' }),
    ).not.toBeInTheDocument()
  })

  it('explains why Create Roadmap is unavailable when the user has the permission but no link', () => {
    // Hiding the button silently reads as a bug; the notice names the cause and the remedy.
    mockAuth.employeeId = null

    render(<RoadmapsPage />)

    expect(
      screen.getByText("Your account isn't linked to an employee record"),
    ).toBeInTheDocument()
  })

  it('does not explain the missing link to users who lack the create permission anyway', () => {
    mockAuth.employeeId = null
    mockAuth.permissions = ['Permissions.Roadmaps.View']

    render(<RoadmapsPage />)

    expect(
      screen.queryByText("Your account isn't linked to an employee record"),
    ).not.toBeInTheDocument()
  })
})
