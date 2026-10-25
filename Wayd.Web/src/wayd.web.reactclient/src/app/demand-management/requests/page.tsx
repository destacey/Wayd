'use client'

import { PageTitle } from '@/src/components/common'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetIntakeRequestsQuery,
  useGetNeedByBasesQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { FC, useEffect } from 'react'
import IntakeRequestsGrid from '../_components/intake-requests-grid'

/**
 * Every request the reader may see, across desks, so a requester can check whether an ask already exists
 * before raising it. Lists carry only the summary, which is what lets them span summary-only desks.
 */
const IntakeRequestsPage: FC = () => {
  useDocumentTitle('Requests')
  const messageApi = useMessage()

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
