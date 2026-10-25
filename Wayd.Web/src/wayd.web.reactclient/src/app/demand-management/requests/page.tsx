'use client'

import { PageTitle } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle, useLinkedEmployee } from '@/src/hooks'
import {
  useGetIntakeRequestsQuery,
  useGetNeedByBasesQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { Button, Flex } from 'antd'
import Link from 'next/link'
import { FC, useEffect, useState } from 'react'
import CreateIntakeDeskForm from '../_components/create-intake-desk-form'
import IntakeRequestsGrid from '../_components/intake-requests-grid'

/**
 * Every request the reader may see, across desks, so a requester can check whether an ask already exists
 * before raising it. Lists carry only the summary, which is what lets them span summary-only desks.
 */
const IntakeRequestsPage: FC = () => {
  useDocumentTitle('Requests')
  const messageApi = useMessage()
  const { hasPermissionClaim } = useAuth()
  const { hasLinkedEmployee } = useLinkedEmployee()
  const canSubmit =
    hasPermissionClaim('Permissions.IntakeRequests.Create') && hasLinkedEmployee

  // Until desk administration exists, this is where an administrator creates the first desk.
  const canCreateDesk = hasPermissionClaim(
    'Permissions.IntakeRequests.Administer',
  )
  const [openCreateDesk, setOpenCreateDesk] = useState(false)

  const { data, isLoading, error, refetch } = useGetIntakeRequestsQuery()
  const { data: needByBases } = useGetNeedByBasesQuery()

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load requests.')
    }
  }, [error, messageApi])

  return (
    <div className="page-gutters">
      <PageTitle
        title="Requests"
        tooltip="Every request you can see, on every desk. Search here before submitting to find out whether someone has already asked."
        actions={
          canSubmit || canCreateDesk ? (
            <Flex gap={8} wrap>
              {canSubmit && (
                <Link href="/demand-management/submit">
                  <Button>Submit a Request</Button>
                </Link>
              )}
              {canCreateDesk && (
                <Button onClick={() => setOpenCreateDesk(true)}>
                  Create Desk
                </Button>
              )}
            </Flex>
          ) : null
        }
      />
      <IntakeRequestsGrid
        requests={data}
        isLoading={isLoading}
        refetch={refetch}
        needByBases={needByBases}
        persistStateKey="demand-management-requests"
        csvFileName="requests"
        emptyMessage="No requests found"
      />
      {openCreateDesk && (
        <CreateIntakeDeskForm
          onFormComplete={() => setOpenCreateDesk(false)}
          onFormCancel={() => setOpenCreateDesk(false)}
        />
      )}
    </div>
  )
}

const IntakeRequestsPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    IntakeRequestsPage,
    'Permission',
    'Permissions.IntakeRequests.View',
  ),
  'demand-management',
)

export default IntakeRequestsPageWithAuthorization
