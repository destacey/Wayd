'use client'

import { usePathname } from 'next/navigation'
import { useGetClientFeatureFlagsQuery } from '@/src/store/features/feature-flags-api'

interface UseFeatureFlagResult {
  isEnabled: boolean
  isLoading: boolean
}

interface UseFeatureFlagsResult {
  /** Whether the named flag is on; false for an unknown flag or while the flags load. */
  isEnabled: (featureName: string) => boolean
  isLoading: boolean
}

/**
 * Reads every client feature flag at once, for code that checks flags it only
 * knows at runtime — such as a registry where each entry names its own — and so
 * cannot call `useFeatureFlag` once per flag.
 */
export function useFeatureFlags(): UseFeatureFlagsResult {
  const pathname = usePathname()
  const isLoggingOut = pathname === '/logout'

  const { data: features, isLoading } = useGetClientFeatureFlagsQuery(
    undefined,
    {
      pollingInterval: 10 * 60_000, // Refresh every 10 minutes
      skip: isLoggingOut,
    },
  )

  const isEnabled = (featureName: string) =>
    features?.some((f) => f.name === featureName && f.isEnabled) ?? false

  return { isEnabled, isLoading }
}

/**
 * Hook to check if a feature flag is enabled.
 *
 * @param featureName - The kebab-case name of the feature flag (e.g., "new-dashboard-layout")
 * @returns { isEnabled, isLoading } - Whether the feature is enabled and whether flags are still loading.
 *
 * @example
 * const { isEnabled, isLoading } = useFeatureFlag('new-dashboard-layout')
 * if (isLoading) return <Spinner />
 * if (isEnabled) return <NewDashboard />
 * return <LegacyDashboard />
 */
export function useFeatureFlag(featureName: string): UseFeatureFlagResult {
  const { isEnabled, isLoading } = useFeatureFlags()

  return { isEnabled: isEnabled(featureName), isLoading }
}
