'use client'

import { PageTitle } from '@/src/components/common'
import {
  DragHandleCell,
  WaydGrid,
  type GridColumnContext,
  type RowReorderEvent,
} from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { ProductTagCategoryDto } from '@/src/services/wayd-api'
import {
  useGetProductTagCategoriesQuery,
  useReorderProductTagCategoriesMutation,
} from '@/src/store/features/product-management/product-tag-categories-api'
import {
  createActionsColumn,
  type ColumnDef,
} from '@/src/components/common/wayd-grid-core'
import { Button } from 'antd'
import Link from 'next/link'
import { useEffect, useMemo, useState } from 'react'
import {
  CreateProductTagCategoryForm,
  useProductTagCategoryActions,
} from './_components'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError } from '@/src/utils'

/**
 * The tag axes, with their tags one click behind each.
 *
 * A list-and-record pair rather than a config panel: a category's content is
 * its tags, which is a list of its own, and a panel beside the grid is for
 * records that have nothing to say beyond their own fields.
 */
const ProductTagCategoriesPage = () => {
  useDocumentTitle('Product Management - Product Tags')
  const [openCreateForm, setOpenCreateForm] = useState<boolean>(false)

  const messageApi = useMessage()

  // Both active and inactive: a settings screen manages what a picker hides.
  const {
    data: categories,
    isLoading,
    error,
    refetch,
  } = useGetProductTagCategoriesQuery(undefined)

  const { hasPermissionClaim } = useAuth()
  const canCreate = hasPermissionClaim('Permissions.ProductTagCategories.Create')
  const canReorder = hasPermissionClaim('Permissions.ProductTagCategories.Update')

  const [reorderProductTagCategories] = useReorderProductTagCategoriesMutation()

  const { getActionItems, dialogs } = useProductTagCategoryActions()

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading product tag categories',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const columns = useMemo(
    () =>
      (
        context: GridColumnContext,
      ): ColumnDef<ProductTagCategoryDto, any>[] => [
      // First, so it stays put as columns are shown, hidden or reordered
      // around it — the ⋯ is always in the same place regardless of layout.
      // The grab handle rides in the same cell rather than taking a column of
      // its own, which would spend width on one icon.
      createActionsColumn<ProductTagCategoryDto>({
        getItems: getActionItems,
        ariaLabel: 'Tag category actions',
        size: 70,
        leading: canReorder ? (
          <DragHandleCell
            isDragEnabled={context.isDragEnabled}
            disabledTooltip="Clear sorting, filters, and search to reorder tag categories."
          />
        ) : undefined,
      }),
      // The position drag-and-drop writes. Read-only — it is not on the forms,
      // because a number each edit has to guess right is how two axes end up
      // claiming the same place.
      {
        id: 'order',
        accessorKey: 'order',
        header: 'Order',
        size: 90,
        enableColumnFilter: false,
      },
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        cell: ({ row }) => (
          <Link href={`./product-tags/${row.original.key}`}>
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
        accessorFn: (category) => category.tags?.length ?? 0,
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
      // Platform-seeded axes are read-only, so the column explains why a row's
      // ⋯ offers less than its neighbour's.
      {
        id: 'isSystem',
        accessorKey: 'isSystem',
        header: 'System',
        size: 100,
        meta: { columnType: 'yesNo' },
      },
    ],
    [getActionItems, canReorder],
  )

  const refresh = async () => {
    refetch()
  }

  // The whole displayed set travels, which is what the API requires — and the
  // grid only enables dragging when nothing is sorted, filtered or searched, so
  // "displayed" is the full list in data order.
  const onRowReorder = async (event: RowReorderEvent<ProductTagCategoryDto>) => {
    try {
      await reorderProductTagCategories({
        orderedCategoryIds: event.orderedData.map((category) => category.id),
      }).unwrap()
    } catch (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while reordering the tag categories',
      )
      refetch()
    }
  }

  const actions = canCreate ? (
    <Button onClick={() => setOpenCreateForm(true)}>Create Tag Category</Button>
  ) : null

  const onCreateFormClosed = (wasCreated: boolean) => {
    setOpenCreateForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  return (
    <div className="page-gutters">
      <PageTitle title="Product Tags" actions={actions} />

      <WaydGrid
        columns={columns}
        data={categories ?? []}
        onRefresh={refresh}
        isLoading={isLoading}
        persistStateKey="settings-product-tag-categories"
        csvFileName="product-tag-categories"
        getRowId={(category: ProductTagCategoryDto) => category.id}
        onRowReorder={canReorder ? onRowReorder : undefined}
      />

      {dialogs}

      {openCreateForm && (
        <CreateProductTagCategoryForm
          onFormComplete={() => onCreateFormClosed(true)}
          onFormCancel={() => onCreateFormClosed(false)}
        />
      )}
    </div>
  )
}

const ProductTagCategoriesPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    ProductTagCategoriesPage,
    'Permission',
    'Permissions.ProductTagCategories.View',
  ),
  'product-management',
)

export default ProductTagCategoriesPageWithAuthorization
