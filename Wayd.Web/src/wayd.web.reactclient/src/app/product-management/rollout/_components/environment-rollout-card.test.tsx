import {
  EnvironmentCategory,
  EnvironmentRolloutDto,
  RolloutItemDto,
} from '@/src/services/wayd-api'
import { render, screen } from '@testing-library/react'
import EnvironmentRolloutCard from './environment-rollout-card'

const item = (overrides: Partial<RolloutItemDto> = {}): RolloutItemDto =>
  ({
    deploymentId: 'deployment-1',
    deploymentKey: 12,
    product: { id: 'product-1', key: 3, name: 'Trio VMS' },
    version: { id: 'version-1', key: 8, name: '2026.10' },
    versionLabel: '2026.10',
    hasFailedAttemptSince: false,
    ...overrides,
  }) as RolloutItemDto

const environment = (
  overrides: Partial<EnvironmentRolloutDto> = {},
): EnvironmentRolloutDto =>
  ({
    id: 'environment-1',
    key: 2,
    name: 'Prod',
    category: EnvironmentCategory.Production,
    ringOrder: 3,
    isActive: true,
    running: [item()],
    ...overrides,
  }) as EnvironmentRolloutDto

describe('EnvironmentRolloutCard', () => {
  it('names the product and the version running in it', () => {
    // Arrange / Act
    render(<EnvironmentRolloutCard environment={environment()} />)

    // Assert
    expect(screen.getByText('Trio VMS')).toBeInTheDocument()
    expect(screen.getByText('2026.10')).toBeInTheDocument()
  })

  it('says nothing is running rather than rendering an empty card', () => {
    // Arrange — an environment nothing has ever succeeded into is a real answer, not missing data.
    // Act
    render(<EnvironmentRolloutCard environment={environment({ running: [] })} />)

    // Assert
    expect(screen.getByText('Nothing running')).toBeInTheDocument()
  })

  it('warns when a later attempt failed, without changing what is reported as running', () => {
    // Arrange — the running version is still the honest answer, but on its own it hides that
    // someone has since tried to move past it and could not.
    const stalled = environment({
      running: [item({ versionLabel: '1.0.6', hasFailedAttemptSince: true })],
    })

    // Act
    render(<EnvironmentRolloutCard environment={stalled} />)

    // Assert
    expect(screen.getByText('1.0.6')).toBeInTheDocument()
    expect(screen.getByLabelText('A later attempt failed')).toBeInTheDocument()
  })

  it('does not warn when nothing has failed since', () => {
    // Arrange / Act
    render(<EnvironmentRolloutCard environment={environment()} />)

    // Assert
    expect(screen.queryByLabelText('A later attempt failed')).not.toBeInTheDocument()
  })

  it('links a running entry to the deployment it was read from', () => {
    // Arrange — the card states a conclusion, so it has to offer the record behind it.
    // Act
    render(<EnvironmentRolloutCard environment={environment()} />)

    // Assert
    expect(screen.getByRole('link', { name: '2026.10' })).toHaveAttribute(
      'href',
      '/product-management/deployments/12',
    )
  })

  it('names the product, not the package, for a component that shipped inside one', () => {
    // Arrange — a package is how the version arrived, not a second thing running alongside the
    // products it carries. Listing the package as its own row left every bundle ever deployed
    // reported as live, because successive bundles never supersede one another by id.
    const packaged = environment({
      running: [
        item({
          product: { id: 'product-9', key: 9, name: 'Catalog API' },
          version: undefined,
          versionLabel: '4.1.0',
          package: { id: 'package-1', key: 5, name: 'OFT-2026.07' },
        }),
      ],
    })

    // Act
    render(<EnvironmentRolloutCard environment={packaged} />)

    // Assert — the product leads, the version is the component's, the package is provenance.
    expect(screen.getByText('Catalog API')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: '4.1.0' })).toBeInTheDocument()
    expect(screen.getByText('in OFT-2026.07')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'OFT-2026.07' })).not.toBeInTheDocument()
  })

  it('marks a retired environment, so an empty card is not read as a gap', () => {
    // Arrange / Act
    render(
      <EnvironmentRolloutCard
        environment={environment({ isActive: false, running: [] })}
      />,
    )

    // Assert
    expect(screen.getByText('Retired')).toBeInTheDocument()
  })
})
