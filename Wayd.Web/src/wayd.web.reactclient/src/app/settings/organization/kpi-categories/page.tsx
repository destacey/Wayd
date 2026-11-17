'use client'

import { PageTitle } from '@/src/components/common'
import {
  DragHandleCell,
  WaydGrid,
  type GridColumnContext,
  type RowReorderEvent,
} from '@/src/components/common/wayd-grid'
import {
  createActionsColumn,
  type ColumnDef,
} from '@/src/components/common/wayd-grid-core'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useMessage } from '@/src/components/contexts/messaging'
import { useDocumentTitle } from '@/src/hooks'
import { KpiCategoryDto } from '@/src/services/api/wayd.schemas'
import {
  useGetKpiCategoriesQuery,
  useReorderKpiCategoriesMutation,
} from '@/src/store/features/common/kpis-api'
import { toApiError } from '@/src/utils'
import { Button, Typography } from 'antd'
import { useEffect, useState } from 'react'
import KpiCategoryForm from './_components/kpi-category-form'
import useKpiCategoryActions from './_components/use-kpi-category-actions'

const { Paragraph } = Typography

/**
 * The organization's KPI categories, which every scorecard groups its KPIs by,
 * in this order.
 */
const KpiCategoriesPage = () => {
  useDocumentTitle('Organization - KPI Categories')
  const [openCreateForm, setOpenCreateForm] = useState(false)
  const messageApi = useMessage()

  // Inactive as well: a settings screen manages what a picker hides.
  const {
    data: categories,
    isLoading,
    error,
    refetch,
  } = useGetKpiCategoriesQuery({ includeInactive: true })
  const [reorderCategories] = useReorderKpiCategoriesMutation()
  const { getActionItems, dialogs } = useKpiCategoryActions()

  useEffect(() => {
    if (error) {
      messageApi.error(
        toApiError(error).detail ??
          'An error occurred while loading the KPI categories',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const columns = (
    context: GridColumnContext,
  ): ColumnDef<KpiCategoryDto, any>[] => [
    createActionsColumn<KpiCategoryDto>({
      getItems: getActionItems,
      ariaLabel: 'Category actions',
      size: 70,
      leading: (
        <DragHandleCell
          isDragEnabled={context.isDragEnabled}
          disabledTooltip="Clear sorting, filters, and search to reorder categories."
        />
      ),
    }),
    {
      id: 'order',
      accessorKey: 'order',
      header: 'Order',
      size: 90,
      enableColumnFilter: false,
    },
    { id: 'name', accessorKey: 'name', header: 'Name', size: 240 },
    {
      id: 'description',
      accessorKey: 'description',
      header: 'Description',
      size: 420,
    },
    {
      id: 'kpiCount',
      accessorKey: 'kpiCount',
      header: 'KPIs',
      size: 90,
      enableColumnFilter: false,
    },
    {
      id: 'isActive',
      accessorKey: 'isActive',
      header: 'Active',
      size: 100,
      meta: { columnType: 'yesNo' },
    },
  ]

  // The whole list travels, as the API requires; the grid allows dragging only
  // when nothing is sorted, filtered or searched.
  const onRowReorder = async (event: RowReorderEvent<KpiCategoryDto>) => {
    try {
      await reorderCategories(event.orderedData.map((c) => c.id)).unwrap()
    } catch (error) {
      messageApi.error(
        toApiError(error).detail ??
          'An error occurred while reordering the KPI categories',
      )
      refetch()
    }
  }

  return (
    <div className="page-gutters">
      <PageTitle
        title="KPI Categories"
        actions={
          <Button onClick={() => setOpenCreateForm(true)}>Add Category</Button>
        }
      />
      <Paragraph type="secondary">
        How scorecards group KPIs, in this order — Flow, Quality, People.
        Deactivate a category to stop offering it; KPIs that have it keep it.
      </Paragraph>

      <WaydGrid
        columns={columns}
        data={categories ?? []}
        onRefresh={async () => {
          refetch()
        }}
        isLoading={isLoading}
        persistStateKey="settings-kpi-categories"
        csvFileName="kpi-categories"
        getRowId={(category: KpiCategoryDto) => category.id}
        onRowReorder={onRowReorder}
      />

      {dialogs}

      {openCreateForm && (
        <KpiCategoryForm
          onFormComplete={() => setOpenCreateForm(false)}
          onFormCancel={() => setOpenCreateForm(false)}
        />
      )}
    </div>
  )
}

const KpiCategoriesPageWithAuthorization = requireFeatureFlag(
  authorizePage(KpiCategoriesPage, 'Permission', 'Permissions.Kpis.Administer'),
  'kpis',
)

export default KpiCategoriesPageWithAuthorization
