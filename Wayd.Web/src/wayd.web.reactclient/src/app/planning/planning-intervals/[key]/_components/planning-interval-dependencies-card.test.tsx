import { DependencyHealth } from '@/src/components/types'
import { DependencyDto } from '@/src/services/wayd-api'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import PlanningIntervalDependenciesCard from './planning-interval-dependencies-card'

let mockDependencies: DependencyDto[] = []
jest.mock('@/src/store/features/planning/planning-interval-api', () => ({
  useGetPlanningIntervalDependenciesQuery: () => ({
    data: mockDependencies,
    isLoading: false,
  }),
}))

const healthNames: Record<DependencyHealth, string> = {
  [DependencyHealth.Healthy]: 'Healthy',
  [DependencyHealth.AtRisk]: 'At Risk',
  [DependencyHealth.Unhealthy]: 'Unhealthy',
  [DependencyHealth.Unknown]: 'Unknown',
  [DependencyHealth.Closed]: 'Closed',
}

const dependency = (
  id: string,
  stateId: 1 | 2 | 3,
  health: DependencyHealth,
): DependencyDto =>
  ({
    id,
    state: { id: stateId, name: 'State' },
    health: { id: health, name: healthNames[health] },
  }) as DependencyDto

const figure = (title: string) =>
  screen.getByRole('button', { name: new RegExp(`^${title} cross-team`) })

describe('PlanningIntervalDependenciesCard', () => {
  it('counts only open dependencies, and the unhealthy and at-risk ones among them', () => {
    // Arrange
    mockDependencies = [
      dependency('todo-unhealthy', 1, DependencyHealth.Unhealthy),
      dependency('progress-unhealthy', 2, DependencyHealth.Unhealthy),
      dependency('todo-at-risk', 1, DependencyHealth.AtRisk),
      dependency('todo-healthy', 1, DependencyHealth.Healthy),
      dependency('done-unhealthy', 3, DependencyHealth.Unhealthy),
    ]

    // Act
    render(<PlanningIntervalDependenciesCard piKey={7} onOpen={jest.fn()} />)

    // Assert
    expect(figure('Open')).toHaveTextContent('4')
    expect(figure('Unhealthy')).toHaveTextContent('2')
    expect(figure('At-risk')).toHaveTextContent('1')
  })

  it('opens the Dependencies section from a figure', async () => {
    // Arrange
    mockDependencies = []
    const onOpen = jest.fn()
    render(<PlanningIntervalDependenciesCard piKey={7} onOpen={onOpen} />)

    // Act
    await userEvent.click(figure('Unhealthy'))

    // Assert
    expect(onOpen).toHaveBeenCalledTimes(1)
  })
})
