import { renderHook, act } from '@testing-library/react'
import { useMyProjectsTour } from './use-my-projects-tour'
import { useTourCompleted } from '@/src/hooks'

jest.mock('@/src/hooks', () => ({
  useTourCompleted: jest.fn(),
}))

const mockUseTourCompleted = useTourCompleted as jest.Mock
const mockMarkCompleted = jest.fn()
const mockResetTour = jest.fn()

beforeEach(() => {
  jest.clearAllMocks()
  mockUseTourCompleted.mockReturnValue({
    isCompleted: false,
    isLoading: false,
    markCompleted: mockMarkCompleted,
    resetTour: mockResetTour,
  })
})

describe('useMyProjectsTour', () => {
  it('uses the myProjectsDashboard tour key', () => {
    renderHook(() => useMyProjectsTour())

    expect(mockUseTourCompleted).toHaveBeenCalledWith('myProjectsDashboard')
  })

  it('returns tourOpen true when not completed and not loading', () => {
    const { result } = renderHook(() => useMyProjectsTour())

    expect(result.current.tourOpen).toBe(true)
  })

  it('returns tourOpen false when tour is completed', () => {
    mockUseTourCompleted.mockReturnValue({
      isCompleted: true,
      isLoading: false,
      markCompleted: mockMarkCompleted,
      resetTour: mockResetTour,
    })

    const { result } = renderHook(() => useMyProjectsTour())

    expect(result.current.tourOpen).toBe(false)
  })

  it('returns tourOpen false while loading', () => {
    mockUseTourCompleted.mockReturnValue({
      isCompleted: false,
      isLoading: true,
      markCompleted: mockMarkCompleted,
      resetTour: mockResetTour,
    })

    const { result } = renderHook(() => useMyProjectsTour())

    expect(result.current.tourOpen).toBe(false)
  })

  it('has 5 tour steps', () => {
    const { result } = renderHook(() => useMyProjectsTour())

    expect(result.current.tourSteps).toHaveLength(5)
  })

  it('has correct step titles in order', () => {
    const { result } = renderHook(() => useMyProjectsTour())

    const titles = result.current.tourSteps!.map((s) => s.title)
    expect(titles).toEqual([
      'Welcome to My Projects',
      'Filter Your Projects',
      'Summary Metrics',
      'Project List',
      'Project Details',
    ])
  })

  it('first step has no target (centered modal)', () => {
    const { result } = renderHook(() => useMyProjectsTour())

    expect(result.current.tourSteps![0].target).toBeNull()
  })

  it('sets detailStepIndex to 4', () => {
    const { result } = renderHook(() => useMyProjectsTour())

    expect(result.current.detailStepIndex).toBe(4)
  })

  it('provides all four refs', () => {
    const { result } = renderHook(() => useMyProjectsTour())

    expect(result.current.refs.filterBarRef).toBeDefined()
    expect(result.current.refs.summaryBarRef).toBeDefined()
    expect(result.current.refs.leftPanelRef).toBeDefined()
    expect(result.current.refs.rightPanelRef).toBeDefined()
  })

  it('onTourClose calls markCompleted', () => {
    const { result } = renderHook(() => useMyProjectsTour())

    act(() => {
      result.current.onTourClose()
    })

    expect(mockMarkCompleted).toHaveBeenCalled()
  })

  it('onTourStart calls resetTour', () => {
    const { result } = renderHook(() => useMyProjectsTour())

    act(() => {
      result.current.onTourStart()
    })

    expect(mockResetTour).toHaveBeenCalled()
  })
})
