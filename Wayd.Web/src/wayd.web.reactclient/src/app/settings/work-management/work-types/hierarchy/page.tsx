'use client'

import { RecordShell } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetWorkTypeLevelsQuery } from '@/src/store/features/work-management/work-type-level-api'
import { useGetWorkTypeTiersQuery } from '@/src/store/features/work-management/work-type-tier-api'
import { Space, Spin } from 'antd'
import { WorkTypeTierCard } from '../_components'

const HierarchyPage = () => {
  useDocumentTitle('Work Management - Work Type Hierarchy')

  const { data: workTiers, isLoading: workTiersIsLoading } =
    useGetWorkTypeTiersQuery(null)
  const {
    data: workLevels,
    isLoading: workLevelsIsLoading,
    refetch: refetchLevels,
  } = useGetWorkTypeLevelsQuery(null)

  const { hasClaim } = useAuth()
  const canCreateWorkTypeLevels = hasClaim(
    'Permission',
    'Permissions.WorkTypeLevels.Create',
  )
  const canUpdateWorkTypeLevels = hasClaim(
    'Permission',
    'Permissions.WorkTypeLevels.Update',
  )

  return (
    <RecordShell
      record={{
        name: 'Work Type Hierarchy',
        parent: {
          label: 'Work Types',
          href: '/settings/work-management/work-types',
        },
      }}
    >
      <Spin
        spinning={workTiersIsLoading || workLevelsIsLoading}
        description="Loading work type tiers and levels..."
        size="large"
        style={{ paddingTop: 50 }}
      >
        <Space vertical>
          {workTiers?.map((tier) => (
            <WorkTypeTierCard
              key={tier.id}
              tier={tier}
              levels={
                workLevels?.filter((level) => level.tier.id === tier.id) ?? []
              }
              refreshLevels={refetchLevels}
              canCreateWorkTypeLevels={canCreateWorkTypeLevels}
              canUpdateWorkTypeLevels={canUpdateWorkTypeLevels}
            />
          ))}
        </Space>
      </Spin>
    </RecordShell>
  )
}

const HierarchyPageWithAuthorization = authorizePage(
  HierarchyPage,
  'Permission',
  'Permissions.WorkTypeLevels.View',
)

export default HierarchyPageWithAuthorization
