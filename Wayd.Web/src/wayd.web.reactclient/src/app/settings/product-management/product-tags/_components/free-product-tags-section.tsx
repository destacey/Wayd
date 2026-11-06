'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import {
  createActionsColumn,
  type ColumnDef,
} from '@/src/components/common/wayd-grid-core'
import useAuth from '@/src/components/contexts/auth'
import { ProductTagListItemDto } from '@/src/services/wayd-api'
import {
  useGetProductTagsSettingsQuery,
  useSearchProductTagsQuery,
} from '@/src/store/features/product-management/product-tags-api'
import { FreeTagsHeading } from '@/src/components/common/tags'
import { Alert, Button, Typography } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { useState } from 'react'
import DeleteFreeProductTagForm from './delete-free-product-tag-form'
import EditFreeProductTagForm from './edit-free-product-tag-form'
import MergeProductTagForm from './merge-product-tag-form'
import ProductTagSettingsForm from './product-tag-settings-form'
import ProductTagSettingsHistoryDrawer from './product-tag-settings-history-drawer'

/** The dialogs a free tag can open. One value, not one boolean each. */
type DialogId = 'edit' | 'merge' | 'delete'

/**
 * Builds the row actions a viewer may take on a free tag. Rename and merge need
 * Update; delete needs Delete, as for tags in a category.
 */
export const freeTagActionItems = (
  tag: ProductTagListItemDto,
  permissions: { canUpdate: boolean; canDelete: boolean },
  open: (dialog: DialogId, tag: ProductTagListItemDto) => void,
): ItemType[] => {
  const items: ItemType[] = []
  if (permissions.canUpdate) {
    items.push(
      { key: 'edit', label: 'Edit', onClick: () => open('edit', tag) },
      { key: 'merge', label: 'Merge into…', onClick: () => open('merge', tag) },
    )
  }
  if (permissions.canDelete) {
    items.push({
      key: 'delete',
      label: 'Delete',
      danger: true,
      onClick: () => open('delete', tag),
    })
  }
  return items
}

/**
 * The free tags people have added to products — tags in no category — for an
 * admin to rename, fold together, promote into a category or delete, with the
 * setting that allows them.
 *
 * Free tags nothing carries are deleted on their own, so every row here is in
 * use somewhere. The setting is a system setting: its switch and history show
 * only to a viewer who can see system settings, and anyone else is told
 * whether free tags are on.
 */
const FreeProductTagsSection = () => {
  const [active, setActive] = useState<{
    dialog: DialogId
    tag: ProductTagListItemDto
  } | null>(null)

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim(
    'Permissions.ProductTagCategories.Update',
  )
  const canDelete = hasPermissionClaim(
    'Permissions.ProductTagCategories.Delete',
  )
  const canViewSettings = hasPermissionClaim('Permissions.SystemSettings.View')
  const canUpdateSettings = hasPermissionClaim(
    'Permissions.SystemSettings.Update',
  )
  const [isHistoryOpen, setIsHistoryOpen] = useState(false)

  const {
    data: freeTags,
    isLoading,
    isError,
    refetch,
  } = useSearchProductTagsQuery({
    isFree: true,
  })
  const {
    data: settings,
    isLoading: isLoadingSettings,
    isError: isSettingsError,
  } = useGetProductTagsSettingsQuery()

  const open = (dialog: DialogId, tag: ProductTagListItemDto) =>
    setActive({ dialog, tag })
  const close = () => setActive(null)

  // No actions column for a viewer who can take none, rather than a menu with nothing in it.
  const columns: ColumnDef<ProductTagListItemDto, any>[] = [
    ...(canUpdate || canDelete
      ? [
          createActionsColumn<ProductTagListItemDto>({
            getItems: (tag) =>
              freeTagActionItems(tag, { canUpdate, canDelete }, open),
            ariaLabel: 'Free tag actions',
            size: 70,
          }),
        ]
      : []),
    { id: 'name', accessorKey: 'name', header: 'Name' },
    {
      id: 'description',
      accessorKey: 'description',
      header: 'Description',
      size: 320,
    },
    {
      id: 'productCount',
      accessorKey: 'productCount',
      header: 'Products',
      size: 110,
    },
  ]

  const status =
    settings === undefined ? null : settings.allowsFreeTags ? (
      <>Free tags are on: anyone who can tag a product can add one.</>
    ) : (
      <>
        Free tags are off: no new ones can be added, and products keep the ones
        they have.
      </>
    )

  // An unloaded setting would show as an unchecked switch, which reads as off.
  const setting = isSettingsError ? (
    <Alert
      type="error"
      showIcon
      title="Could not load the product tag settings. Reload the page to try again."
    />
  ) : canViewSettings ? (
    <ProductTagSettingsForm
      settings={settings}
      isLoading={isLoadingSettings}
      canUpdate={canUpdateSettings}
    />
  ) : (
    <Typography.Paragraph>{status}</Typography.Paragraph>
  )

  return (
    <>
      <FreeTagsHeading
        description="Informal labels in no category, added by typing a name while tagging. Merge one into a tag in a category to promote it."
        extra={
          canViewSettings && (
            <Button type="link" onClick={() => setIsHistoryOpen(true)}>
              Setting history
            </Button>
          )
        }
      />
      {setting}

      {/* Without this a failed load reads as no free tags at all. */}
      {isError && (
        <Alert
          type="error"
          showIcon
          title="Could not load the free tags. Use refresh to try again."
        />
      )}

      <WaydGrid
        columns={columns}
        data={freeTags ?? []}
        onRefresh={async () => {
          refetch()
        }}
        isLoading={isLoading}
        persistStateKey="settings-product-free-tags"
        csvFileName="product-free-tags"
        getRowId={(tag: ProductTagListItemDto) => tag.id}
      />

      {active?.dialog === 'edit' && (
        <EditFreeProductTagForm
          tag={active.tag}
          onFormComplete={close}
          onFormCancel={close}
        />
      )}
      {active?.dialog === 'merge' && (
        <MergeProductTagForm
          tag={active.tag}
          onFormComplete={close}
          onFormCancel={close}
        />
      )}
      {active?.dialog === 'delete' && (
        <DeleteFreeProductTagForm
          tag={active.tag}
          onFormComplete={close}
          onFormCancel={close}
        />
      )}
      {canViewSettings && (
        <ProductTagSettingsHistoryDrawer
          open={isHistoryOpen}
          onClose={() => setIsHistoryOpen(false)}
        />
      )}
    </>
  )
}

export default FreeProductTagsSection
