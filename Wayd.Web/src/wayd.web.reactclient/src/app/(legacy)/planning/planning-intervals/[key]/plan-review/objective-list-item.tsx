'use client'

import { PlanningIntervalObjectiveListDto } from '@/src/services/wayd-api'
import { HolderOutlined } from '@ant-design/icons'
import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import PlanningIntervalObjectiveCard from '../_components/planning-interval-objective-card'

export interface ObjectiveListItemProps {
  objective: PlanningIntervalObjectiveListDto
  piKey: number
  canUpdateObjectives: boolean
  canCreateHealthChecks: boolean
  refreshObjectives: () => void
  onObjectiveClick: (objectiveKey: number) => void
}

const ObjectiveListItem = ({
  objective,
  piKey,
  canUpdateObjectives,
  canCreateHealthChecks,
  refreshObjectives,
  onObjectiveClick,
}: ObjectiveListItemProps) => {
  const { attributes, listeners, setNodeRef, transform, transition } =
    useSortable({ id: objective.id })

  const sortableStyle = {
    transition: transition,
    transform: CSS.Transform.toString(transform),
    touchAction: 'none',
    marginBottom: 4,
  }

  return (
    <PlanningIntervalObjectiveCard
      objective={objective}
      piKey={piKey}
      canUpdateObjectives={canUpdateObjectives}
      canCreateHealthChecks={canCreateHealthChecks}
      refreshObjectives={refreshObjectives}
      onObjectiveClick={onObjectiveClick}
      leading={
        canUpdateObjectives ? (
          <HolderOutlined
            {...listeners}
            rotate={90}
            style={{ marginRight: 12 }}
          />
        ) : null
      }
      cardRef={setNodeRef}
      cardAttributes={attributes}
      cardStyle={sortableStyle}
      showTeamTag={false}
    />
  )
}

export default ObjectiveListItem
