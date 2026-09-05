'use client'

import PageTitle from '@/src/components/common/page-title'
import {
  ConfigListPanel,
  useSelectedRecord,
} from '@/src/components/common/config-list'
import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import { Suspense, useState } from 'react'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { authorizePage } from '@/src/components/hoc'
import useAuth from '@/src/components/contexts/auth'
import type { ItemType } from 'antd/es/menu/interface'
import { useDocumentTitle } from '@/src/hooks'
import {
  ControlItemsMenu,
  ControlItemSwitch,
} from '@/src/components/common/control-items-menu'
import { FeatureFlagDto, FeatureFlagListDto } from '@/src/services/wayd-api'
import {
  useGetFeatureFlagQuery,
  useGetFeatureFlagsQuery,
} from '@/src/store/features/admin/feature-flags-api'
import EditFeatureFlagForm from './_components/edit-feature-flag-form'
import FeatureFlagPanel from './_components/feature-flag-panel'
import useFeatureFlagActions from './_components/use-feature-flag-actions'

/** What a feature flag's actions need — the fields both DTOs carry. */
type FeatureFlagActionTarget = Pick<
  FeatureFlagDto,
  'id' | 'name' | 'displayName' | 'isEnabled' | 'isSystem' | 'isArchived'
>

const FeatureFlagsListPage = () => {
  useDocumentTitle('Feature Flags')
  const [editingFlagId, setEditingFlagId] = useState<number | null>(null)
  const [includeArchived, setIncludeArchived] = useState(false)
  const { selectedId, select, clear } = useSelectedRecord()

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.FeatureFlags.Update')
  const canDelete = hasPermissionClaim('Permissions.FeatureFlags.Delete')
  const showRowActions = canUpdate || canDelete

  const {
    data: featureFlags = [],
    isLoading,
    refetch,
  } = useGetFeatureFlagsQuery({ includeArchived })

  // The list omits `description`, so the panel asks for the record. Cached
  // per id, so reopening a row costs nothing.
  const { data: selectedFlag, isLoading: isLoadingSelected } =
    useGetFeatureFlagQuery(Number(selectedId), { skip: !selectedId })

  const { handleToggle, handleArchive } = useFeatureFlagActions()

  const refresh = () => {
    refetch()
  }

  /**
   * One definition for the row's ⋯ and the panel's, so the two menus cannot
   * drift. Takes the fields both DTOs carry rather than either one.
   */
  const rowActionItems = (flag: FeatureFlagActionTarget): ItemType[] => {
    const items: ItemType[] = []

    if (canUpdate) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => setEditingFlagId(flag.id),
      })
      items.push({
        key: 'toggle',
        label: flag.isEnabled ? 'Disable' : 'Enable',
        onClick: () => handleToggle(flag),
      })
    }

    // A system flag is seeded by the product and an archived one is already
    // gone; neither can be archived again.
    if (canDelete && !flag.isSystem && !flag.isArchived) {
      if (items.length > 0) {
        items.push({ key: 'divider', type: 'divider' })
      }
      items.push({
        key: 'archive',
        label: 'Archive',
        danger: true,
        onClick: () => handleArchive(flag),
      })
    }

    return items
  }

  const columns: ColumnDef<FeatureFlagListDto, any>[] = [
    createActionsColumn<FeatureFlagListDto>({
      unavailable: !showRowActions,
      ariaLabel: 'Feature flag actions',
      getItems: rowActionItems,
    }),
    {
      id: 'name',
      accessorKey: 'name',
      header: 'Name',
      size: 250,
    },
    {
      id: 'displayName',
      accessorKey: 'displayName',
      header: 'Display Name',
      size: 250,
    },
    {
      id: 'isSystem',
      accessorFn: (row) => (row.isSystem ? 'System' : 'User'),
      header: 'Type',
      size: 120,
      meta: {
        filterType: 'set',
        filterOptions: [
          { label: 'System', value: 'System' },
          { label: 'User', value: 'User' },
        ],
      },
    },
    {
      id: 'isEnabled',
      accessorKey: 'isEnabled',
      header: 'Enabled',
      meta: { columnType: 'yesNo' },
    },
    // Mode-dependent column: excluded from the defs (not meta.unavailable) so it
    // stays out of the column chooser and persisted layouts; the memo
    // rebuilds when the Include Archived switch flips.
    ...(includeArchived
      ? [
          {
            id: 'isArchived',
            accessorKey: 'isArchived',
            header: 'Archived',
            meta: { columnType: 'yesNo' },
          } satisfies ColumnDef<FeatureFlagListDto, any>,
        ]
      : []),
  ]

  const controlItems: ItemType[] = [
    {
      label: (
        <ControlItemSwitch
          label="Include Archived"
          checked={includeArchived}
          onChange={setIncludeArchived}
        />
      ),
      key: 'include-archived',
      onClick: () => setIncludeArchived((prev) => !prev),
    },
  ]

  return (
    <div className="page-gutters">
      <PageTitle title="Feature Flags" />
      <ConfigListPanel
        open={!!selectedId}
        onClose={clear}
        title={selectedFlag?.displayName}
        details={<FeatureFlagPanel featureFlag={selectedFlag} />}
        actionItems={selectedFlag && rowActionItems(selectedFlag)}
        isLoading={isLoadingSelected}
      >
        <WaydGrid
          columns={columns}
          data={featureFlags}
          onRefresh={refresh}
          isLoading={isLoading}
          persistStateKey="settings-feature-flags"
          csvFileName="feature-flags"
          rightSlot={<ControlItemsMenu items={controlItems} />}
          onRowActivate={(flag) => select(String(flag.id))}
          activatedRowId={selectedId}
          getRowActivateLabel={(flag) => flag.displayName}
        />
      </ConfigListPanel>
      {editingFlagId !== null && (
        <EditFeatureFlagForm
          featureFlagId={editingFlagId}
          onFormSave={() => setEditingFlagId(null)}
          onFormCancel={() => setEditingFlagId(null)}
        />
      )}
    </div>
  )
}

const PageWithAuthorization = authorizePage(
  FeatureFlagsListPage,
  'Permission',
  'Permissions.FeatureFlags.View',
)

/**
 * `useSelectedRecord` reads the query string, so the page needs a Suspense
 * boundary — Next requires one around any `useSearchParams` consumer or the
 * whole route opts out of static rendering.
 */
const PageWithSuspense = () => (
  <Suspense>
    <PageWithAuthorization />
  </Suspense>
)

export default PageWithSuspense
