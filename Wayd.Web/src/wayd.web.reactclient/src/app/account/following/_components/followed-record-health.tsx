'use client'

import ProjectHealthCheckTag from '@/src/app/ppm/projects/_components/project-health-check-tag'
import PiObjectiveHealthCheckTag from '@/src/app/planning/planning-intervals/_components/pi-objective-health-check-tag'
import type { HealthCheckVariant } from '@/src/components/common/health-check/health-check-tag'
import { FollowedRecordDto } from '@/src/services/wayd-api'
import { FollowOwnerTypes } from '@/src/store/features/common/follows-api'

export interface FollowedRecordHealthProps {
  record: FollowedRecordDto
  /** `flag` for a list row, `tag` for the record's detail. */
  variant: HealthCheckVariant
}

/**
 * A followed record's current health check, as the record's own lists show it:
 * hovering opens who reported it, when it expires and the note, loaded on
 * first hover through the record's own health-check endpoint. Shows nothing
 * when the record has no current health check.
 */
const FollowedRecordHealth = ({
  record,
  variant,
}: FollowedRecordHealthProps) => {
  if (!record.healthCheck) return null

  switch (record.ownerType) {
    case FollowOwnerTypes.Project:
      return (
        <ProjectHealthCheckTag
          healthCheck={record.healthCheck}
          projectId={record.id}
          variant={variant}
        />
      )
    case FollowOwnerTypes.PlanningIntervalObjective:
      return (
        <PiObjectiveHealthCheckTag
          healthCheck={record.healthCheck}
          planningIntervalId={record.parentId}
          objectiveId={record.id}
          variant={variant}
        />
      )
    default:
      return null
  }
}

export default FollowedRecordHealth
