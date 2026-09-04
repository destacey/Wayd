'use client'

import { PageTitle } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetVersionsQuery } from '@/src/store/features/product-management/versions-api'
import { Button, Space } from 'antd'
import { FC, useEffect, useState } from 'react'
import {
  ImportVersionsForm,
  PlanVersionForm,
  VersionsGrid,
} from './_components'

const VersionsPage: FC = () => {
  useDocumentTitle('Versions')
  const [openPlanVersionForm, setOpenPlanVersionForm] = useState<boolean>(false)
  const [openImportVersionsForm, setOpenImportVersionsForm] =
    useState<boolean>(false)
  const messageApi = useMessage()

  const { hasPermissionClaim } = useAuth()
  const canCreateVersion = hasPermissionClaim('Permissions.Delivery.Create')
  const canImportVersions = hasPermissionClaim('Permissions.Delivery.Import')

  const {
    data: versionData,
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

  const actions =
    !canCreateVersion && !canImportVersions ? null : (
      <Space>
        {canImportVersions && (
          <Button onClick={() => setOpenImportVersionsForm(true)}>Import</Button>
        )}
        {canCreateVersion && (
          <Button onClick={() => setOpenPlanVersionForm(true)}>
            Add Version
          </Button>
        )}
      </Space>
    )

  const onPlanVersionFormClosed = (wasPlanned: boolean) => {
    setOpenPlanVersionForm(false)
    if (wasPlanned) {
      refetch()
    }
  }

  const onImportVersionsFormClosed = (wasImported: boolean) => {
    setOpenImportVersionsForm(false)
    if (wasImported) {
      refetch()
    }
  }

  return (
    <div className="page-gutters">
      <PageTitle title="Versions" actions={actions} />
      <VersionsGrid
        versions={versionData ?? []}
        isLoading={isLoading}
        refetch={refetch}
        persistStateKey="product-management-versions"
      />
      {openImportVersionsForm && (
        <ImportVersionsForm
          onFormComplete={() => onImportVersionsFormClosed(true)}
          onFormCancel={() => onImportVersionsFormClosed(false)}
        />
      )}
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
