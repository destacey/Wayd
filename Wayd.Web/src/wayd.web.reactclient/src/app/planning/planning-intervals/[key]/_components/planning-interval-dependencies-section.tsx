'use client'

import { DependencyWheelExplorer } from '@/src/components/common/dependency-wheel'
import { DependencyDto } from '@/src/services/wayd-api'
import { useGetPlanningIntervalDependenciesQuery } from '@/src/store/features/planning/planning-interval-api'
import { Segmented } from 'antd'
import { useState } from 'react'

export type DependencyStateFilter = 'open' | 'all'

// DependencyState ids: To Do and In Progress are open; Done is not.
const OPEN_STATE_IDS = [1, 2]

/** Whether a dependency passes the open/all filter: open means its predecessor is not done. */
export const matchesStateFilter = (
  dependency: DependencyDto,
  filter: DependencyStateFilter,
) => filter === 'all' || OPEN_STATE_IDS.includes(dependency.state.id)

export interface PlanningIntervalDependenciesSectionProps {
  planningIntervalKey: number
}

/**
 * The PI's dependencies that cross team lines: a wheel of where they run between teams, over the grid of
 * them, open ones only or all of them.
 */
const PlanningIntervalDependenciesSection = ({
  planningIntervalKey,
}: PlanningIntervalDependenciesSectionProps) => {
  const [stateFilter, setStateFilter] = useState<DependencyStateFilter>('open')

  const { data, isLoading, refetch } =
    useGetPlanningIntervalDependenciesQuery(planningIntervalKey)

  const inState = (data ?? []).filter((d) => matchesStateFilter(d, stateFilter))

  return (
    <DependencyWheelExplorer
      // A new set of dependencies starts with nothing picked on the wheel.
      key={stateFilter}
      dependencies={inState}
      isLoading={isLoading}
      refetch={refetch}
      gridKey="planning-interval-dependencies"
      exportFileName={`planning-interval-${planningIntervalKey}-dependencies`}
      emptyMessage={
        stateFilter === 'open'
          ? 'No open dependencies cross team lines in this PI.'
          : 'No dependencies cross team lines in this PI.'
      }
      extra={
        <Segmented<DependencyStateFilter>
          size="small"
          value={stateFilter}
          onChange={setStateFilter}
          options={[
            { label: 'Open', value: 'open' },
            { label: 'All', value: 'all' },
          ]}
          aria-label="Dependency state"
        />
      }
    />
  )
}

export default PlanningIntervalDependenciesSection
