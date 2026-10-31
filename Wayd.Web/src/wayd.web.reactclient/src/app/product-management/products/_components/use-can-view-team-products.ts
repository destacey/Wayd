'use client'

import useAuth from '@/src/components/contexts/auth'
import { useFeatureFlag } from '@/src/hooks'

/**
 * Whether a team page shows the products the team is linked to.
 *
 * Those are product records: the API serves them only with Product Management turned on and to a holder of
 * `Permissions.Products.View`, so without either the section would only ever show a failed request.
 */
export const useCanViewTeamProducts = (): boolean => {
  const { isEnabled } = useFeatureFlag('product-management')
  const { hasPermissionClaim } = useAuth()

  return isEnabled && hasPermissionClaim('Permissions.Products.View')
}

export default useCanViewTeamProducts
