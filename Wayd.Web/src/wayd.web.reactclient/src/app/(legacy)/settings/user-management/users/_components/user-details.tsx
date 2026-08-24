'use client'

import { UserDetailsDto } from '@/src/services/wayd-api'
import { InfoCircleOutlined } from '@ant-design/icons'
import { Alert, Card, Descriptions, Flex, Tag, Typography } from 'antd'
import WaydList from '@/src/components/common/wayd-list'
import dayjs from 'dayjs'
import Link from 'next/link'

const { Text } = Typography

const { Item } = Descriptions
const { Item: ListItem } = WaydList

interface UserDetailsProps {
  user: UserDetailsDto
}

const UserDetails = (props: UserDetailsProps) => {
  const { user } = props

  if (!user) return null

  return (
    <Flex vertical gap="middle">
      {user.pendingMigrationTenantId && (
        <Alert
          type="info"
          showIcon
          title="Tenant migration pending"
          description={
            <>
              This user has a staged migration to tenant{' '}
              <Text code copyable={{ text: user.pendingMigrationTenantId }}>
                {user.pendingMigrationTenantId}
              </Text>
              . The rebind completes on their next sign-in from that tenant.
            </>
          }
        />
      )}
      <Descriptions column={2} size="small">
        <Item label="User Name">{user.userName}</Item>
        <Item label="Email">{user.email}</Item>
        <Item label="First Name">{user.firstName}</Item>
        <Item label="Last Name">{user.lastName}</Item>
        <Item label="Phone Number">{user.phoneNumber}</Item>
        <Item label="Login Provider">
          {user.loginProvider === 'MicrosoftEntraId'
            ? 'Microsoft Entra ID'
            : user.loginProvider}
        </Item>
        <Item label="Employee">
          {user.employee && (
            <Link href={`/organizations/employees/${user.employee?.key}`}>
              {user.employee?.name}
            </Link>
          )}
        </Item>
        <Item label="Last Activity">
          {user.lastActivityAt
            ? dayjs(user.lastActivityAt).format('MMM D, YYYY h:mm A')
            : null}
        </Item>
        <Item label="Is Active?">{user.isActive?.toString()}</Item>
        {user.lockoutEnd && new Date(user.lockoutEnd) > new Date() && (
          <Item label="Account Status">
            <Tag color="error">
              Locked until {dayjs(user.lockoutEnd).format('MMM D, YYYY h:mm A')}
            </Tag>
          </Item>
        )}
      </Descriptions>
      <Card size="small" title="Roles" style={{ width: 300 }}>
        <WaydList
          size="small"
          dataSource={user.roles}
          renderItem={(item) => (
            <ListItem>
              <Link href={`/settings/user-management/roles/${item.id}`}>
                {item.name}
              </Link>
              {item.description && (
                <InfoCircleOutlined title={item.description} />
              )}
            </ListItem>
          )}
        />
      </Card>
    </Flex>
  )
}

export default UserDetails
