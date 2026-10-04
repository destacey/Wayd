'use client'

import { useGetRoadmapStateOptionsQuery } from '@/src/store/features/planning/roadmaps-api'
import { RoadmapState } from '@/src/services/wayd-api'
import { FC } from 'react'
import { PpmFilterBar } from '@/src/app/ppm/_components'

export interface RoadmapsFilterBarProps {
  selectedStates: RoadmapState[]
  onStateChange: (states: RoadmapState[]) => void
}

const RoadmapsFilterBar: FC<RoadmapsFilterBarProps> = ({
  selectedStates,
  onStateChange,
}) => {
  const { data: stateOptions, isLoading } = useGetRoadmapStateOptionsQuery()

  return (
    <PpmFilterBar
      statusOptions={stateOptions}
      selectedStatuses={selectedStates}
      onStatusChange={onStateChange}
      loading={isLoading}
    />
  )
}

export default RoadmapsFilterBar
