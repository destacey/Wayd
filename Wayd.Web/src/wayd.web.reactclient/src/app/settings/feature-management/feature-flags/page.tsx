'use client'

import { Alert, Empty, Flex, Input, Skeleton } from 'antd'
import { SearchOutlined } from '@ant-design/icons'
import { useState } from 'react'
import PageTitle from '@/src/components/common/page-title'
import { authorizePage } from '@/src/components/hoc'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { useDocumentTitle } from '@/src/hooks'
import type { FeatureFlagDto } from '@/src/services/wayd-api'
import {
  useGetFeatureFlagModulesQuery,
  useGetFeatureFlagsQuery,
  useToggleFeatureFlagMutation,
} from '@/src/store/features/admin/feature-flags-api'
import FeatureFlagModuleCard from './_components/feature-flag-module-card'
import { groupFeatureFlags } from './_components/group-feature-flags'

const FeatureFlagsPage = () => {
  useDocumentTitle('Feature Flags')
  const [search, setSearch] = useState('')
  const messageApi = useMessage()

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.FeatureFlags.Update')

  const {
    data: flags = [],
    isLoading: isLoadingFlags,
    isError: flagsFailed,
  } = useGetFeatureFlagsQuery()
  const {
    data: modules = [],
    isLoading: isLoadingModules,
    isError: modulesFailed,
  } = useGetFeatureFlagModulesQuery()
  const [toggleFeatureFlag] = useToggleFeatureFlagMutation()

  const groups = groupFeatureFlags(modules, flags, search)

  const handleToggle = async (flag: FeatureFlagDto, isEnabled: boolean) => {
    try {
      await toggleFeatureFlag({ id: flag.id, isEnabled }).unwrap()
      messageApi.success(
        `${flag.displayName} turned ${isEnabled ? 'on' : 'off'}.`,
      )
    } catch {
      messageApi.error(
        `Could not turn ${flag.displayName} ${isEnabled ? 'on' : 'off'}.`,
      )
    }
  }

  return (
    <div className="page-gutters">
      <PageTitle
        title="Feature Flags"
        subtitle="Turn features on or off for everyone in your organization. Changes take effect right away."
        actions={
          <Input
            allowClear
            placeholder="Search feature flags"
            aria-label="Search feature flags"
            prefix={<SearchOutlined />}
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            style={{ width: '100%', maxWidth: 300 }}
          />
        }
      />
      {isLoadingFlags || isLoadingModules ? (
        <Skeleton active />
      ) : flagsFailed || modulesFailed ? (
        <Alert
          type="error"
          showIcon
          title="The feature flags could not be loaded. Refresh the page to try again."
        />
      ) : groups.length === 0 ? (
        <Empty
          description={
            search ? 'No feature flags match your search.' : 'No feature flags.'
          }
        />
      ) : (
        <Flex vertical gap="middle">
          {groups.map((group) => (
            <FeatureFlagModuleCard
              key={group.module.code}
              group={group}
              canUpdate={canUpdate}
              onToggle={handleToggle}
            />
          ))}
        </Flex>
      )}
    </div>
  )
}

export default authorizePage(
  FeatureFlagsPage,
  'Permission',
  'Permissions.FeatureFlags.View',
)
