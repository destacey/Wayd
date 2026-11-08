'use client'

import useAuth from '@/src/components/contexts/auth'
import { useFeatureFlags, useLinkedEmployee } from '@/src/hooks'
import { HomeWidgets } from './home-widgets'
import { arrangeWidgets, attentionTiles } from './widget-registry'

/**
 * The home page's layout for the signed-in account: the widgets that apply to
 * it in their columns, and the tiles they contribute to the attention strip.
 */
export const useHomeLayout = () => {
  const { hasPermissionClaim } = useAuth()
  const { hasLinkedEmployee } = useLinkedEmployee()
  const { isEnabled: isFeatureEnabled } = useFeatureFlags()

  const columns = arrangeWidgets(HomeWidgets, {
    hasPermissionClaim,
    isFeatureEnabled,
    hasLinkedEmployee,
  })

  return { columns, tiles: attentionTiles(columns), hasLinkedEmployee }
}
