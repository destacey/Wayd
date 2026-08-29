'use client'

import { usePathname } from 'next/navigation'
import { useGetClientFeatureFlagsQuery } from '@/src/store/features/feature-flags-api'

interface UseFeatureFlagResult {
  isEnabled: boolean
  isLoading: boolean
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
  const pathname = usePathname()
  const isLoggingOut = pathname === '/logout'

  const { data: features, isLoading } = useGetClientFeatureFlagsQuery(
    undefined,
    {
      pollingInterval: 10 * 60_000, // Refresh every 10 minutes
      skip: isLoggingOut,
    },
  )

  const isEnabled =
    features?.some((f) => f.name === featureName && f.isEnabled) ?? false

  return { isEnabled, isLoading }
}
