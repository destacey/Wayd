'use client'

import {
  EmployeeNavigationDto,
  PlanningTeamNavigationDto,
} from '@/src/services/wayd-api'
import { DownOutlined, RightOutlined, TeamOutlined } from '@ant-design/icons'
import { Button, Flex, Spin, Typography } from 'antd'
import { FC, ReactNode } from 'react'

const { Text, Title } = Typography

/** A titled group of people in a drawer listing who is on a record. */
export const PeopleSection: FC<{ title: string; children: ReactNode }> = ({
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

/** One person on a record, with optional tags beside their name. */
export const EmployeeRow: FC<{
  employee: EmployeeNavigationDto
  extra?: ReactNode
}> = ({ employee, extra }) => (
  <Flex align="center" justify="space-between" gap={8}>
    <Text ellipsis>{employee.name}</Text>
    {extra}
  </Flex>
)

export interface TeamMembersTagProps {
  team: PlanningTeamNavigationDto
  expanded: boolean
  onToggle: () => void
  members?: EmployeeNavigationDto[]
  isFetching: boolean
  isError?: boolean
  /** Shown under the members, such as who else the team brings in. */
  note: string
}

/**
 * A team on a record as one tag; opening it lists the team's members. The
 * caller loads the members, from its record's own endpoint, once expanded.
 */
export const TeamMembersTag: FC<TeamMembersTagProps> = ({
  team,
  expanded,
  onToggle,
  members,
  isFetching,
  isError,
  note,
}) => (
  <Flex vertical gap={4}>
    <Button
      type="text"
      size="small"
      icon={<TeamOutlined />}
      style={{ alignSelf: 'flex-start' }}
      onClick={onToggle}
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
          <Text type="secondary">{note}</Text>
        </Flex>
      ))}
  </Flex>
)
