'use client'

import { LabeledContent } from '@/src/components/common/content'
import { RecordPersonLink } from '@/src/components/common/record'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { UserDetailsDto } from '@/src/services/wayd-api'
import { InfoCircleOutlined } from '@ant-design/icons'
import { Alert, Card, Divider, Flex, Typography } from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'
import UserIdentityHistory from '../../_components/user-identity-history'
import styles from './user-overview.module.css'

const { Text } = Typography

/** Entra's provider id is not something to show a person. */
const providerName = (loginProvider: string) =>
  loginProvider === 'MicrosoftEntraId' ? 'Microsoft Entra ID' : loginProvider

export interface UserOverviewProps {
  user: UserDetailsDto
}

/**
 * A user's account details, roles, and identity history — the whole record.
 *
 * No facts panel and no section rail. The panel is closed by default and holds
 * reference material beside content, but an account's own fields *are* the
 * content; and a rail over two entries, one of them a single grid, spends
 * 190px saying there is nowhere to go. Everything stacks on one page instead.
 */
const UserOverview = ({ user }: UserOverviewProps) => {
  const isLockedOut = !!user.lockoutEnd && new Date(user.lockoutEnd) > new Date()

  const roles = [...(user.roles ?? [])].sort((a, b) =>
    caseInsensitiveCompare(a.name, b.name),
  )

  return (
    <Flex vertical gap="middle">
      <Flex gap="middle" wrap align="flex-start">
        <Card size="small" title="Account" style={{ flex: '1 1 380px' }}>
          <Flex vertical gap={16}>
            {isLockedOut && (
              <Alert
                type="error"
                showIcon
                title="Account locked"
                description={`Locked until ${dayjs(user.lockoutEnd).format('MMM D, YYYY h:mm A')}.`}
              />
            )}

            {/*
              A responsive grid rather than fixed pairs: the card sits beside
              Roles at full width and alone below it, so a hard two-column
              layout leaves one ragged column at the narrow end. `auto-fit`
              with a floor lets the fields reflow to whatever the card gets.
            */}
            <div className={styles.fieldGrid}>
              <LabeledContent label="First Name">
                {user.firstName}
              </LabeledContent>
              <LabeledContent label="Last Name">{user.lastName}</LabeledContent>
              <LabeledContent label="User Name">{user.userName}</LabeledContent>
              <LabeledContent label="Phone Number">
                {user.phoneNumber || <Text type="secondary">Not set</Text>}
              </LabeledContent>
            </div>

            {/* Email is its own row: it is the longest value here, and pairing
                it would force the column beside it narrow for every record. */}
            <LabeledContent label="Email">{user.email}</LabeledContent>

            <Divider className={styles.divider} />

            <div className={styles.fieldGrid}>
              <LabeledContent label="Login Provider">
                {providerName(user.loginProvider)}
              </LabeledContent>
              <LabeledContent label="Last Activity">
                {user.lastActivityAt ? (
                  dayjs(user.lastActivityAt).format('MMM D, YYYY h:mm A')
                ) : (
                  <Text type="secondary">Never</Text>
                )}
              </LabeledContent>
            </div>

            <LabeledContent label="Employee">
              {user.employee ? (
                <RecordPersonLink
                  name={user.employee.name}
                  href={`/organizations/employees/${user.employee.key}`}
                />
              ) : (
                <Text type="secondary">Not linked</Text>
              )}
            </LabeledContent>
          </Flex>
        </Card>

        <Card
          size="small"
          title={`Roles${roles.length > 0 ? ` (${roles.length})` : ''}`}
          style={{ flex: '0 1 300px' }}
        >
          {roles.length === 0 ? (
            <Text type="secondary">
              This account has no roles, so it can see nothing beyond its own
              profile.
            </Text>
          ) : (
            <Flex vertical gap={6}>
              {roles.map((role) => (
                <Flex key={role.id} align="center" gap={6}>
                  <Link href={`/settings/user-management/roles/${role.id}`}>
                    {role.name}
                  </Link>
                  {role.description && (
                    <InfoCircleOutlined title={role.description} />
                  )}
                </Flex>
              ))}
            </Flex>
          )}
        </Card>
      </Flex>

      <Card size="small" title="Identity History">
        <UserIdentityHistory userId={user.id} />
      </Card>
    </Flex>
  )
}

export default UserOverview
