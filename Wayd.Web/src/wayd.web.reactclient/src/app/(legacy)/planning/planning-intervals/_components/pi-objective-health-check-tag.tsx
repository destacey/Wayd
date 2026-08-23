'use client'

import { useState } from 'react'
import { useGetObjectiveHealthCheckQuery } from '@/src/store/features/planning/pi-objective-health-checks-api'
import HealthCheckTag, {
  HealthCheckStatusTagData,
} from '@/src/components/common/health-check/health-check-tag'

export interface PiObjectiveHealthCheckTagProps {
  healthCheck?: HealthCheckStatusTagData & { id: string }
  /**
   * Parent context required to resolve the health check via the nested
   * /planning-intervals/{id}/objectives/{objectiveId}/health-checks endpoint.
   * Required when the popover should fetch full health-check details on hover.
   */
  planningIntervalId?: string
  objectiveId?: string
}

const PiObjectiveHealthCheckTag = ({
  healthCheck,
  planningIntervalId,
  objectiveId,
}: PiObjectiveHealthCheckTagProps) => {
  const [hovered, setHovered] = useState(false)
  const canFetchDetails = !!planningIntervalId && !!objectiveId

  // Skip the request until the user actually hovers — avoids hitting the API
  // for every tag rendered in a long list of objectives. RTK Query caches per
  // arg-tuple, so re-hovering the same tag is free.
  const { data: healthCheckData, isLoading } = useGetObjectiveHealthCheckQuery(
    {
      planningIntervalId: planningIntervalId ?? '',
      objectiveId: objectiveId ?? '',
      healthCheckId: healthCheck?.id ?? '',
    },
    { skip: !hovered || !canFetchDetails || !healthCheck },
  )

  return (
    <HealthCheckTag
      healthCheck={healthCheck}
      details={canFetchDetails ? healthCheckData : null}
      isLoading={canFetchDetails ? isLoading : false}
      onOpenChange={setHovered}
    />
  )
}

export default PiObjectiveHealthCheckTag
