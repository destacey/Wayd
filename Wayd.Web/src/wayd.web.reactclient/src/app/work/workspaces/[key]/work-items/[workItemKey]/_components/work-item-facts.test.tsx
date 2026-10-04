import { render, screen } from '@testing-library/react'
import { WorkTypeTier } from '@/src/components/types'
import { WorkItemDetailsDto } from '@/src/services/wayd-api'
import WorkItemFacts from './work-item-facts'

// The planning barrel pulls in forms whose module-level date math breaks under the global dayjs
// stub; these tests render no sprint.
jest.mock('@/src/components/common/planning', () => ({
  SprintLink: () => null,
}))

const workItem = (
  estimates: Partial<
    Pick<WorkItemDetailsDto, 'storyPoints' | 'effort' | 'size'>
  >,
): WorkItemDetailsDto =>
  ({
    id: 'w-1',
    key: 'ID-1',
    title: 'Sample work item',
    type: {
      name: 'Feature',
      tier: { id: WorkTypeTier.Portfolio, name: 'Portfolio' },
    },
    tags: [],
    created: new Date('2026-10-01T12:00:00Z'),
    lastModified: new Date('2026-10-02T12:00:00Z'),
    ...estimates,
  }) as unknown as WorkItemDetailsDto

describe('WorkItemFacts', () => {
  it('shows each estimate that has a value, including zero', () => {
    // Arrange
    const item = workItem({ storyPoints: 3, effort: 8, size: 0 })

    // Act
    render(<WorkItemFacts workItem={item} />)

    // Assert
    expect(screen.getByText('Story Points')).toBeInTheDocument()
    expect(screen.getByText('3')).toBeInTheDocument()
    expect(screen.getByText('Effort')).toBeInTheDocument()
    expect(screen.getByText('8')).toBeInTheDocument()
    expect(screen.getByText('Size')).toBeInTheDocument()
    expect(screen.getByText('0')).toBeInTheDocument()
  })

  it('omits estimates that have no value', () => {
    // Arrange
    const item = workItem({ effort: 13 })

    // Act
    render(<WorkItemFacts workItem={item} />)

    // Assert
    expect(screen.getByText('Effort')).toBeInTheDocument()
    expect(screen.queryByText('Story Points')).not.toBeInTheDocument()
    expect(screen.queryByText('Size')).not.toBeInTheDocument()
  })
})
