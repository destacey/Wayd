import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import MyRoadmapsWidget, {
  MY_ROADMAPS_LIMIT,
  sortRoadmaps,
} from './my-roadmaps-widget'
import { RoadmapListDto, RoadmapState } from '@/src/services/wayd-api'

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('@/src/store/features/planning/roadmaps-api', () => ({
  useGetRoadmapsQuery: vi.fn(),
}))

vi.mock(import('@/src/utils'), async (importOriginal) => ({
  ...(await importOriginal()),
  todayCalendarDate: () => '2026-11-11',
}))

import { useGetRoadmapsQuery } from '@/src/store/features/planning/roadmaps-api'

const mockRoadmaps = useGetRoadmapsQuery as Mock

const roadmap = (
  key: number,
  name: string,
  start: string,
  end: string,
): RoadmapListDto => ({
  id: `r-${key}`,
  key,
  name,
  start,
  end,
  visibility: { id: 1, name: 'Public' },
  state: { id: 1, name: 'Active' },
  owners: [],
  canManage: true,
})

const loaded = (data: RoadmapListDto[]) => ({
  data,
  isLoading: false,
  isError: false,
})

beforeEach(() => {
  vi.clearAllMocks()
})

describe('sortRoadmaps', () => {
  it('orders by start date, ties by name ignoring case', () => {
    // Arrange
    const roadmaps = [
      roadmap(1, 'Later', '2026-06-01', '2026-12-31'),
      roadmap(2, 'beta', '2026-01-01', '2026-12-31'),
      roadmap(3, 'Alpha', '2026-01-01', '2026-12-31'),
    ]

    // Act
    const result = sortRoadmaps(roadmaps)

    // Assert
    expect(result.map((r) => r.name)).toEqual(['Alpha', 'beta', 'Later'])
  })
})

describe('MyRoadmapsWidget', () => {
  it('asks for the active roadmaps the caller owns', () => {
    // Arrange
    mockRoadmaps.mockReturnValue(loaded([]))

    // Act
    render(<MyRoadmapsWidget title="My roadmaps" />)

    // Assert
    expect(mockRoadmaps).toHaveBeenCalledWith({
      state: [RoadmapState.Active],
      ownedByMe: true,
    })
  })

  it('links each roadmap and flags one past its end date', () => {
    // Arrange
    mockRoadmaps.mockReturnValue(
      loaded([
        roadmap(7, 'Platform 2026', '2026-01-01', '2026-12-31'),
        roadmap(8, 'Mobile 2026', '2026-01-01', '2026-10-31'),
      ]),
    )

    // Act
    render(<MyRoadmapsWidget title="My roadmaps" />)

    // Assert
    expect(screen.getByRole('link', { name: 'Platform 2026' })).toHaveAttribute(
      'href',
      '/planning/roadmaps/7',
    )
    expect(screen.getAllByText(/past end date/)).toHaveLength(1)
  })

  it('shows each roadmap’s visibility', () => {
    // Arrange
    mockRoadmaps.mockReturnValue(
      loaded([roadmap(7, 'Platform 2026', '2026-01-01', '2026-12-31')]),
    )

    // Act
    render(<MyRoadmapsWidget title="My roadmaps" />)

    // Assert
    expect(screen.getByText('Public')).toBeInTheDocument()
  })

  it('lists the earliest-starting roadmaps up to the limit and links to the rest', () => {
    // Arrange — given latest start first, so a cut before sorting would keep the wrong ones.
    const many = Array.from({ length: MY_ROADMAPS_LIMIT + 2 }, (_, i) =>
      roadmap(
        i + 1,
        `Roadmap ${i + 1}`,
        `2026-0${MY_ROADMAPS_LIMIT + 2 - i}-01`,
        '2026-12-31',
      ),
    )
    mockRoadmaps.mockReturnValue(loaded(many))

    // Act
    render(<MyRoadmapsWidget title="My roadmaps" />)

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(MY_ROADMAPS_LIMIT)
    expect(screen.queryByRole('link', { name: 'Roadmap 1' })).toBeNull()
    expect(screen.queryByRole('link', { name: 'Roadmap 2' })).toBeNull()
    expect(screen.getByRole('link', { name: 'Roadmap 7' })).toBeInTheDocument()
    expect(screen.getByText('And 2 more.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'View all' })).toHaveAttribute(
      'href',
      '/planning/roadmaps',
    )
  })

  it('says how it fills when the caller owns no active roadmaps', () => {
    // Arrange
    mockRoadmaps.mockReturnValue(loaded([]))

    // Act
    render(<MyRoadmapsWidget title="My roadmaps" />)

    // Assert
    expect(screen.getByText(/You own no active roadmaps/)).toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'View all' })).toHaveAttribute(
      'href',
      '/planning/roadmaps',
    )
  })

  it('says when the roadmaps could not load', () => {
    // Arrange
    mockRoadmaps.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    // Act
    render(<MyRoadmapsWidget title="My roadmaps" />)

    // Assert
    expect(
      screen.getByText('Could not load your roadmaps.'),
    ).toBeInTheDocument()
  })
})
