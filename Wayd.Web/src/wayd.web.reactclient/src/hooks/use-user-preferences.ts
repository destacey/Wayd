'use client'

import { UserPreferencesDto } from '@/src/services/api/wayd.schemas'
import {
  useGetUserPreferencesQuery,
  useUpdateUserPreferencesMutation,
} from '@/src/store/features/user-management/profile-api'

const DEFAULT_PREFERENCES: UserPreferencesDto = { tours: {} }

export const useUserPreferences = () => {
  const { data: preferences, isLoading } = useGetUserPreferencesQuery()
  const [updatePreferences] = useUpdateUserPreferencesMutation()

  const current = preferences ?? DEFAULT_PREFERENCES

  const setPreferences = (updater: (prev: UserPreferencesDto) => UserPreferencesDto) => {
    const updated = updater(current)
    updatePreferences(updated)
  }

  return { preferences: current, isLoading, setPreferences }
}

export const useTourCompleted = (tourKey: string) => {
  const { preferences, isLoading, setPreferences } = useUserPreferences()

  const isCompleted = preferences.tours[tourKey] ?? false

  const markCompleted = () => {
    setPreferences((prev) => ({
      ...prev,
      tours: { ...prev.tours, [tourKey]: true },
    }))
  }

  const resetTour = () => {
    setPreferences((prev) => ({
      ...prev,
      tours: { ...prev.tours, [tourKey]: false },
    }))
  }

  return { isCompleted, isLoading, markCompleted, resetTour }
}
