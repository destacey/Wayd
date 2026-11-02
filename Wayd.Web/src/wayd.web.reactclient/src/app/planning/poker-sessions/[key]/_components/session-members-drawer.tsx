'use client'

import {
  EmployeeNavigationDto,
  PlanningTeamNavigationDto,
  PokerSessionDetailsDto,
  PokerSessionMemberMode,
  Visibility,
} from '@/src/services/wayd-api'
import { useGetPokerSessionInvitedTeamMembersQuery } from '@/src/store/features/planning/poker-sessions-api'
import { DownOutlined, RightOutlined, TeamOutlined } from '@ant-design/icons'
import { Drawer, Flex, Spin, Tag, Typography } from 'antd'
import { FC, useState } from 'react'

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
  const { data: members, isFetching } =
    useGetPokerSessionInvitedTeamMembersQuery(
      { sessionId, teamId: team.id },
      { skip: !expanded },
    )

  return (
    <Flex vertical gap={4}>
      <Tag
        icon={<TeamOutlined />}
        style={{ cursor: 'pointer', alignSelf: 'flex-start', margin: 0 }}
        onClick={() => setExpanded((e) => !e)}
        aria-expanded={expanded}
      >
        {team.name} {expanded ? <DownOutlined /> : <RightOutlined />}
      </Tag>
      {expanded &&
        (isFetching ? (
          <Spin size="small" />
        ) : (
          <Flex vertical gap={4} style={{ paddingInlineStart: 16 }}>
            {(members ?? []).length === 0 ? (
              <Text type="secondary">No members.</Text>
            ) : (
              members!.map((m) => (
                <EmployeeRow
                  key={m.id}
                  employee={m}
                  extra={<Text type="secondary">via {team.name}</Text>}
                />
              ))
            )}
            {team.type === 'Team' && (
              <Text type="secondary">
                Members of its team of teams are invited too.
              </Text>
            )}
          </Flex>
        ))}
    </Flex>
  )
}

/**
 * Who runs the session, who has joined it, and, for a private session, who is
 * invited.
 */
const SessionMembersDrawer: FC<SessionMembersDrawerProps> = ({
  session,
  open,
  onClose,
}) => {
  const isPrivate = session.visibility === Visibility.Private
  const present = session.members.filter((m) => !m.leftOn)
  const left = session.members.filter((m) => m.leftOn)

  return (
    <Drawer title="Members" open={open} onClose={onClose} size="default">
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

        <Section title={`In the session (${present.length})`}>
          {present.length === 0 ? (
            <Text type="secondary">No one has joined yet.</Text>
          ) : (
            present.map((m) => (
              <EmployeeRow
                key={m.employee.id}
                employee={m.employee}
                extra={
                  <Tag
                    color={
                      m.mode === PokerSessionMemberMode.Participant
                        ? 'processing'
                        : undefined
                    }
                    style={{ margin: 0 }}
                  >
                    {m.mode}
                  </Tag>
                }
              />
            ))
          )}
        </Section>

        {left.length > 0 && (
          <Section title={`Left (${left.length})`}>
            {left.map((m) => (
              <EmployeeRow
                key={m.employee.id}
                employee={m.employee}
                extra={<Text type="secondary">{m.mode}</Text>}
              />
            ))}
          </Section>
        )}

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
          {session.invitedEmployees.map((e) => (
            <EmployeeRow key={e.id} employee={e} />
          ))}
          {session.invitedTeams.map((t) => (
            <InvitedTeam key={t.id} sessionId={session.id} team={t} />
          ))}
        </Section>
      </Flex>
    </Drawer>
  )
}

export default SessionMembersDrawer
