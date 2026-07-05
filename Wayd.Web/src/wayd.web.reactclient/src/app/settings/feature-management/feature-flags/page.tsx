'use client'

import PageTitle from '@/src/components/common/page-title'
import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import { useMemo, useState } from 'react'
import { Button } from 'antd'
import type { ColumnDef } from '@tanstack/react-table'
import { authorizePage } from '../../../../components/hoc'
import useAuth from '../../../../components/contexts/auth'
import type { ItemType } from 'antd/es/menu/interface'
import { useDocumentTitle } from '../../../../hooks'
import {
  ControlItemsMenu,
  ControlItemSwitch,
} from '../../../../components/common/control-items-menu'
import { FeatureFlagListDto } from '@/src/services/wayd-api'
import { useGetFeatureFlagsQuery } from '@/src/store/features/admin/feature-flags-api'
import EditFeatureFlagForm from './_components/edit-feature-flag-form'
import FeatureFlagDetailsDrawer from './_components/feature-flag-details-drawer'
import useFeatureFlagActions from './_components/use-feature-flag-actions'

const FeatureFlagsListPage = () => {
  useDocumentTitle('Feature Flags')
  const [editingFlagId, setEditingFlagId] = useState<number | null>(null)
  const [viewingFlagId, setViewingFlagId] = useState<number | null>(null)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [includeArchived, setIncludeArchived] = useState(false)

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.FeatureFlags.Update')
  const canDelete = hasPermissionClaim('Permissions.FeatureFlags.Delete')
  const showRowActions = canUpdate || canDelete

  const {
    data: featureFlags = [],
    isLoading,
    refetch,
  } = useGetFeatureFlagsQuery({ includeArchived })

  const { handleToggle, handleArchive } = useFeatureFlagActions()

  const refresh = () => {
    refetch()
  }

  const closeDetailsDrawer = () => {
    setDrawerOpen(false)
    setViewingFlagId(null)
  }

  const columns = useMemo<ColumnDef<FeatureFlagListDto, any>[]>(() => {
    const openDetailsDrawer = (id: number) => {
      setViewingFlagId(id)
      setDrawerOpen(true)
    }

    return [
      createActionsColumn<FeatureFlagListDto>({
        hide: !showRowActions,
        ariaLabel: 'Feature flag actions',
        getItems: (flag) => {
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
        },
      }),
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 250,
        cell: ({ row }) => (
          // A real button (styled as a link) — an href-less <a> isn't
          // keyboard-focusable and reads inconsistently to assistive tech.
          <Button
            type="link"
            style={{ padding: 0, height: 'auto', fontSize: 'inherit' }}
            onClick={() => openDetailsDrawer(row.original.id)}
          >
            {row.original.name}
          </Button>
        ),
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
      // Mode-dependent column: excluded from the defs (not meta.hide) so it
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
  }, [
    showRowActions,
    canUpdate,
    canDelete,
    includeArchived,
    handleToggle,
    handleArchive,
  ])

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
    <>
      <PageTitle title="Feature Flags" />
      <WaydGrid
        columns={columns}
        data={featureFlags}
        onRefresh={refresh}
        isLoading={isLoading}
        persistStateKey="settings-feature-flags"
        csvFileName="feature-flags"
        rightSlot={<ControlItemsMenu items={controlItems} />}
      />
      {editingFlagId !== null && (
        <EditFeatureFlagForm
          featureFlagId={editingFlagId}
          onFormSave={() => setEditingFlagId(null)}
          onFormCancel={() => setEditingFlagId(null)}
        />
      )}
      {viewingFlagId !== null && (
        <FeatureFlagDetailsDrawer
          featureFlagId={viewingFlagId}
          drawerOpen={drawerOpen}
          onDrawerClose={closeDetailsDrawer}
        />
      )}
    </>
  )
}

const PageWithAuthorization = authorizePage(
  FeatureFlagsListPage,
  'Permission',
  'Permissions.FeatureFlags.View',
)

export default PageWithAuthorization
