'use client'

import { WaydEmpty } from '@/src/components/common'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { useGetEmployeeTeamMembershipsQuery } from '@/src/store/features/organization/team-members-api'
import { Card, Flex, Skeleton, Tag, Typography } from 'antd'
import Link from 'next/link'
import { useMemo } from 'react'

const { Text, Title } = Typography

interface Props {
  employeeId: string
}

/**
 * The employee's team memberships, beside the facts rail on the Details tab.
 *
 * A summary rather than the full grid on the Teams tab — the question here is
 * "who is this person working with", not "let me filter their memberships".
 */
const EmployeeTeamsSummary = ({ employeeId }: Props) => {
  const { data: memberships, isLoading } = useGetEmployeeTeamMembershipsQuery(
    { employeeId },
    { skip: !employeeId },
  )

  const sorted = useMemo(
    () =>
      [...(memberships ?? [])].sort((a, b) =>
        caseInsensitiveCompare(a.team.name, b.team.name),
      ),
    [memberships],
  )

  return (
    <Flex vertical gap="small">
      <Title level={4} style={{ margin: 0 }}>
        Teams
      </Title>
      <Card size="small">
        {isLoading ? (
          <Skeleton active paragraph={{ rows: 2 }} title={false} />
        ) : sorted.length === 0 ? (
          <WaydEmpty message="Not a member of any team." />
        ) : (
          <Flex vertical gap={10}>
            {sorted.map((m) => (
              <Flex
                key={m.team.id}
                align="center"
                gap="small"
                wrap
                justify="space-between"
              >
                <Link href={`/organizations/teams/${m.team.key}`}>
                  {m.team.name}
                </Link>
                <Flex gap={4} wrap>
                  {m.roles.length > 0 ? (
                    [...m.roles]
                      .sort((a, b) => caseInsensitiveCompare(a.name, b.name))
                      .map((r) => <Tag key={r.id}>{r.name}</Tag>)
                  ) : (
                    <Text type="secondary">Member</Text>
                  )}
                </Flex>
              </Flex>
            ))}
          </Flex>
        )}
      </Card>
    </Flex>
  )
}

export default EmployeeTeamsSummary
