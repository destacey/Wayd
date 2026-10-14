'use client'

import { Alert } from 'antd'
import { useFeatureFlag } from '@/src/hooks'

/**
 * Warns, in the dialog that deactivates a team or team of teams, that doing so
 * ends people's allocations to it. The feature flag decides only whether the
 * warning shows: with Team Capacity Management off nobody can see allocations,
 * but any made while it was on are still ended.
 */
const DeactivateTeamAllocationsAlert = () => {
  const { isEnabled: capacityEnabled } = useFeatureFlag(
    'team-capacity-management',
  )

  if (!capacityEnabled) return null

  return (
    <Alert
      type="warning"
      showIcon
      title="People's allocations to this team end on the inactive date."
      description="Any that start later are removed. Reactivating the team does not bring them back."
    />
  )
}

export default DeactivateTeamAllocationsAlert
