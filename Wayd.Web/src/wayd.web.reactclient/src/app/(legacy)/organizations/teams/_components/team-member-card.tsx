'use client'

import { TeamMemberDto } from '@/src/store/features/organization/team-members-api'
import { getAvatarColor, getInitials } from '@/src/utils'
import { Avatar, Card, Flex, Tag, Typography } from 'antd'
import { useRouter } from 'next/navigation'

const { Text } = Typography

interface Props {
  member: TeamMemberDto
}

const TeamMemberCard = ({ member }: Props) => {
  const router = useRouter()

  return (
    <Card
      size="small"
      hoverable
      onClick={() =>
        router.push(`/organizations/employees/${member.employee.key}`)
      }
      styles={{ body: { padding: '12px' } }}
    >
      <Flex gap={10} align="flex-start">
        <Avatar
          size={36}
          style={{
            flexShrink: 0,
            backgroundColor: getAvatarColor(member.employee.name),
          }}
        >
          {getInitials(member.employee.name)}
        </Avatar>
        <Flex vertical>
          <Text strong>{member.employee.name}</Text>
          {member.employee.jobTitle && (
            <Text type="secondary" style={{ fontSize: 11 }}>
              {member.employee.jobTitle}
            </Text>
          )}
          <Flex wrap gap={4}>
            {member.roles.map((role) => (
              <Tag
                key={role.id}
                variant="filled"
                style={{ margin: 0, fontSize: 11 }}
              >
                {role.name}
              </Tag>
            ))}
          </Flex>
        </Flex>
      </Flex>
    </Card>
  )
}

export default TeamMemberCard
