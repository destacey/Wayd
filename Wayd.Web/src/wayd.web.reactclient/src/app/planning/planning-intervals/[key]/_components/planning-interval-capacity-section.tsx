'use client'

import { useState } from 'react'
import Link from 'next/link'
import { Alert, Button, Drawer, Table, Tag, Tooltip, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import useAuth from '@/src/components/contexts/auth'
import {
  PlanningIntervalCapacityFiguresDto,
  PlanningIntervalTeamCapacityDto,
} from '@/src/services/wayd-api'
import { useGetPlanningIntervalCapacityQuery } from '@/src/store/features/planning/planning-interval-api'
import { formatCalendarDate, isApiError } from '@/src/utils'
import { formatPersonDays } from '@/src/app/organizations/_components/team-capacity/team-capacity-data'
import TeamCapacityPlan from '@/src/app/organizations/_components/capacity-plan/team-capacity-plan'
import type { TeamTypeName } from '@/src/app/organizations/types'

const { Text } = Typography

const TEAM_OF_TEAMS: TeamTypeName = 'Team of Teams'

export interface PlanningIntervalCapacitySectionProps {
  planningIntervalKey: number
}

/** A team's page, by its type. */
export const teamHref = (team: PlanningIntervalTeamCapacityDto['team']) =>
  team.type === TEAM_OF_TEAMS
    ? `/organizations/team-of-teams/${team.key}`
    : `/organizations/teams/${team.key}`

/**
 * What a cell shows: the delivery person-days, or a dash for a team with no
 * one allocated, which has nothing to plan with rather than no time.
 */
export const capacityText = (
  row: PlanningIntervalTeamCapacityDto,
  figures: PlanningIntervalCapacityFiguresDto,
): string =>
  row.hasAllocations ? formatPersonDays(figures.deliveryPersonDays) : '—'

const figureDetails = (figures: PlanningIntervalCapacityFiguresDto) => (
  <>
    <div>{`${formatPersonDays(figures.deliveryPersonDays)} delivery person-days`}</div>
    <div>{`${formatPersonDays(figures.allocatedPersonDays)} allocated in every role`}</div>
    {figures.awayPersonDays > 0 && (
      <div>{`${formatPersonDays(figures.awayPersonDays)} taken by time away`}</div>
    )}
  </>
)

/** A figure with its breakdown on hover, or a dash for a team with no one allocated. */
const CapacityCell = ({
  row,
  figures,
  strong,
}: {
  row: PlanningIntervalTeamCapacityDto
  figures: PlanningIntervalCapacityFiguresDto
  strong?: boolean
}) =>
  row.hasAllocations ? (
    <Tooltip title={figureDetails(figures)}>
      <Text strong={strong}>{capacityText(row, figures)}</Text>
    </Tooltip>
  ) : (
    <Text type="secondary">{capacityText(row, figures)}</Text>
  )

/**
 * Each team's capacity in a planning interval, for the whole planning
 * interval and each of its iterations by the planning interval's own dates.
 * A team's plan opens over the planning interval's dates.
 */
const PlanningIntervalCapacitySection = ({
  planningIntervalKey,
}: PlanningIntervalCapacitySectionProps) => {
  const { hasPermissionClaim } = useAuth()
  const canViewTeams = hasPermissionClaim('Permissions.Teams.View')
  const [planFor, setPlanFor] =
    useState<PlanningIntervalTeamCapacityDto | null>(null)

  const {
    data: capacity,
    isFetching,
    error,
  } = useGetPlanningIntervalCapacityQuery(planningIntervalKey)

  if (error) {
    return (
      <Alert
        type="error"
        showIcon
        title="The planning interval's capacity could not be loaded."
        description={isApiError(error) ? error.detail : undefined}
      />
    )
  }

  const iterationColumns: ColumnsType<PlanningIntervalTeamCapacityDto> = (
    capacity?.iterations ?? []
  ).map((iteration, index) => ({
    key: iteration.id,
    title: (
      <Tooltip
        title={`${formatCalendarDate(iteration.start)} – ${formatCalendarDate(iteration.end)}`}
      >
        <span>
          {iteration.name}
          {iteration.category.name !== 'Development' && (
            <Tag style={{ marginInlineStart: 4 }}>
              {iteration.category.name}
            </Tag>
          )}
        </span>
      </Tooltip>
    ),
    align: 'right' as const,
    render: (_: unknown, row: PlanningIntervalTeamCapacityDto) => {
      const figures = row.iterations[index]
      return <CapacityCell row={row} figures={figures} />
    },
  }))

  const columns: ColumnsType<PlanningIntervalTeamCapacityDto> = [
    {
      key: 'team',
      title: 'Team',
      fixed: 'left',
      render: (_, row) => (
        <Link href={teamHref(row.team)}>{row.team.name}</Link>
      ),
    },
    {
      key: 'type',
      title: 'Type',
      render: (_, row) => row.team.type,
    },
    ...iterationColumns,
    {
      key: 'total',
      title: 'Planning interval',
      align: 'right',
      render: (_, row) => <CapacityCell row={row} figures={row.total} strong />,
    },
    ...(canViewTeams
      ? [
          {
            key: 'plan',
            title: '',
            render: (_: unknown, row: PlanningIntervalTeamCapacityDto) => (
              <Button
                type="link"
                size="small"
                aria-label={`Open the plan for ${row.team.name}`}
                onClick={() => setPlanFor(row)}
              >
                Plan
              </Button>
            ),
          },
        ]
      : []),
  ]

  return (
    <>
      <Text type="secondary">
        Delivery person-days after holidays, time away and team days off, for
        each iteration by the planning interval&apos;s dates. A team of teams
        includes the teams beneath it, so rows are not added up. A dash is a
        team with no one allocated.
      </Text>
      <Table<PlanningIntervalTeamCapacityDto>
        size="small"
        bordered
        rowKey={(row) => row.team.id}
        columns={columns}
        dataSource={capacity?.teams ?? []}
        loading={isFetching}
        pagination={false}
        scroll={{ x: 'max-content' }}
        style={{ marginTop: 8 }}
        locale={{ emptyText: 'This planning interval has no teams.' }}
      />
      {planFor && capacity && (
        <Drawer
          open
          size="90%"
          title={`${planFor.team.name} — capacity plan`}
          onClose={() => setPlanFor(null)}
          destroyOnHidden
        >
          <TeamCapacityPlan
            team={{
              id: planFor.team.id,
              name: planFor.team.name,
              isActive: planFor.isActive,
            }}
            teamType={
              planFor.team.type === TEAM_OF_TEAMS ? 'TeamOfTeams' : 'Team'
            }
            from={capacity.start}
            to={capacity.end}
            daysOff={
              planFor.team.type === TEAM_OF_TEAMS
                ? undefined
                : planFor.teamDaysOff
            }
          />
        </Drawer>
      )}
    </>
  )
}

export default PlanningIntervalCapacitySection
