'use client'

import { PageTitle } from '@/src/components/common'
import {
  ConfigListPanel,
  useSelectedRecord,
} from '@/src/components/common/config-list'
import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { EstimationScaleDto } from '@/src/services/wayd-api'
import { useGetEstimationScalesQuery } from '@/src/store/features/planning/estimation-scales-api'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { Button } from 'antd'
import { Suspense, useEffect, useState } from 'react'
import {
  CreateEstimationScaleForm,
  EstimationScalePanel,
  useEstimationScaleActions,
} from './_components'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError } from '@/src/utils'

const EstimationScalesPage = () => {
  useDocumentTitle('Planning - Estimation Scales')
  const [openCreateForm, setOpenCreateForm] = useState<boolean>(false)

  const messageApi = useMessage()
  const { selectedId, select, clear } = useSelectedRecord()

  const {
    data: scaleData,
    isLoading,
    error,
    refetch,
  } = useGetEstimationScalesQuery(true)

  // The list DTO is the whole record — values included — so unlike the other
  // config lists there is no second query for the panel to make.
  const selectedScale = scaleData?.find(
    (scale) => String(scale.id) === selectedId,
  )

  const { hasPermissionClaim } = useAuth()
  const canCreateEstimationScale = hasPermissionClaim(
    'Permissions.EstimationScales.Create',
  )

  const { getActionItems, dialogs } = useEstimationScaleActions({
    onChanged: () => refetch(),
    onDeleted: (deletedId) => {
      // Only close the panel if it was showing the record that went — deleting
      // a different row from its own ⋯ should leave the panel alone.
      if (String(deletedId) === selectedId) {
        clear()
      }
      refetch()
    },
  })

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading estimation scales',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const columns: ColumnDef<EstimationScaleDto, any>[] = [
    // First, so it stays put as columns are shown, hidden or reordered around
    // it. Its button is excluded from row activation, so opening the menu does
    // not also open the record.
    createActionsColumn<EstimationScaleDto>({
      getItems: getActionItems,
      ariaLabel: 'Estimation scale actions',
    }),
    {
      id: 'name',
      accessorKey: 'name',
      header: 'Name',
    },
    {
      id: 'isActive',
      accessorKey: 'isActive',
      header: 'Active',
      size: 100,
      meta: { columnType: 'yesNo' },
    },
    {
      id: 'values',
      accessorFn: (row) => row.values?.length ?? 0,
      header: 'Values',
      size: 100,
      enableColumnFilter: false,
    },
  ]

  const refresh = async () => {
    refetch()
  }

  const actions = !canCreateEstimationScale ? null : (
    <Button onClick={() => setOpenCreateForm(true)}>
      Create Estimation Scale
    </Button>
  )

  const onCreateFormClosed = (wasCreated: boolean) => {
    setOpenCreateForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  return (
    <div className="page-gutters">
      <PageTitle title="Estimation Scales" actions={actions} />

      <ConfigListPanel
        // Not just `selectedId`: a stale id from a pasted link, or a record
        // deleted from another row's menu, would hold the panel open on
        // nothing. The list is the whole record here, so its absence is
        // conclusive once loading has finished.
        open={!!selectedScale || (!!selectedId && isLoading)}
        onClose={clear}
        title={selectedScale?.name}
        details={<EstimationScalePanel estimationScale={selectedScale} />}
        actionItems={selectedScale && getActionItems(selectedScale)}
        isLoading={isLoading}
      >
        <WaydGrid
          columns={columns}
          data={scaleData ?? []}
          onRefresh={refresh}
          isLoading={isLoading}
          persistStateKey="settings-estimation-scales"
          csvFileName="estimation-scales"
          onRowActivate={(scale) => select(String(scale.id))}
          activatedRowId={selectedId}
          getRowActivateLabel={(scale) => scale.name}
        />
      </ConfigListPanel>

      {dialogs}

      {openCreateForm && (
        <CreateEstimationScaleForm
          onFormComplete={() => onCreateFormClosed(true)}
          onFormCancel={() => onCreateFormClosed(false)}
        />
      )}
    </div>
  )
}

const EstimationScalesPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    EstimationScalesPage,
    'Permission',
    'Permissions.EstimationScales.View',
  ),
  'planning-poker',
)

/**
 * `useSelectedRecord` reads the query string, so the page needs a Suspense
 * boundary — Next requires one around any `useSearchParams` consumer or the
 * whole route opts out of static rendering.
 */
const EstimationScalesPageWithSuspense = () => (
  <Suspense>
    <EstimationScalesPageWithAuthorization />
  </Suspense>
)

export default EstimationScalesPageWithSuspense
