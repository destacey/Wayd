'use client'

import { ReactNode, useState } from 'react'
import { Alert, Button, Flex, Segmented, Statistic } from 'antd'
import useAuth from '@/src/components/contexts/auth'
import { useLocalStorageState } from '@/src/hooks'
import { useMessage } from '@/src/components/contexts/messaging'
import { TeamDayOffDto } from '@/src/services/wayd-api'
import { CalendarDate, isApiError, type ApiError } from '@/src/utils'
import { useChangeEmployeeAllocationsMutation } from '@/src/store/features/organization/employee-allocations-api'
import {
  CapacityTeamType,
  useGetTeamCapacityPlanQuery,
} from '@/src/store/features/organization/team-capacity-api'
import {
  useGetTeamMembersQuery,
  useGetTeamOfTeamsMembersQuery,
} from '@/src/store/features/organization/team-members-api'
import ChangeAllocationForm from '../allocations/change-allocation-form'
import { formatPersonDays } from '../team-capacity/team-capacity-data'
import TimeAwayForm from '../time-away/time-away-form'
import CapacityPlanGrid, { PlanUnit } from './capacity-plan-grid'
import {
  PlanRow,
  planRequests,
  planRows,
  planWeeks,
  RowEdits,
} from './capacity-plan-data'
import styles from './team-capacity-plan.module.css'

export interface TeamCapacityPlanProps {
  team: { id: string; name: string; isActive: boolean }
  teamType: CapacityTeamType
  /** The plan's first and last days, both included. */
  from: CalendarDate
  to: CalendarDate
  /** Days the whole team is off for some or all of, such as a sprint's team days off. */
  daysOff?: TeamDayOffDto[]
  /** More actions beside the plan's own, shown while it is not being edited. */
  extraActions?: ReactNode
}

/** A person whose changes the API refused, and why. */
interface Failure {
  employeeId: string
  name: string
  reason: string
}

/**
 * A team's capacity over a fixed range, such as a sprint, as a plan: its people
 * against the range's weeks. Nothing is stored for the plan; each edit is an
 * allocation change over the edited week or day, so the days around it, and
 * every day outside the range, keep what they had.
 */
