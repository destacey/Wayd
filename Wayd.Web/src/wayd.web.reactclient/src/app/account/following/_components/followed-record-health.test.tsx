import { render, screen } from '@testing-library/react'
import {
  FollowedRecordDto,
  HealthStatus,
  LifecycleCategory,
} from '@/src/services/wayd-api'
import FollowedRecordHealth from './followed-record-health'

const mockProjectHealthCheckQuery = jest.fn()
jest.mock('@/src/store/features/ppm/project-health-checks-api', () => ({
  useGetProjectHealthCheckQuery: (...args: unknown[]) =>
    mockProjectHealthCheckQuery(...args),
}))

const mockObjectiveHealthCheckQuery = jest.fn()
jest.mock(
  '@/src/store/features/planning/pi-objective-health-checks-api',
  () => ({
    useGetObjectiveHealthCheckQuery: (...args: unknown[]) =>
      mockObjectiveHealthCheckQuery(...args),
  }),
)

const record = (overrides: Partial<FollowedRecordDto>): FollowedRecordDto => ({
  ownerType: 'ppm.project',
  id: 'record-1',
  key: 'K-1',
  name: 'Record',
  statusName: 'Active',
  lifecycle: LifecycleCategory.Active,
  health: HealthStatus.AtRisk,
  healthCheck: { id: 'hc-1', status: { id: 2, name: 'At Risk' } },
  followedOn: new Date('2026-10-24T09:00:00Z'),
  isMuted: false,
  ...overrides,
})

describe('FollowedRecordHealth', () => {
  beforeEach(() => {
    mockProjectHealthCheckQuery.mockReturnValue({
      data: undefined,
      isLoading: false,
    })
    mockObjectiveHealthCheckQuery.mockReturnValue({
      data: undefined,
      isLoading: false,
    })
  })

  it("shows a project's health check, read through the project", () => {
    // Act
    render(<FollowedRecordHealth record={record({})} variant="tag" />)

    // Assert
    expect(screen.getByText('At Risk')).toBeInTheDocument()
    expect(mockProjectHealthCheckQuery).toHaveBeenCalledWith(
      { projectId: 'record-1', healthCheckId: 'hc-1' },
      expect.anything(),
    )
    expect(mockObjectiveHealthCheckQuery).not.toHaveBeenCalled()
  })

  it("shows an objective's health check, read through its planning interval", () => {
    // Act
    render(
      <FollowedRecordHealth
        record={record({
          ownerType: 'planning.planning-interval-objective',
          parentId: 'pi-1',
        })}
        variant="flag"
      />,
    )

    // Assert
    expect(screen.getByLabelText('Health: At Risk')).toBeInTheDocument()
    expect(mockObjectiveHealthCheckQuery).toHaveBeenCalledWith(
      {
        planningIntervalId: 'pi-1',
        objectiveId: 'record-1',
        healthCheckId: 'hc-1',
      },
      expect.anything(),
    )
  })

  it('shows nothing when the record has no current health check', () => {
    // Act
    const { container } = render(
      <FollowedRecordHealth
        record={record({ health: undefined, healthCheck: undefined })}
        variant="tag"
      />,
    )

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('shows nothing for a kind of record without health', () => {
    // Act
    const { container } = render(
      <FollowedRecordHealth
        record={record({ ownerType: 'ppm.program' })}
        variant="tag"
      />,
    )

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})
