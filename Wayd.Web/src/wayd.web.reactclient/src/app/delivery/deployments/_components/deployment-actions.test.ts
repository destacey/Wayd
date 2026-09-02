import {
  DeploymentDto,
  EnvironmentCategory,
  ProductStatusAlias,
  StatusCategory,
} from '@/src/services/wayd-api'
import { deploymentActionAvailability } from './deployment-actions'

const deployment = (overrides: Partial<DeploymentDto> = {}): DeploymentDto =>
  ({
    id: '11111111-1111-1111-1111-111111111111',
    key: 1,
    release: { id: 'r', key: 2, name: '1.0' },
    environment: { id: 'e', key: 3, name: 'Production' },
    environmentCategory: EnvironmentCategory.Production,
    startedAt: new Date('2026-04-01T10:00:00Z'),
    status: {
      id: 's',
      name: 'In Progress',
      category: StatusCategory.Active,
      alias: 20,
    },
    outcome: ProductStatusAlias.InProgress,
    isComplete: false,
    isChangeFailure: false,
    ...overrides,
  }) as DeploymentDto

const inFlight = () => deployment()

const succeeded = () =>
  deployment({
    completedAt: new Date('2026-04-01T10:30:00Z'),
    status: {
      id: 's',
      name: 'Succeeded',
      category: StatusCategory.Done,
      alias: 21,
    },
    outcome: ProductStatusAlias.Succeeded,
    isComplete: true,
  })

const failed = () =>
  deployment({
    completedAt: new Date('2026-04-01T10:30:00Z'),
    status: {
      id: 's',
      name: 'Failed',
      category: StatusCategory.Removed,
      alias: 22,
    },
    outcome: ProductStatusAlias.Failed,
    isComplete: true,
    isChangeFailure: true,
  })

const rolledBack = () =>
  deployment({
    completedAt: new Date('2026-04-01T10:30:00Z'),
    status: {
      id: 's',
      name: 'Rolled Back',
      category: StatusCategory.Removed,
      alias: 23,
    },
    outcome: ProductStatusAlias.RolledBack,
    isComplete: true,
    isChangeFailure: true,
  })

describe('deploymentActionAvailability', () => {
  it('offers both outcomes while a deployment is in flight', () => {
    // Arrange / Act
    const available = deploymentActionAvailability(inFlight())

    // Assert
    expect(available).toEqual({
      canSucceed: true,
      canFail: true,
      // Nothing has reached the environment yet, so there is nothing to take back.
      canRollBack: false,
    })
  })

  it('refuses a second outcome once complete', () => {
    // Arrange / Act
    const available = deploymentActionAvailability(succeeded())

    // Assert
    expect(available.canSucceed).toBe(false)
    expect(available.canFail).toBe(false)
  })

  it('offers roll back only on a succeeded deployment', () => {
    // Arrange / Act / Assert
    expect(deploymentActionAvailability(succeeded()).canRollBack).toBe(true)
    // A failed deployment never reached its environment, so there is nothing to revert.
    expect(deploymentActionAvailability(failed()).canRollBack).toBe(false)
  })

  it('refuses rolling back what is already rolled back', () => {
    // Arrange / Act
    const available = deploymentActionAvailability(rolledBack())

    // Assert
    expect(available.canRollBack).toBe(false)
  })

  it('gates the outcomes on isComplete rather than on the presence of a completion date', () => {
    // Arrange — a record whose completedAt is set but which the server still reports as in flight.
    // The DTO's isComplete is the server's own answer and is what the guard must read: deriving
    // completeness from the date here would let the UI and the aggregate disagree.
    const disagreeing = deployment({
      completedAt: new Date('2026-04-01T10:30:00Z'),
      isComplete: false,
    })

    // Act
    const available = deploymentActionAvailability(disagreeing)

    // Assert
    expect(available.canSucceed).toBe(true)
    expect(available.canFail).toBe(true)
  })
})
