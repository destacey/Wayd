'use client'

import { PageTitle } from '@/src/components/common'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { ProductTagCategoryDto } from '@/src/services/wayd-api'
import { useGetProductTagCategoriesQuery } from '@/src/store/features/product-management/product-tag-categories-api'
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

  const columns = useMemo<ColumnDef<ProductTagCategoryDto, any>[]>(
    () => [
      // First, so it stays put as columns are shown, hidden or reordered
      // around it — the ⋯ is always in the same place regardless of layout.
      createActionsColumn<ProductTagCategoryDto>({
        getItems: getActionItems,
        ariaLabel: 'Tag category actions',
      }),
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
    [getActionItems],
  )

  const refresh = async () => {
    refetch()
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
