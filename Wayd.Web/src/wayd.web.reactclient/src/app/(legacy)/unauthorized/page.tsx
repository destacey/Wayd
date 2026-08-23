'use client'

import { Button, Result, Typography } from 'antd'
import useAuth from '@/src/components/contexts/auth'

const { Paragraph, Text } = Typography

export default function UnauthorizedPage() {
  const { user, logout } = useAuth()
  const username = user?.username || user?.name || 'Unknown user'

  return (
    <Result
      status="403"
      title="Access Denied"
      subTitle="You are not authorized to access Wayd."
      extra={[
        <Button type="primary" key="logout" onClick={logout}>
          Sign Out
        </Button>,
      ]}
    >
      <div className="desc">
        <Paragraph>
          <Text strong>You are signed in as: </Text>
          <Text code>{username}</Text>
        </Paragraph>
        <Paragraph>
          Your account has been authenticated, but you do not have the required
          permissions to use Wayd. Please contact your administrator if you
          believe this is an error.
        </Paragraph>
      </div>
    </Result>
  )
}
