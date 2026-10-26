'use client'

import { PageTitle } from '@/src/components/common'
import { useMessage } from '@/src/components/contexts/messaging'
import { requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle, useLinkedEmployee } from '@/src/hooks'
import {
  useGetIntakeRequestsQuery,
  useGetNeedByBasesQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { Alert, Button } from 'antd'
import Link from 'next/link'
import { FC, useEffect } from 'react'
import IntakeRequestsGrid from '../_components/intake-requests-grid'

/**
 * The requests the signed-in employee asked for or submitted for someone else. Filtered on the server,
 * because who submitted a request is not in the summaries a list returns.
 */
const MyIntakeRequestsPage: FC = () => {
  useDocumentTitle('My Requests')
  const messageApi = useMessage()
  const { hasLinkedEmployee } = useLinkedEmployee()
  const canSubmit = hasLinkedEmployee

  const { data, isLoading, error, refetch } = useGetIntakeRequestsQuery(
    { mine: true },
    { skip: !hasLinkedEmployee },
  )
  const { data: needByBases } = useGetNeedByBasesQuery()

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load your requests.')
    }
  }, [error, messageApi])

  return (
    <div className="page-gutters">
      <PageTitle
        title="My Requests"
        tooltip="Requests you asked for, and requests you submitted on someone else's behalf."
        actions={
          canSubmit && hasLinkedEmployee ? (
            <Link href="/demand-management/submit">
              <Button>Submit a Request</Button>
            </Link>
          ) : null
        }
      />
      {hasLinkedEmployee ? (
        <IntakeRequestsGrid
          requests={data}
          isLoading={isLoading}
          refetch={refetch}
          needByBases={needByBases}
          persistStateKey="demand-management-my-requests"
          csvFileName="my-requests"
          emptyMessage="You have no requests yet"
        />
      ) : (
        <Alert
          type="info"
          showIcon
          title="Your account is not linked to an employee record."
          description="Requests are made by and for employees, so there are none to show until an administrator links your account."
        />
      )}
    </div>
  )
}

// Following your own requests needs no permission: the server returns only what is yours.
const MyIntakeRequestsPageWithAuthorization = requireFeatureFlag(
  MyIntakeRequestsPage,
  'demand-management',
)

export default MyIntakeRequestsPageWithAuthorization
