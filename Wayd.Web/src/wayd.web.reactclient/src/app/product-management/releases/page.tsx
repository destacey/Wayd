'use client'

import { PageTitle } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetReleasesQuery } from '@/src/store/features/product-management/releases-api'
import { Button } from 'antd'
import { FC, useEffect, useState } from 'react'
import { PlanReleaseForm, ReleasesGrid } from './_components'

const ReleasesPage: FC = () => {
  useDocumentTitle('Releases')
  const [openPlanReleaseForm, setOpenPlanReleaseForm] = useState<boolean>(false)
  const messageApi = useMessage()

  const { hasPermissionClaim } = useAuth()
  // Releases carry their own permission rather than riding Delivery's: a product manager drafting
  // 2026.07 is a different person from whoever records that the pipeline ran.
  const canCreateRelease = hasPermissionClaim('Permissions.Releases.Create')

  const {
    data: releaseData,
    isLoading,
    error,
    refetch,
  } = useGetReleasesQuery(undefined)

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load releases.')
    }
  }, [error, messageApi])

  const actions = !canCreateRelease ? null : (
    <Button onClick={() => setOpenPlanReleaseForm(true)}>Add Release</Button>
  )

  const onPlanReleaseFormClosed = (wasPlanned: boolean) => {
    setOpenPlanReleaseForm(false)
    if (wasPlanned) {
      refetch()
    }
  }

  return (
    <div className="page-gutters">
      <PageTitle title="Releases" actions={actions} />
      <ReleasesGrid
        releases={releaseData ?? []}
        isLoading={isLoading}
        refetch={refetch}
        persistStateKey="product-management-releases"
      />
      {openPlanReleaseForm && (
        <PlanReleaseForm
          onFormComplete={() => onPlanReleaseFormClosed(true)}
          onFormCancel={() => onPlanReleaseFormClosed(false)}
        />
      )}
    </div>
  )
}

const ReleasesPageWithAuthorization = requireFeatureFlag(
  authorizePage(ReleasesPage, 'Permission', 'Permissions.Releases.View'),
  'product-management',
)

export default ReleasesPageWithAuthorization
