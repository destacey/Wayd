'use client'

import { useState } from 'react'
import { useGetProjectHealthCheckQuery } from '@/src/store/features/ppm/project-health-checks-api'
import HealthCheckTag, {
  HealthCheckDetailsData,
  HealthCheckStatusTagData,
  HealthCheckVariant,
} from '@/src/components/common/health-check/health-check-tag'

export interface ProjectHealthCheckTagProps {
  healthCheck?: HealthCheckStatusTagData & { id: string }
  projectId?: string
  variant?: HealthCheckVariant
}

const isFullDetails = (
  hc: HealthCheckStatusTagData & { id: string },
): hc is HealthCheckDetailsData & { id: string } =>
  'reportedBy' in hc && hc.reportedBy != null

const ProjectHealthCheckTag = ({
  healthCheck,
  projectId,
  variant,
}: ProjectHealthCheckTagProps) => {
  const [hovered, setHovered] = useState(false)

  const hasFullDetails = !!healthCheck && isFullDetails(healthCheck)
  const canFetchDetails = !hasFullDetails && !!projectId && !!healthCheck?.id

  const { data: fetchedDetails, isLoading } = useGetProjectHealthCheckQuery(
    {
      projectId: projectId ?? '',
      healthCheckId: healthCheck?.id ?? '',
    },
    { skip: !hovered || !canFetchDetails },
  )

  const details = hasFullDetails
    ? healthCheck
    : canFetchDetails
      ? fetchedDetails
      : null

  return (
    <HealthCheckTag
      healthCheck={healthCheck}
      details={details}
      isLoading={canFetchDetails ? isLoading : false}
      variant={variant}
      onOpenChange={setHovered}
    />
  )
}

export default ProjectHealthCheckTag
