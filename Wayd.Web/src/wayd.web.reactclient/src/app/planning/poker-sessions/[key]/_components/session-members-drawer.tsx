'use client'

import {
  PlanningTeamNavigationDto,
  PokerSessionDetailsDto,
  Visibility,
} from '@/src/services/wayd-api'
import {
  EmployeeRow,
  PeopleSection as Section,
  TeamMembersTag,
} from '@/src/components/common/planning/record-people'
import { useGetPokerSessionInvitedTeamMembersQuery } from '@/src/store/features/planning/poker-sessions-api'
import { Drawer, Flex, Typography } from 'antd'
import { FC, useState } from 'react'
import ParticipantTags from './participant-tags'

const { Text } = Typography

export interface SessionMembersDrawerProps {
  session: PokerSessionDetailsDto
  open: boolean
  onClose: () => void
}

/** An invited team as one tag; opening it lists who the invite reaches. */
const InvitedTeam: FC<{
  sessionId: string
  team: PlanningTeamNavigationDto
}> = ({ sessionId, team }) => {
  const [expanded, setExpanded] = useState(false)
  const {
    data: members,
    isFetching,
    isError,
  } = useGetPokerSessionInvitedTeamMembersQuery(
    { sessionId, teamId: team.id },
    { skip: !expanded },
  )

  return (
    <TeamMembersTag
      team={team}
      expanded={expanded}
      onToggle={() => setExpanded((e) => !e)}
      members={members}
      isFetching={isFetching}
      isError={isError}
      note="Members of its team of teams are invited too."
    />
  )
}

/**
 * Who runs the session, who has joined it and whether they vote, and, for a
 * private session, who is invited.
 */
const SessionMembersDrawer: FC<SessionMembersDrawerProps> = ({
  session,
  open,
  onClose,
}) => {
  const isPrivate = session.visibility === Visibility.Private
  const facilitatorIds = new Set(session.facilitators.map((f) => f.id))

  return (
    <Drawer title="Participants" open={open} onClose={onClose} size="default">
      <Flex vertical gap={24}>
        <Section title="Facilitators">
          {session.facilitators.length === 0 ? (
            <Text type="secondary">
              No facilitators. A poker administrator can run this session.
            </Text>
          ) : (
            session.facilitators.map((f) => (
              <EmployeeRow key={f.id} employee={f} />
            ))
          )}
        </Section>

        <Section title={`Joined (${session.members.length})`}>
          {session.members.length === 0 ? (
            <Text type="secondary">No one has joined yet.</Text>
          ) : (
            session.members.map((m) => (
              <EmployeeRow
                key={m.employee.id}
                employee={m.employee}
                extra={
                  <ParticipantTags
                    isFacilitator={facilitatorIds.has(m.employee.id)}
                    isVoting={m.isVoting}
                  />
                }
              />
            ))
          )}
        </Section>

        <Section title="Access">
          <Text type="secondary">
            {isPrivate
              ? 'Private: only facilitators and the people and teams invited can see and join.'
              : 'Public: anyone who can view Planning Poker can see and join.'}
          </Text>
          {isPrivate &&
            session.invitedEmployees.length === 0 &&
            session.invitedTeams.length === 0 && (
              <Text type="secondary">No one is invited.</Text>
            )}
          {/* A public session keeps its invites, but they admit no one. */}
          {isPrivate &&
            session.invitedEmployees.map((e) => (
              <EmployeeRow key={e.id} employee={e} />
            ))}
          {isPrivate &&
            session.invitedTeams.map((t) => (
              <InvitedTeam key={t.id} sessionId={session.id} team={t} />
            ))}
        </Section>
      </Flex>
    </Drawer>
  )
}

export default SessionMembersDrawer
