'use client'

import { useGetStateOptionsQuery } from '@/src/store/features/strategic-management/strategic-themes-api'
import { StrategicThemeState } from '@/src/services/wayd-api'
import { FC } from 'react'
import { PpmFilterBar } from '@/src/app/ppm/_components'

export interface StrategicThemesFilterBarProps {
  selectedStates: StrategicThemeState[]
  onStateChange: (states: StrategicThemeState[]) => void
}

const StrategicThemesFilterBar: FC<StrategicThemesFilterBarProps> = ({
  selectedStates,
  onStateChange,
}) => {
  const { data: stateOptions, isLoading } = useGetStateOptionsQuery()

  return (
    <PpmFilterBar
      statusOptions={stateOptions}
      selectedStatuses={selectedStates}
      onStatusChange={onStateChange}
      loading={isLoading}
    />
  )
}

export default StrategicThemesFilterBar
