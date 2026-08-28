import { render, screen } from '@testing-library/react'
import { ProjectLifecycleDetailsDto } from '@/src/services/wayd-api'
import ProjectLifecycleFacts from './project-lifecycle-facts'

const lifecycle = (
  overrides: Partial<ProjectLifecycleDetailsDto> = {},
): ProjectLifecycleDetailsDto =>
  ({
    id: 'd9c4f200-0000-0000-0000-000000000001',
    key: 3,
    name: 'Standard Delivery',
    description: 'The default lifecycle for delivery projects',
    state: { id: 2, name: 'Active' },
    stages: [],
    ...overrides,
  }) as ProjectLifecycleDetailsDto

/** The value rendered under a given label. */
const valueFor = (label: string) =>
  screen.getByText(label).parentElement?.textContent?.replace(label, '')

describe('ProjectLifecycleFacts', () => {
  it('renders the record facts', () => {
    // Arrange / Act
    render(<ProjectLifecycleFacts lifecycle={lifecycle()} />)

    // Assert
    expect(valueFor('Key')).toBe('3')
    expect(valueFor('State')).toBe('Active')
    expect(valueFor('Description')).toBe(
      'The default lifecycle for delivery projects',
    )
  })

  it('omits an empty description', () => {
    // Arrange / Act
    render(<ProjectLifecycleFacts lifecycle={lifecycle({ description: '' })} />)

    // Assert
    expect(screen.queryByText('Description')).not.toBeInTheDocument()
  })

  it('tolerates a lifecycle with no state', () => {
    // Arrange / Act — the old details tab guarded state, so the data has
    // evidently allowed it.
    render(
      <ProjectLifecycleFacts
        lifecycle={lifecycle({ state: undefined as any })}
      />,
    )

    // Assert
    expect(screen.getByText('State')).toBeInTheDocument()
  })
})
