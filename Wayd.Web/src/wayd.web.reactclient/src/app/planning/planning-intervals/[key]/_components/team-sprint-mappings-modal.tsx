'use client'

import PlanningIntervalTeamSprintMappings from '@/src/app/planning/planning-intervals/_components/planning-interval-team-sprint-mappings'
import { PlanningIntervalDetailsDto } from '@/src/services/wayd-api'
import { Modal } from 'antd'

export interface TeamSprintMappingsModalProps {
  planningInterval: PlanningIntervalDetailsDto
  onClose: () => void
}

/**
 * Every team's iteration-to-sprint mapping, in one matrix.
 *
 * No footer: the matrix itself saves nothing — each row's pencil opens the
 * per-team form that does. It is wide because a PI's iterations become columns.
 */
const TeamSprintMappingsModal = ({
  planningInterval,
  onClose,
}: TeamSprintMappingsModalProps) => (
  <Modal
    title="Team Sprints"
    open
    width="80vw"
    footer={null}
    onCancel={onClose}
    destroyOnHidden
  >
    <PlanningIntervalTeamSprintMappings planningInterval={planningInterval} />
  </Modal>
)

export default TeamSprintMappingsModal
