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
import BoardOutlined from '@/src/components/common/icons/board-outlined'
import { MenuOutlined } from '@ant-design/icons'
import { Button, ConfigProvider, Flex, Grid, Segmented, Skeleton } from 'antd'
import Link from 'next/link'
import { usePathname, useRouter, useSearchParams } from 'next/navigation'
import { FC, Suspense, useEffect, useRef } from 'react'
import IntakeRequestsGrid from '../_components/intake-requests-grid'
import { TOUCH_THEME } from '../_components/touch-theme'
import RequestBoardView from './_components/request-board-view'
import RequestFilterBar from './_components/request-filter-bar'
import {
  DEFAULT_REQUEST_FILTERS,
  MY_DESKS,
  RequestFilters,
  RequestView,
  applyRequestFilters,
  fromSearchParams,
  resolveDeskIds,
  toSearchParams,
} from './_components/request-filters'

const { useBreakpoint } = Grid

/**
 * Where reviewers work from, and where anyone checks whether an ask already exists before raising it. It
 * opens on the requests still open; "Everything" adds the closed ones. Lists carry only the summary, which
 * is what lets them span summary-only desks, and a private desk's requests are absent for anyone outside it.
 *
 * The filters and the layout live in the address, so any view can be bookmarked or sent to someone. Someone
 * who works a desk opens on the requests of their own desks; everyone else on every open request.
 */
const IntakeRequestsPage: FC = () => {
  useDocumentTitle('Requests')
  const messageApi = useMessage()
  const { employeeId, hasLinkedEmployee } = useLinkedEmployee()
  const canSubmit = hasLinkedEmployee
  const router = useRouter()
  const pathname = usePathname()
  const searchParams = useSearchParams()
  const phone = useBreakpoint().md === false
  const fromAddress = fromSearchParams(searchParams)
  const view = fromAddress.view
  // A shared "Assigned to me" link means nothing to a reader with no employee record, who is assigned
  // nothing; read it as anyone rather than show an empty list under a filter they cannot choose.
  const filters: RequestFilters =
    !employeeId && fromAddress.filters.assignee === 'me'
      ? { ...fromAddress.filters, assignee: 'anyone' }
      : fromAddress.filters
  const navigate = (next: RequestFilters, nextView: RequestView) => {
    const query = toSearchParams(next, nextView).toString()
    router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false })
  }
  const setFilters = (next: RequestFilters) => navigate(next, view)

  // `currentData` rather than `data`: switching scope must not show, or count, the previous scope's set
  // while the new one loads.
  const {
    currentData: data,
    isFetching,
    error,
    refetch,
  } = useGetIntakeRequestsQuery({ openOnly: filters.scope === 'open' })
  const isLoading = isFetching && !data
  const { data: needByBases } = useGetNeedByBasesQuery()
  const { data: desks } = useGetIntakeDesksQuery()
  const { data: channels } = useGetIntakeChannelsQuery()
  const myDeskIds = (desks ?? [])
    .filter((d) => d.members.some((m) => m.employee.id === employeeId))
    .map((d) => d.id)

  // An address with no filters means "where I start": for someone who works a desk, their own desks.
  // Decided once, when the desks arrive, so clearing the filters afterwards really shows everything.
  const startDecided = useRef(false)
  const opensBare = searchParams.toString() === ''
  useEffect(() => {
    if (startDecided.current || !desks) return
    startDecided.current = true
    if (opensBare && myDeskIds.length > 0)
      navigate({ ...DEFAULT_REQUEST_FILTERS, deskIds: [MY_DESKS] }, view)
  })

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load requests.')
    }
  }, [error, messageApi])

  const visible = data
    ? applyRequestFilters(data, filters, employeeId, myDeskIds)
    : data

  const viewSelector = (
    <ConfigProvider theme={phone ? TOUCH_THEME : undefined}>
      <Segmented<RequestView>
        value={view}
        onChange={(next) => navigate(filters, next)}
        options={[
          {
            value: 'list',
            icon: <MenuOutlined alt="List" title="List" />,
          },
          {
            value: 'board',
            icon: <BoardOutlined alt="Board" title="Board" />,
          },
        ]}
        aria-label="Layout"
      />
    </ConfigProvider>
  )

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
        employeeId={employeeId}
        myDeskIds={myDeskIds}
      />
      {view === 'list' ? (
        <IntakeRequestsGrid
          requests={visible}
          isLoading={isLoading}
          refetch={refetch}
          needByBases={needByBases}
          persistStateKey="demand-management-requests"
          csvFileName="requests"
          emptyMessage="No requests match these filters"
          rightSlot={viewSelector}
        />
      ) : (
        <>
          <Flex justify="end" align="center" style={{ paddingBottom: 8 }}>
            {viewSelector}
          </Flex>
          <RequestBoardView
            requests={visible}
            filters={{
              ...filters,
              deskIds: resolveDeskIds(filters.deskIds, myDeskIds),
            }}
            desks={desks}
            onPickDesk={(deskId) =>
              setFilters({ ...filters, deskIds: [deskId] })
            }
          />
        </>
      )}
    </div>
  )
}

// The page reads its filters from the address, which a statically rendered route reads only under Suspense.
const IntakeRequestsPageWithSuspense: FC = () => (
  <Suspense fallback={<Skeleton active paragraph={{ rows: 8 }} />}>
    <IntakeRequestsPage />
  </Suspense>
)

// No permission gate: the server lists what the reader may see, which View widens but does not decide.
const IntakeRequestsPageWithAuthorization = requireFeatureFlag(
  IntakeRequestsPageWithSuspense,
  'demand-management',
)

export default IntakeRequestsPageWithAuthorization
