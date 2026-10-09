'use client'

import { useState } from 'react'
import Link from 'next/link'
import { Button, Flex, Spin, Table, Tag, Typography } from 'antd'
import { EditOutlined } from '@ant-design/icons'
import type { ColumnsType } from 'antd/es/table'
import {
  PlanningIntervalDetailsDto,
  PlanningSprintListDto,
} from '@/src/services/wayd-api'
import {
  useGetIterationSprintsQuery,
  useGetPlanningIntervalTeamsQuery,
} from '@/src/store/features/planning/planning-interval-api'
import WaydEmpty from '@/src/components/common/wayd-empty'
import useTheme from '@/src/components/contexts/theme'
import useAuth from '@/src/components/contexts/auth'
import ConfigureTeamSprintMappingsForm from './configure-team-sprint-mappings-form'
import {
  CalendarDate,
  compareCalendarDates,
  formatCalendarDate,
  todayCalendarDate,
} from '@/src/utils'

const { Text } = Typography

interface PlanningIntervalTeamSprintMappingsProps {
  planningInterval: PlanningIntervalDetailsDto
}

interface TeamRowData {
  key: string
  teamId: string
  teamKey: number
  teamName: string
  teamCode: string
  teamOfTeamsName: string | null
  sprintsByIteration: Record<string, PlanningSprintListDto | null>
}

const formatDateRange = (start: CalendarDate, end: CalendarDate): string => {
  return `${formatCalendarDate(start, 'MMM D')} - ${formatCalendarDate(end, 'MMM D')}`
}

const isActiveIteration = (start: CalendarDate, end: CalendarDate): boolean => {
  const today = todayCalendarDate()
  return (
    compareCalendarDates(start, today) <= 0 &&
    compareCalendarDates(today, end) <= 0
  )
}

interface SprintCellProps {
  sprint: PlanningSprintListDto | null
}

const SprintCell = ({ sprint }: SprintCellProps) => {
  if (!sprint) {
    return (
      <Tag color="default" style={{ width: '100%', textAlign: 'center' }}>
        No mapped sprint
      </Tag>
    )
  }

  return (
    <div style={{ textAlign: 'center' }}>
      <div>
        <Link href={`/work/sprints/${sprint.key}`}>{sprint.name}</Link>
      </div>
      <Text type="secondary" style={{ fontSize: '11px' }}>
        {formatDateRange(sprint.start, sprint.end)}
      </Text>
    </div>
  )
}

