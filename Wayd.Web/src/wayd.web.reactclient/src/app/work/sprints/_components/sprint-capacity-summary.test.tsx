import { render, screen } from '@testing-library/react'
import {
  SizingMethod,
  SprintCapacityDto,
  SprintCapacityOutcome,
  SprintCapacityScopeBasis,
} from '@/src/services/wayd-api'
import SprintCapacitySummary, {
  lastSprints,
  overCapacityWarning,
} from './sprint-capacity-summary'

let mockCapacity: SprintCapacityDto | undefined
// What RTK Query still holds from the previous sprint while this one loads.
let mockStaleCapacity: SprintCapacityDto | undefined
let mockIsFetching = false
const mockCapacityArgs = jest.fn()
jest.mock('@/src/store/features/work-management/sprints-api', () => ({
  useGetSprintCapacityQuery: (arg: unknown) => {
    mockCapacityArgs(arg)
    return {
      data: mockCapacity ?? mockStaleCapacity,
      currentData: mockCapacity,
      isFetching: mockIsFetching,
    }
  },
}))

const capacity = (
  overrides: Partial<SprintCapacityDto> = {},
): SprintCapacityDto => ({
  sprintId: 'sprint-1',
  start: '2026-09-14',
  end: '2026-09-25',
  allocatedPersonDays: 12,
  deliveryPersonDays: 10,
  awayPersonDays: 0.5,
  sizingMethod: SizingMethod.StoryPoints,
  scopeBasis: SprintCapacityScopeBasis.Committed,
  scope: 34,
  outcome: SprintCapacityOutcome.Compared,
  expectedScope: 28.4,
  priorSprints: 3,
  isOverCapacity: true,
  ...overrides,
})

describe('overCapacityWarning', () => {
  it('names the scope, what the capacity is expected to deliver and where that comes from', () => {
    // Arrange / Act
    const warning = overCapacityWarning(capacity())

    // Assert
    expect(warning?.title).toBe(
      'Over capacity: 34 story points committed, about 28 expected.',
    )
    expect(warning?.description).toContain("the team's last 3 sprints")
    expect(warning?.description).toContain('10 person-days')
  })

  it('says planned before the commitment point', () => {
    // Arrange / Act
    const warning = overCapacityWarning(
      capacity({ scopeBasis: SprintCapacityScopeBasis.Planned }),
    )

    // Assert
    expect(warning?.title).toContain('34 story points planned')
  })

  it('has no warning when the scope is within capacity', () => {
    // Arrange / Act
    const warning = overCapacityWarning(capacity({ isOverCapacity: false }))

    // Assert
    expect(warning).toBeNull()
  })
})

describe('lastSprints', () => {
  it.each([
    [1, '', 'last sprint'],
    [3, '', 'last 3 sprints'],
    [1, 'completed ', 'last completed sprint'],
    [7, 'completed ', 'last 7 completed sprints'],
  ])('names %i sprint(s) with %p', (count, kind, expected) => {
    // Arrange / Act
    const text = lastSprints(count, kind)

    // Assert
    expect(text).toBe(expected)
  })
})

describe('SprintCapacitySummary', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockCapacity = capacity()
    mockStaleCapacity = undefined
    mockIsFetching = false
  })

  it('warns when the committed scope exceeds capacity', () => {
    // Arrange / Act
    render(<SprintCapacitySummary sprintKey={201} />)

    // Assert
    expect(mockCapacityArgs).toHaveBeenCalledWith(201)
    expect(screen.getByRole('alert')).toHaveTextContent('Over capacity')
    expect(screen.getByText('Committed')).toBeInTheDocument()
    expect(screen.getByText('Expected')).toBeInTheDocument()
  })

  it('does not warn when the capacity covers the scope', () => {
    // Arrange
    mockCapacity = capacity({ isOverCapacity: false, scope: 20 })

    // Act
    render(<SprintCapacitySummary sprintKey={201} />)

    // Assert
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.getByText('Capacity')).toBeInTheDocument()
    expect(screen.getByText('person-days · 0.5 away')).toBeInTheDocument()
  })

  it('says why the scope was not compared, with no expectation', () => {
    // Arrange
    mockCapacity = capacity({
      outcome: SprintCapacityOutcome.NoPriorSprints,
      expectedScope: undefined,
      isOverCapacity: false,
      priorSprints: 0,
    })

    // Act
    render(<SprintCapacitySummary sprintKey={201} />)

    // Assert
    expect(
      screen.getByText(/no completed sprints in this sizing method/),
    ).toBeInTheDocument()
    expect(screen.queryByText('Expected')).not.toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it("never shows the previous sprint's figures while this sprint's load", () => {
    // Arrange
    mockCapacity = undefined
    mockStaleCapacity = capacity()
    mockIsFetching = true

    // Act
    render(<SprintCapacitySummary sprintKey={202} />)

    // Assert
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
    expect(screen.queryByText('Committed')).not.toBeInTheDocument()
    expect(screen.queryByText('Expected')).not.toBeInTheDocument()
  })

  it('shows nothing for a team with no one allocated for the sprint', () => {
    // Arrange
    mockCapacity = capacity({
      allocatedPersonDays: 0,
      deliveryPersonDays: 0,
      awayPersonDays: 0,
      outcome: SprintCapacityOutcome.NoCapacity,
      expectedScope: undefined,
      isOverCapacity: false,
    })

    // Act
    const { container } = render(<SprintCapacitySummary sprintKey={201} />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })

  it('explains a team whose time is only in roles that do not count toward delivery', () => {
    // Arrange
    mockCapacity = capacity({
      allocatedPersonDays: 5,
      deliveryPersonDays: 0,
      outcome: SprintCapacityOutcome.NoCapacity,
      expectedScope: undefined,
      isOverCapacity: false,
    })

    // Act
    render(<SprintCapacitySummary sprintKey={201} />)

    // Assert
    expect(
      screen.getByText(/in a role that counts toward delivery/),
    ).toBeInTheDocument()
  })

  it('shows nothing when the capacity cannot be read', () => {
    // Arrange
    mockCapacity = undefined

    // Act
    const { container } = render(<SprintCapacitySummary sprintKey={201} />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})
