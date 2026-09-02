'use client'

import { PageTitle } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetReleasePackagesQuery } from '@/src/store/features/product-management/release-packages-api'
import { Button } from 'antd'
import { FC, useEffect, useState } from 'react'
import { AssembleReleasePackageForm, ReleasePackagesGrid } from './_components'

const ReleasePackagesPage: FC = () => {
  useDocumentTitle('Release Packages')
  const [openAssembleForm, setOpenAssembleForm] = useState<boolean>(false)
  const messageApi = useMessage()

  const { hasPermissionClaim } = useAuth()
  const canCreatePackage = hasPermissionClaim(
    'Permissions.Delivery.Create',
  )

  const {
    data: packageData,
    isLoading,
    error,
    refetch,
  } = useGetReleasePackagesQuery(undefined)

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load release packages.')
    }
  }, [error, messageApi])

  const actions = !canCreatePackage ? null : (
    <Button onClick={() => setOpenAssembleForm(true)}>Assemble Package</Button>
  )

  const onAssembleFormClosed = (wasAssembled: boolean) => {
    setOpenAssembleForm(false)
    if (wasAssembled) {
      refetch()
    }
  }

  return (
    <div className="page-gutters">
      <PageTitle title="Release Packages" actions={actions} />
      <ReleasePackagesGrid
        packages={packageData ?? []}
        isLoading={isLoading}
        refetch={refetch}
        persistStateKey="product-management-release-packages"
      />
      {openAssembleForm && (
        <AssembleReleasePackageForm
          onFormComplete={() => onAssembleFormClosed(true)}
          onFormCancel={() => onAssembleFormClosed(false)}
        />
      )}
    </div>
  )
}

const ReleasePackagesPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    ReleasePackagesPage,
    'Permission',
    'Permissions.Delivery.View',
  ),
  'product-management',
)

export default ReleasePackagesPageWithAuthorization