const TeamCapacityPlan = ({
  team,
  teamType,
  from,
  to,
  daysOff,
  extraActions,
}: TeamCapacityPlanProps) => {
  const messageApi = useMessage()
  const { hasPermissionClaim } = useAuth()
  const [changeAllocations] = useChangeEmployeeAllocationsMutation()

  const [editing, setEditing] = useState(false)
  const [edits, setEdits] = useState<Record<string, RowEdits>>({})
  const [isSaving, setIsSaving] = useState(false)
  const [failures, setFailures] = useState<Failure[]>([])
  const [allocating, setAllocating] = useState(false)
  const [recordingFor, setRecordingFor] = useState<PlanRow | null>(null)
  const [unit, setUnit] = useLocalStorageState<PlanUnit>(
    'capacity-plan-unit',
    'percent',
  )

  const {
    currentData: plan,
    isFetching,
    error,
  } = useGetTeamCapacityPlanQuery({
    teamId: team.id,
    teamType,
    from,
    to,
    daysOff,
  })

  const { data: teamMembers } = useGetTeamMembersQuery(
    { teamId: team.id },
    { skip: teamType !== 'Team' },
  )
  const { data: teamOfTeamsMembers } = useGetTeamOfTeamsMembersQuery(
    { teamId: team.id },
    { skip: teamType !== 'TeamOfTeams' },
  )
  const members = teamType === 'Team' ? teamMembers : teamOfTeamsMembers
  const memberIds = new Set(members?.map((m) => m.employee.id))

  // The commands refuse new time on an inactive team, so it is read-only.
  const canEdit =
    hasPermissionClaim('Permissions.Teams.Update') &&
    plan?.capacity.canManage === true &&
    team.isActive

  const rows = plan ? planRows(plan) : []
  // Until the plan arrives; the system setting defaults to the same.
  const workingDayHours = plan?.capacity.workingDayHours ?? 8
  const weeks = planWeeks(from, to, plan?.days ?? [])
  const requests = planRequests(rows, weeks, edits, team.id)

  const onEditRow = (rowId: string, rowEdits: RowEdits) =>
    setEdits((current) => ({ ...current, [rowId]: rowEdits }))

  const stopEditing = () => {
    setEditing(false)
    setEdits({})
    setFailures([])
  }

  const save = async () => {
    setIsSaving(true)
    const failed: Failure[] = []
    for (const { employeeId, request } of requests) {
      const response = await changeAllocations({ employeeId, request })
      if (response.error) {
        const apiError: ApiError = isApiError(response.error)
          ? response.error
          : {}
        failed.push({
          employeeId,
          name:
            rows.find((r) => r.employee.id === employeeId)?.employee.name ??
            employeeId,
          reason: apiError.detail ?? 'The changes could not be saved.',
        })
      }
    }
    setIsSaving(false)

    if (failed.length === 0) {
      messageApi.success('Plan saved.')
      stopEditing()
      return
    }

    // Keep only the edits that did not save, to correct and retry.
    const failedIds = new Set(failed.map((f) => f.employeeId))
    setEdits((current) =>
      Object.fromEntries(
        Object.entries(current).filter(([rowId]) =>
          failedIds.has(rowId.split('|')[0]),
        ),
      ),
    )
    setFailures(failed)
  }

  if (error) {
    return (
      <Alert
        type="error"
        showIcon
        title="The team's capacity could not be loaded for these dates."
        description={isApiError(error) ? error.detail : undefined}
      />
    )
  }

  return (
    <Flex vertical gap="middle">
      <Flex gap="large" wrap>
        <Statistic
          title="Working days"
          value={plan?.capacity.workingDays ?? 0}
        />
        <Statistic title="People" value={plan?.capacity.people.length ?? 0} />
        <Statistic
          title="Allocated person-days"
          value={formatPersonDays(plan?.capacity.allocatedPersonDays ?? 0)}
        />
        <Statistic
          title="Delivery person-days"
          value={formatPersonDays(plan?.capacity.deliveryPersonDays ?? 0)}
        />
        <Statistic
          title="Time away person-days"
          value={formatPersonDays(plan?.capacity.awayPersonDays ?? 0)}
        />
      </Flex>

      <Flex justify="space-between" align="center" wrap gap="small">
        <Flex gap="small" className={styles.actions}>
          {canEdit && editing && (
            <>
              <Button
                type="primary"
                size="small"
                loading={isSaving}
                disabled={requests.length === 0}
                onClick={save}
              >
                Save
              </Button>
              <Button size="small" disabled={isSaving} onClick={stopEditing}>
                Cancel
              </Button>
            </>
          )}
          {canEdit && !editing && (
            <>
              <Button size="small" onClick={() => setAllocating(true)}>
                Allocate Person
              </Button>
              <Button
                size="small"
                disabled={rows.length === 0}
                onClick={() => setEditing(true)}
              >
                Edit Plan
              </Button>
            </>
          )}
          {!editing && extraActions}
        </Flex>
        <Segmented<PlanUnit>
          size="small"
          value={unit}
          onChange={setUnit}
          options={[
            { label: '%', value: 'percent' },
            { label: 'Hours', value: 'hours' },
          ]}
          aria-label="Show the plan in percent or hours"
        />
      </Flex>

      {editing && (
        <Alert
          type="info"
          showIcon
          title="A week or a day sets the person's allocation for those days only. The days before and after them keep what they have."
          description="Time away is not an allocation: record it with the calendar button beside a person's name. It applies on every team they are on."
        />
      )}

      {failures.length > 0 && (
        <Alert
          type="error"
          showIcon
          title="These people's changes could not be saved. Everyone else's were."
          description={
            <ul>
              {failures.map((failure) => (
                <li key={failure.employeeId}>
                  {`${failure.name}: ${failure.reason}`}
                </li>
              ))}
            </ul>
          }
        />
      )}

      <CapacityPlanGrid
        rows={rows}
        weeks={weeks}
        editing={editing}
        edits={edits}
        onEditRow={onEditRow}
        unit={unit}
        workingDayHours={workingDayHours}
        onRecordTimeAway={setRecordingFor}
        teamDaysOff={plan?.teamDaysOff}
        isLoading={isFetching}
      />

      {recordingFor && (
        <TimeAwayForm
          employeeId={recordingFor.employee.id}
          workingDayHours={recordingFor.workingDayHours}
          initialDates={{ start: from, end: from }}
          onFormComplete={() => setRecordingFor(null)}
          onFormCancel={() => setRecordingFor(null)}
        />
      )}

      {allocating && (
        <ChangeAllocationForm
          team={team}
          initialDates={{ start: from, end: to }}
          // A member's time on the team runs on past the plan, so they are in
          // the next one too; a non-member's ends with it. Until the members
          // have loaded no one can be told apart, so the end is left as the
          // form opened it.
          endDateFor={
            members
              ? (employeeId) => (memberIds.has(employeeId) ? undefined : to)
              : undefined
          }
          onFormComplete={() => setAllocating(false)}
          onFormCancel={() => setAllocating(false)}
        />
      )}
    </Flex>
  )
}

export default TeamCapacityPlan
