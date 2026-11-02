'use client'

import {
  EmployeeNavigationDto,
  PlanningTeamNavigationDto,
  PokerSessionDetailsDto,
  Visibility,
} from '@/src/services/wayd-api'
import { useGetPokerSessionInvitedTeamMembersQuery } from '@/src/store/features/planning/poker-sessions-api'
import { DownOutlined, RightOutlined, TeamOutlined } from '@ant-design/icons'
import { Button, Drawer, Flex, Spin, Typography } from 'antd'
import { FC, useState } from 'react'
import ParticipantTags from './participant-tags'

const { Text, Title } = Typography

export interface SessionMembersDrawerProps {
  session: PokerSessionDetailsDto
  open: boolean
  onClose: () => void
}

const Section: FC<{ title: string; children: React.ReactNode }> = ({
  title,
  children,
}) => (
  <Flex vertical gap={8}>
    <Title level={5} style={{ margin: 0 }}>
      {title}
    </Title>
    {children}
  </Flex>
)

const EmployeeRow: FC<{
  employee: EmployeeNavigationDto
  extra?: React.ReactNode
}> = ({ employee, extra }) => (
  <Flex align="center" justify="space-between" gap={8}>
    <Text ellipsis>{employee.name}</Text>
    {extra}
  </Flex>
)

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
    <Flex vertical gap={4}>
      <Button
        type="text"
        size="small"
        icon={<TeamOutlined />}
        style={{ alignSelf: 'flex-start' }}
        onClick={() => setExpanded((e) => !e)}
        aria-expanded={expanded}
      >
        {team.name} {expanded ? <DownOutlined /> : <RightOutlined />}
      </Button>
      {expanded &&
        (isFetching ? (
          <Spin size="small" />
        ) : isError ? (
          <Text type="secondary" style={{ paddingInlineStart: 16 }}>
            Could not load this team&apos;s members.
          </Text>
        ) : (
          <Flex vertical gap={4} style={{ paddingInlineStart: 16 }}>
            {(members ?? []).length === 0 ? (
              <Text type="secondary">No members.</Text>
            ) : (
              members!.map((m) => <EmployeeRow key={m.id} employee={m} />)
            )}
            <Text type="secondary">
              Members of its team of teams are invited too.
            </Text>
          </Flex>
        ))}
    </Flex>
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
