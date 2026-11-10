// @vitest-environment jsdom
import type { Mock } from 'vitest'
import { renderHook, act } from '@testing-library/react'
import { useUserPreferences, useTourCompleted } from './use-user-preferences'
import {
  useGetUserPreferencesQuery,
  useUpdateUserPreferencesMutation,
} from '@/src/store/features/user-management/profile-api'

vi.mock('@/src/store/features/user-management/profile-api', () => ({
  useGetUserPreferencesQuery: vi.fn(),
  useUpdateUserPreferencesMutation: vi.fn(),
}))

const mockQuery = useGetUserPreferencesQuery as Mock
const mockMutationHook = useUpdateUserPreferencesMutation as Mock
const mockUpdatePreferences = vi.fn()

beforeEach(() => {
  vi.clearAllMocks()
  mockMutationHook.mockReturnValue([mockUpdatePreferences])
})

describe('useUserPreferences', () => {
  it('returns default preferences when loading', () => {
    mockQuery.mockReturnValue({ data: undefined, isLoading: true })

    const { result } = renderHook(() => useUserPreferences())

    expect(result.current.preferences).toEqual({ tours: {} })
    expect(result.current.isLoading).toBe(true)
  })

  it('returns default preferences when data is undefined', () => {
    mockQuery.mockReturnValue({ data: undefined, isLoading: false })

    const { result } = renderHook(() => useUserPreferences())

    expect(result.current.preferences).toEqual({ tours: {} })
    expect(result.current.isLoading).toBe(false)
  })

  it('returns server preferences when loaded', () => {
    const prefs = { tours: { myProjectsDashboard: true } }
    mockQuery.mockReturnValue({ data: prefs, isLoading: false })

    const { result } = renderHook(() => useUserPreferences())

    expect(result.current.preferences).toEqual(prefs)
  })

  it('calls updatePreferences mutation with updated value', () => {
    mockQuery.mockReturnValue({
      data: { tours: { existingTour: true } },
      isLoading: false,
    })

    const { result } = renderHook(() => useUserPreferences())

    act(() => {
      result.current.setPreferences((prev) => ({
        ...prev,
        tours: { ...prev.tours, newTour: true },
      }))
    })

    expect(mockUpdatePreferences).toHaveBeenCalledWith({
      tours: { existingTour: true, newTour: true },
    })
  })
})

describe('useTourCompleted', () => {
  it('returns isCompleted false when tour key is not present', () => {
    mockQuery.mockReturnValue({ data: { tours: {} }, isLoading: false })

    const { result } = renderHook(() => useTourCompleted('myProjectsDashboard'))

    expect(result.current.isCompleted).toBe(false)
    expect(result.current.isLoading).toBe(false)
  })

  it('returns isCompleted true when tour key is true', () => {
    mockQuery.mockReturnValue({
      data: { tours: { myProjectsDashboard: true } },
      isLoading: false,
    })

    const { result } = renderHook(() => useTourCompleted('myProjectsDashboard'))

    expect(result.current.isCompleted).toBe(true)
  })

  it('returns isCompleted false when tour key is false', () => {
    mockQuery.mockReturnValue({
      data: { tours: { myProjectsDashboard: false } },
      isLoading: false,
    })

    const { result } = renderHook(() => useTourCompleted('myProjectsDashboard'))

    expect(result.current.isCompleted).toBe(false)
  })

  it('returns isLoading true when preferences are loading', () => {
    mockQuery.mockReturnValue({ data: undefined, isLoading: true })

    const { result } = renderHook(() => useTourCompleted('myProjectsDashboard'))

    expect(result.current.isLoading).toBe(true)
    expect(result.current.isCompleted).toBe(false)
  })

  it('markCompleted sets the tour key to true', () => {
    mockQuery.mockReturnValue({
      data: { tours: { otherTour: true } },
      isLoading: false,
    })

    const { result } = renderHook(() => useTourCompleted('myProjectsDashboard'))

    act(() => {
      result.current.markCompleted()
    })

    expect(mockUpdatePreferences).toHaveBeenCalledWith({
      tours: { otherTour: true, myProjectsDashboard: true },
    })
  })

  it('resetTour sets the tour key to false', () => {
    mockQuery.mockReturnValue({
      data: { tours: { myProjectsDashboard: true } },
      isLoading: false,
    })

    const { result } = renderHook(() => useTourCompleted('myProjectsDashboard'))

    act(() => {
      result.current.resetTour()
    })

    expect(mockUpdatePreferences).toHaveBeenCalledWith({
      tours: { myProjectsDashboard: false },
    })
  })

  it('preserves other tour keys when marking completed', () => {
    mockQuery.mockReturnValue({
      data: { tours: { tourA: true, tourB: false } },
      isLoading: false,
    })

    const { result } = renderHook(() => useTourCompleted('tourC'))

    act(() => {
      result.current.markCompleted()
    })

    expect(mockUpdatePreferences).toHaveBeenCalledWith({
      tours: { tourA: true, tourB: false, tourC: true },
    })
  })
})
