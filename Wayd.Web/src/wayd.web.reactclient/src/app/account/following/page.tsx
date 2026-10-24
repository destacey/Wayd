'use client'

import { Alert, Flex, Spin } from 'antd'
import { PageTitle, WaydEmpty } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetMyFollowsQuery } from '@/src/store/features/common/follows-api'
import FollowingGroup from './_components/following-group'

const PageSize = 10

const FollowingPage = () => {
  useDocumentTitle('Following')
  const { user } = useAuth()

  const {
    data: groups,
    isLoading,
    isError,
  } = useGetMyFollowsQuery({
    pageSize: PageSize,
  })

  const renderContent = () => {
    if (isLoading) return <Spin />
    if (isError)
      return <Alert type="error" title="Could not load what you follow." />
    if (!user?.employeeId)
      return (
        <WaydEmpty message="Following needs your account to be linked to an employee. Ask an administrator to link it in Settings → Users." />
      )
    if (!groups || groups.length === 0)
      return (
        <WaydEmpty message="You are not following anything yet. Use Follow on a project, program or planning interval objective to keep it here." />
      )

    return (
      <Flex vertical gap="middle">
        {groups.map((group) => (
          <FollowingGroup
            key={group.ownerType}
            group={group}
            pageSize={PageSize}
          />
        ))}
      </Flex>
    )
  }

  return (
    <>
      <PageTitle
        title="Following"
        subtitle="Records you follow and can still see. Following records your interest; it does not send notifications yet."
      />
      {renderContent()}
    </>
  )
}

export default requireFeatureFlag(FollowingPage, 'following')
