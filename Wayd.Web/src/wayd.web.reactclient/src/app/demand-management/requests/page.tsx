'use client'

import { PageTitle } from '@/src/components/common'
import { useMessage } from '@/src/components/contexts/messaging'
import { requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle, useLinkedEmployee } from '@/src/hooks'
import {
  useGetIntakeChannelsQuery,
  useGetIntakeDesksQuery,
  useGetIntakeRequestsQuery,
  useGetNeedByBasesQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { Button } from 'antd'
import Link from 'next/link'
import { FC, useEffect, useState } from 'react'
import IntakeRequestsGrid from '../_components/intake-requests-grid'
import RequestFilterBar from './_components/request-filter-bar'
import {
  DEFAULT_REQUEST_FILTERS,
  RequestFilters,
  applyRequestFilters,
} from './_components/request-filters'

/**
 * Where reviewers work from, and where anyone checks whether an ask already exists before raising it. It
 * opens on the requests still open; "Everything" adds the closed ones. Lists carry only the summary, which
 * is what lets them span summary-only desks, and a private desk's requests are absent for anyone outside it.
 */
const IntakeRequestsPage: FC = () => {
  useDocumentTitle('Requests')
  const messageApi = useMessage()
  const { employeeId, hasLinkedEmployee } = useLinkedEmployee()
  const canSubmit = hasLinkedEmployee
  const [filters, setFilters] = useState<RequestFilters>(
    DEFAULT_REQUEST_FILTERS,
  )

  const { data, isLoading, error, refetch } = useGetIntakeRequestsQuery({
    openOnly: filters.scope === 'open',
  })
  const { data: needByBases } = useGetNeedByBasesQuery()
  const { data: desks } = useGetIntakeDesksQuery()
  const { data: channels } = useGetIntakeChannelsQuery()

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load requests.')
    }
  }, [error, messageApi])

  const visible = data ? applyRequestFilters(data, filters, employeeId) : data

  return (
    <div className="page-gutters">
      <PageTitle
        title="Requests"
        tooltip="Every request you can see, on every desk. Search here before submitting to find out whether someone has already asked."
        actions={
          canSubmit ? (
            <Link href="/demand-management/submit">
              <Button>Submit a Request</Button>
            </Link>
          ) : null
        }
      />
      <RequestFilterBar
        filters={filters}
        onChange={setFilters}
        requests={data}
        desks={desks}
        channels={channels}
        hasLinkedEmployee={hasLinkedEmployee}
      />
      <IntakeRequestsGrid
        requests={visible}
        isLoading={isLoading}
        refetch={refetch}
        needByBases={needByBases}
        persistStateKey="demand-management-requests"
        csvFileName="requests"
        emptyMessage="No requests match these filters"
      />
    </div>
  )
}

// No permission gate: the server lists what the reader may see, which View widens but does not decide.
const IntakeRequestsPageWithAuthorization = requireFeatureFlag(
  IntakeRequestsPage,
  'demand-management',
)

export default IntakeRequestsPageWithAuthorization
