'use client'

import useAuth from '@/src/components/contexts/auth'
import { useGetEmployeeTeamMembershipsQuery } from '@/src/store/features/organization/team-members-api'
import { useGetActiveSprintQuery } from '@/src/store/features/organizations/team-api'
import { TeamMemberDto } from '@/src/services/wayd-api'
import { Flex, Typography } from 'antd'
import ActiveTeamSprint from './active-team-sprint'
import { FC, useCallback, useEffect, useState } from 'react'

const { Title } = Typography

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
      {allProbed && hasAnySprint && (
        <Flex vertical gap="middle">
          <Title level={5} style={{ margin: 0 }}>
            My Team Sprints
          </Title>
          {teamMemberships
            .filter((m) => sprintPresence[m.team.id])
            .map((m) => (
              <ActiveTeamSprint key={m.team.id} teamId={m.team.id} showTeamLink />
            ))}
        </Flex>
      )}
    </>
  )
}

interface MyTeamSprintsProps {
  onHasSprintsChange?: (hasSprints: boolean) => void
}

const MyTeamSprints: FC<MyTeamSprintsProps> = ({ onHasSprintsChange }) => {
  const { user } = useAuth()
  const employeeId = user?.employeeId

  const { data: memberships } = useGetEmployeeTeamMembershipsQuery(
    { employeeId: employeeId! },
    { skip: !employeeId },
  )

  const teamMemberships =
    memberships
      ?.filter((m) => m.team.type === 'Team')
      .sort((a, b) => a.team.code.localeCompare(b.team.code)) ?? []

  useEffect(() => {
    if (!employeeId || teamMemberships.length === 0) {
      onHasSprintsChange?.(false)
    }
  }, [employeeId, teamMemberships.length, onHasSprintsChange])

  if (!employeeId || teamMemberships.length === 0) return null

  return (
    <MyTeamSprintsList
      teamMemberships={teamMemberships}
      onHasSprintsChange={onHasSprintsChange}
    />
  )
}

export default MyTeamSprints
