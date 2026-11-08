'use client'

import useAuth from '@/src/components/contexts/auth'
import { useGetEmployeeTeamMembershipsQuery } from '@/src/store/features/organization/team-members-api'
import { useGetActiveSprintQuery } from '@/src/store/features/organizations/team-api'
import { TeamMemberDto } from '@/src/services/wayd-api'
import { Flex } from 'antd'
import ActiveTeamSprint from './active-team-sprint'
import { FC, useCallback, useEffect, useState } from 'react'

interface TeamSprintProbeProps {
  teamId: string
  onResult: (teamId: string, hasSprint: boolean) => void
}

const TeamSprintProbe: FC<TeamSprintProbeProps> = ({ teamId, onResult }) => {
  const { data: sprintData, isLoading } = useGetActiveSprintQuery(teamId)

  useEffect(() => {
    if (!isLoading) {
      onResult(teamId, !!sprintData)
    }
  }, [isLoading, sprintData, teamId, onResult])

  return null
}

interface MyTeamSprintsListProps {
  teamMemberships: TeamMemberDto[]
  onHasSprintsChange?: (hasSprints: boolean) => void
}

const MyTeamSprintsList: FC<MyTeamSprintsListProps> = ({
  teamMemberships,
  onHasSprintsChange,
}) => {
  const [sprintPresence, setSprintPresence] = useState<Record<string, boolean>>({})

  const handleResult = useCallback((teamId: string, hasSprint: boolean) => {
    setSprintPresence((prev) => {
      if (prev[teamId] === hasSprint) return prev
      return { ...prev, [teamId]: hasSprint }
    })
  }, [])

  const allProbed = teamMemberships.every((m) => m.team.id in sprintPresence)
  const hasAnySprint = Object.values(sprintPresence).some(Boolean)

  useEffect(() => {
    if (allProbed) {
      onHasSprintsChange?.(hasAnySprint)
    }
  }, [allProbed, hasAnySprint, onHasSprintsChange])

  return (
    <>
      {teamMemberships.map((m) => (
        <TeamSprintProbe key={m.team.id} teamId={m.team.id} onResult={handleResult} />
      ))}
      {/* The home widget around this list carries the title and the card. */}
      {allProbed && hasAnySprint && (
        <Flex vertical gap="middle">
          {teamMemberships
            .filter((m) => sprintPresence[m.team.id])
            .map((m) => (
              <ActiveTeamSprint
                key={m.team.id}
                teamId={m.team.id}
                showTeamLink
                embedded
              />
            ))}
        </Flex>
      )}
    </>
  )
}

interface MyTeamSprintsProps {
  onHasSprintsChange?: (hasSprints: boolean) => void
  /** Called when the caller's team memberships fail to load, so the sprints can never be known. */
  onLoadError?: () => void
}

const MyTeamSprints: FC<MyTeamSprintsProps> = ({
  onHasSprintsChange,
  onLoadError,
}) => {
  const { user } = useAuth()
  const employeeId = user?.employeeId

  const {
    data: memberships,
    isSuccess,
    isError,
  } = useGetEmployeeTeamMembershipsQuery(
    { employeeId: employeeId! },
    { skip: !employeeId },
  )

  const teamMemberships =
    memberships
      ?.filter((m) => m.team.type === 'Team')
      .sort((a, b) => a.team.code.localeCompare(b.team.code)) ?? []

  // "No sprints" only once the memberships have arrived: while they load the
  // list is empty too, and reporting then would show an empty state too early.
  useEffect(() => {
    if (isError) {
      onLoadError?.()
    } else if (!employeeId || (isSuccess && teamMemberships.length === 0)) {
      onHasSprintsChange?.(false)
    }
  }, [
    employeeId,
    isSuccess,
    isError,
    teamMemberships.length,
    onHasSprintsChange,
    onLoadError,
  ])

  if (!employeeId || teamMemberships.length === 0) return null

  return (
    <MyTeamSprintsList
      teamMemberships={teamMemberships}
      onHasSprintsChange={onHasSprintsChange}
    />
  )
}

export default MyTeamSprints
