'use client'

import { useGetStrategicInitiativeStatusOptionsQuery } from '@/src/store/features/ppm/strategic-initiatives-api'
import { useGetPortfolioOptionsQuery } from '@/src/store/features/ppm/portfolios-api'
import { FC } from 'react'
import PpmFilterBar from './ppm-filter-bar'

export interface StrategicInitiativesFilterBarProps {
  selectedStatuses: number[]
  onStatusChange: (statuses: number[]) => void
  selectedPortfolioId?: string | null
  onPortfolioChange?: (portfolioId: string | null) => void
  showPortfolioFilter?: boolean
}

const StrategicInitiativesFilterBar: FC<
  StrategicInitiativesFilterBarProps
> = ({
  selectedStatuses,
  onStatusChange,
  selectedPortfolioId,
  onPortfolioChange,
  showPortfolioFilter = true,
}) => {
  const { data: statusOptions, isLoading: statusOptionsLoading } =
    useGetStrategicInitiativeStatusOptionsQuery()

  const { data: portfolioOptions, isLoading: portfolioOptionsLoading } =
    useGetPortfolioOptionsQuery(undefined, { skip: !showPortfolioFilter })

  const loading =
    statusOptionsLoading || (showPortfolioFilter && portfolioOptionsLoading)

  return (
    <PpmFilterBar
      statusOptions={statusOptions}
      selectedStatuses={selectedStatuses}
      onStatusChange={onStatusChange}
      portfolioOptions={showPortfolioFilter ? portfolioOptions : undefined}
      selectedPortfolioId={showPortfolioFilter ? selectedPortfolioId : undefined}
      onPortfolioChange={showPortfolioFilter ? onPortfolioChange : undefined}
      loading={loading}
    />
  )
}

export default StrategicInitiativesFilterBar
