'use client'

import { PageTitle } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetVersionsQuery } from '@/src/store/features/delivery/versions-api'
import { Button } from 'antd'
import { FC, useEffect, useState } from 'react'
import { PlanVersionForm, VersionsGrid } from './_components'

const VersionsPage: FC = () => {
  useDocumentTitle('Versions')
  const [openPlanVersionForm, setOpenPlanVersionForm] = useState<boolean>(false)
  const messageApi = useMessage()

  const { hasPermissionClaim } = useAuth()
  const canCreateRelease = hasPermissionClaim('Permissions.Delivery.Create')

  const {
    data: releaseData,
    isLoading,
    error,
    refetch,
  } = useGetVersionsQuery(undefined)

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load versions.')
    }
  }, [error, messageApi])

  const actions = !canCreateRelease ? null : (
    <Button onClick={() => setOpenPlanVersionForm(true)}>Add Version</Button>
  )

  const onPlanVersionFormClosed = (wasPlanned: boolean) => {
    setOpenPlanVersionForm(false)
    if (wasPlanned) {
      refetch()
    }
  }

  return (
    <div className="page-gutters">
      <PageTitle title="Versions" actions={actions} />
      <VersionsGrid
        versions={releaseData ?? []}
        isLoading={isLoading}
        refetch={refetch}
        persistStateKey="delivery-versions"
      />
      {openPlanVersionForm && (
        <PlanVersionForm
          onFormComplete={() => onPlanVersionFormClosed(true)}
          onFormCancel={() => onPlanVersionFormClosed(false)}
        />
      )}
    </div>
  )
}

const VersionsPageWithAuthorization = requireFeatureFlag(
  authorizePage(VersionsPage, 'Permission', 'Permissions.Delivery.View'),
  'product-management',
)

export default VersionsPageWithAuthorization
