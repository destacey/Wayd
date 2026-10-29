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
import { IntakeRequestTagCategoryDto } from '@/src/services/wayd-api'
import {
  useGetIntakeRequestTagCategoriesQuery,
  useReorderIntakeRequestTagCategoriesMutation,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError } from '@/src/utils'
import { Button, Typography } from 'antd'
import Link from 'next/link'
import { useEffect, useState } from 'react'
import {
  CreateTagAxisForm,
  FreeTagsList,
  useTagAxisActions,
} from './_components'

const { Paragraph, Title } = Typography

/**
 * The axes reviewers file requests along, with each axis's tags one click
 * behind it. Requests list their tags in the order set here.
 */
const TagAxesPage = () => {
  useDocumentTitle('Demand Management - Tag Axes')
  const [openCreateForm, setOpenCreateForm] = useState(false)
  const messageApi = useMessage()

  // Inactive as well: a settings screen manages what a picker hides.
  const {
    data: axes,
    isLoading,
    error,
    refetch,
  } = useGetIntakeRequestTagCategoriesQuery({ includeInactive: true })
  const [reorderTagAxes] = useReorderIntakeRequestTagCategoriesMutation()
  const { getActionItems, dialogs } = useTagAxisActions()

  useEffect(() => {
    if (error) {
      messageApi.error(
        toApiError(error).detail ??
          'An error occurred while loading the tag axes',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const columns = (
    context: GridColumnContext,
  ): ColumnDef<IntakeRequestTagCategoryDto, any>[] => [
    createActionsColumn<IntakeRequestTagCategoryDto>({
      getItems: getActionItems,
      ariaLabel: 'Tag axis actions',
      size: 70,
      leading: (
        <DragHandleCell
          isDragEnabled={context.isDragEnabled}
          disabledTooltip="Clear sorting, filters, and search to reorder tag axes."
        />
      ),
    }),
    // Read-only: drag-and-drop writes it.
    {
      id: 'order',
      accessorKey: 'order',
      header: 'Order',
      size: 90,
      enableColumnFilter: false,
    },
    {
      id: 'name',
      accessorKey: 'name',
      header: 'Name',
      size: 240,
      cell: ({ row }) => (
        <Link href={`/settings/demand-management/tag-axes/${row.original.id}`}>
          {row.original.name}
        </Link>
      ),
    },
    {
      id: 'description',
      accessorKey: 'description',
      header: 'Description',
      size: 320,
    },
    {
      id: 'tagCount',
      header: 'Tags',
      accessorFn: (axis) => axis.tags?.length ?? 0,
      size: 90,
    },
    {
      id: 'allowsMany',
      accessorKey: 'allowsMany',
      header: 'Allows Many',
      size: 130,
      meta: { columnType: 'yesNo' },
    },
    {
      id: 'isActive',
      accessorKey: 'isActive',
      header: 'Active',
      size: 100,
      meta: { columnType: 'yesNo' },
    },
    // Platform-defined axes are read-only, so the column explains why a row's
    // ⋯ offers less than its neighbour's.
    {
      id: 'isSystem',
      accessorKey: 'isSystem',
      header: 'System',
      size: 100,
      meta: { columnType: 'yesNo' },
    },
  ]

  // The whole list travels, as the API requires; the grid allows dragging only
  // when nothing is sorted, filtered or searched.
  const onRowReorder = async (
    event: RowReorderEvent<IntakeRequestTagCategoryDto>,
  ) => {
    try {
      await reorderTagAxes({
        orderedCategoryIds: event.orderedData.map((axis) => axis.id),
      }).unwrap()
    } catch (error) {
      messageApi.error(
        toApiError(error).detail ??
          'An error occurred while reordering the tag axes',
      )
      refetch()
    }
  }

  return (
    <div className="page-gutters">
      <PageTitle
        title="Tag Axes"
        actions={
          <Button onClick={() => setOpenCreateForm(true)}>Add Tag Axis</Button>
        }
      />
      <Paragraph type="secondary">
        The axes reviewers file requests along, and the tags on each. Deactivate
        an axis or a tag to stop offering it; requests already tagged keep their
        tags.
      </Paragraph>

      <WaydGrid
        columns={columns}
        data={axes ?? []}
        onRefresh={async () => {
          refetch()
        }}
        isLoading={isLoading}
        persistStateKey="settings-intake-request-tag-axes"
        csvFileName="intake-request-tag-axes"
        getRowId={(axis: IntakeRequestTagCategoryDto) => axis.id}
        onRowReorder={onRowReorder}
      />

      {dialogs}

      <Title level={4}>Free Tags</Title>
      <Paragraph type="secondary">
        Tags agents add by typing a name, on desks that allow free tags. Rename
        one, merge near-duplicates together, or promote one onto an axis by
        adding the tag there and merging into it. A free tag no request carries
        is removed on its own.
      </Paragraph>
      <FreeTagsList />

      {openCreateForm && (
        <CreateTagAxisForm
          onFormComplete={() => setOpenCreateForm(false)}
          onFormCancel={() => setOpenCreateForm(false)}
        />
      )}
    </div>
  )
}

const TagAxesPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    TagAxesPage,
    'Permission',
    'Permissions.IntakeRequests.Administer',
  ),
  'demand-management',
)

export default TagAxesPageWithAuthorization