export const PlanningIntervalTeamSprintMappings = ({
  planningInterval,
}: PlanningIntervalTeamSprintMappingsProps) => {
  const { token } = useTheme()

  const { hasPermissionClaim } = useAuth()
  const canUpdatePlanningInterval = hasPermissionClaim(
    'Permissions.PlanningIntervals.Update',
  )

  const [editingTeam, setEditingTeam] = useState<TeamRowData | null>(null)

  const onEditTeamSprints = (team: TeamRowData) => {
    setEditingTeam(team)
  }

  const onFormSave = () => {
    setEditingTeam(null)
  }

  const onFormCancel = () => {
    setEditingTeam(null)
  }

  const { data: teamsData, isLoading: teamsLoading } =
    useGetPlanningIntervalTeamsQuery(planningInterval.key)

  const { data: iterationSprintsResponse, isLoading: sprintsLoading } =
    useGetIterationSprintsQuery({
      idOrKey: planningInterval.key.toString(),
    })

  // Iterations become columns, so they have to read left to right in the order
  // the PI runs them. The API returns them unordered.
  const iterationSprintsData = !iterationSprintsResponse
    ? undefined
    : [...iterationSprintsResponse].sort((a, b) =>
        compareCalendarDates(a.start, b.start),
      )

  const isLoading = teamsLoading || sprintsLoading

  // Build sprint lookup by team and iteration
  const sprintsByTeamAndIteration = (() => {
    if (!iterationSprintsData) return new Map<string, PlanningSprintListDto>()

    const lookup = new Map<string, PlanningSprintListDto>()

    iterationSprintsData.forEach((iteration) => {
      iteration.sprints?.forEach((sprint) => {
        const key = `${sprint.team.id}:${iteration.id}`
        lookup.set(key, sprint)
      })
    })

    return lookup
  })()

  // Build table data with team rows, filtered to only Teams (not Team of Teams), sorted alphabetically
  const tableData: TeamRowData[] = (() => {
    if (!teamsData || !iterationSprintsData) return []

    return [...teamsData]
      .filter((team) => team.type === 'Team')
      .sort((a, b) => a.name.localeCompare(b.name))
      .map((team) => {
        const sprintsByIteration: Record<string, PlanningSprintListDto | null> =
          {}

        iterationSprintsData.forEach((iteration) => {
          const key = `${team.id}:${iteration.id}`
          sprintsByIteration[iteration.id] =
            sprintsByTeamAndIteration.get(key) ?? null
        })

        return {
          key: team.id,
          teamId: team.id,
          teamKey: team.key,
          teamName: team.name,
          teamCode: team.code,
          teamOfTeamsName: team.teamOfTeams?.name ?? null,
          sprintsByIteration,
        }
      })
  })()

  // Build table columns dynamically based on iterations
  const columns: ColumnsType<TeamRowData> = (() => {
    const cols: ColumnsType<TeamRowData> = [
      {
        title: 'Team',
        dataIndex: 'teamName',
        key: 'team',
        fixed: 'left',
        width: 200,
        render: (_, record) => (
          <Flex justify="space-between" align="center">
            <div>
              <Link href={`/organizations/teams/${record.teamKey}`}>
                {record.teamName}
              </Link>
              {record.teamOfTeamsName && (
                <div>
                  <Text type="secondary" style={{ fontSize: '12px' }}>
                    {record.teamOfTeamsName}
                  </Text>
                </div>
              )}
            </div>
            {canUpdatePlanningInterval && (
              <Button
                type="text"
                size="small"
                icon={<EditOutlined />}
                onClick={() => onEditTeamSprints(record)}
              />
            )}
          </Flex>
        ),
      },
    ]

    if (iterationSprintsData) {
      const totalRows = teamsData?.length ?? 0

      iterationSprintsData.forEach((iteration) => {
        const isActive = isActiveIteration(iteration.start, iteration.end)
        const borderColor = token.colorPrimary
        const borderWidth = '2px'

        cols.push({
          title: (
            <div style={{ textAlign: 'center' }}>
              <div>{iteration.name}</div>
              <Text type="secondary" style={{ fontSize: '11px' }}>
                {formatDateRange(iteration.start, iteration.end)}
              </Text>
              <div>
                <Text type="secondary" style={{ fontSize: '11px' }}>
                  {iteration.category?.name}
                </Text>
              </div>
            </div>
          ),
          dataIndex: ['sprintsByIteration', iteration.id],
          key: iteration.id,
          width: 160,
          render: (sprint: PlanningSprintListDto | null) => (
            <SprintCell sprint={sprint} />
          ),
          onHeaderCell: () =>
            isActive
              ? {
                  style: {
                    borderTop: `${borderWidth} solid ${borderColor}`,
                    borderLeft: `${borderWidth} solid ${borderColor}`,
                    borderRight: `${borderWidth} solid ${borderColor}`,
                  },
                }
              : {},
          onCell: (_, index) =>
            isActive
              ? {
                  style: {
                    borderLeft: `${borderWidth} solid ${borderColor}`,
                    borderRight: `${borderWidth} solid ${borderColor}`,
                    ...(index === totalRows - 1 && {
                      borderBottom: `${borderWidth} solid ${borderColor}`,
                    }),
                  },
                }
              : {},
        })
      })
    }

    return cols
  })()

  if (isLoading) {
    return (
      <Flex justify="center" align="center" style={{ padding: '48px' }}>
        <Spin size="large" />
      </Flex>
    )
  }

  if (!teamsData || teamsData.length === 0) {
    return (
      <WaydEmpty message="No teams configured for this planning interval" />
    )
  }

  return (
    <>
      <Table
        dataSource={tableData}
        columns={columns}
        pagination={false}
        scroll={{ x: 'max-content' }}
        size="small"
        bordered
      />
      {editingTeam && (
        <ConfigureTeamSprintMappingsForm
          planningIntervalId={planningInterval.id}
          planningIntervalKey={planningInterval.key}
          teamId={editingTeam.teamId}
          teamName={editingTeam.teamName}
          teamOfTeamsName={editingTeam.teamOfTeamsName}
          onFormSave={onFormSave}
          onFormCancel={onFormCancel}
        />
      )}
    </>
  )
}

export default PlanningIntervalTeamSprintMappings
