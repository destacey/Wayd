'use client'

import { FC, useState } from 'react'
import {
  Alert,
  Button,
  Flex,
  Select,
  Space,
  Tag,
  Tooltip,
  Typography,
} from 'antd'
import type { BaseOptionType } from 'antd/es/select'
import { LoadingOutlined, WarningOutlined } from '@ant-design/icons'
import {
  useGetConnectionIdentitiesQuery,
  useUpdateConnectionIdentityMutation,
} from '@/src/store/features/app-integration/connections-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import {
  ExternalIdentityMappingDto,
  IdentityMappingAction,
} from '@/src/services/wayd-api'
import {
  WaydGrid,
  createActionsColumn,
  formatDateTime,
} from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError, type ApiError } from '@/src/utils'
import { ItemType } from 'antd/es/menu/interface'
import styles from './identity-mappings-tab.module.css'

const { Text } = Typography

const statusTagColor: Record<string, string | undefined> = {
  Unmapped: 'warning',
  AutoMatched: 'processing',
  ManuallyMapped: 'success',
  Ignored: undefined,
}

const statusLabel: Record<string, string> = {
  Unmapped: 'Unmapped',
  AutoMatched: 'Auto-matched',
  ManuallyMapped: 'Mapped',
  Ignored: 'Ignored',
}

interface Props {
  connectionId: string
}

/**
 * Maps the users an external system reports on work items to Wayd employees.
 *
 * Per-row save rather than the whole-list replace the team mappings use: a connection can carry
 * hundreds of identities, and posting every row to change one loses a concurrent admin's edit.
 */
const IdentityMappingsTab: FC<Props> = ({ connectionId }) => {
  const [unmappedOnly, setUnmappedOnly] = useState(false)
  const [savingIds, setSavingIds] = useState<string[]>([])
  const [editingId, setEditingId] = useState<string | null>(null)
  // Shown while a save is in flight so the picked value appears before the round trip returns.
  const [pendingNames, setPendingNames] = useState<
    Record<string, string | null>
  >({})
  // A failed save reverts the cell silently and the toast expires, so the row has to carry the
  // failure itself or the user reads the old value as success.
  const [failedIds, setFailedIds] = useState<Record<string, string>>({})

  const { hasClaim } = useAuth()
  const canUpdate = hasClaim('Permission', 'Permissions.Connections.Update')
  const messageApi = useMessage()

  const {
    data: identities,
    isLoading,
    error,
    refetch,
  } = useGetConnectionIdentitiesQuery({ connectionId, unmappedOnly })

  // Inactive employees included: a former employee still authored and was assigned work, and
  // attributing that history is exactly what an admin comes here to do.
  const { data: employeeOptions } = useGetEmployeeOptionsQuery(true)

  const [updateIdentity] = useUpdateConnectionIdentityMutation()

  const applyAction = async (
    mapping: ExternalIdentityMappingDto,
    action: IdentityMappingAction,
    employeeId?: string,
  ) => {
    setEditingId(null)
    setPendingNames((names) => ({
      ...names,
      [mapping.id]:
        action === IdentityMappingAction.Map
          ? (employeeOptions?.find((o) => o.value === employeeId)?.label ??
            null)
          : null,
    }))
    setSavingIds((ids) => [...ids, mapping.id])
    setFailedIds((ids) => {
      const { [mapping.id]: _cleared, ...rest } = ids
      return rest
    })
    try {
      const response = await updateIdentity({
        connectionId,
        mappingId: mapping.id,
        action,
        employeeId,
      } as never)
      if (response.error) throw response.error

      const name = mapping.displayName ?? mapping.handle ?? mapping.externalId
      messageApi.success(
        action === IdentityMappingAction.Map
          ? `Mapped ${name}.`
          : action === IdentityMappingAction.Ignore
            ? `Ignoring ${name}.`
            : `Cleared the mapping for ${name}.`,
      )
    } catch (err) {
      const apiError: ApiError = isApiError(err) ? err : {}
      console.error(err)
      const detail =
        apiError.detail ??
        'An error occurred while updating the identity mapping.'
      messageApi.error(detail)
      setFailedIds((ids) => ({ ...ids, [mapping.id]: detail }))
    } finally {
      setSavingIds((ids) => ids.filter((id) => id !== mapping.id))
      setPendingNames((names) => {
        const { [mapping.id]: _dropped, ...rest } = names
        return rest
      })
    }
  }

  const unmappedCount =
    identities?.filter((i) => i.status === 'Unmapped').length ?? 0

  const columns: ColumnDef<ExternalIdentityMappingDto, any>[] = [
    ...(canUpdate
      ? [
          createActionsColumn<ExternalIdentityMappingDto>({
            ariaLabel: 'Identity actions',
            getItems: (m) => {
              const items: ItemType[] = []

              if (m.status !== 'Ignored') {
                items.push({
                  key: 'ignore',
                  label: 'Ignore',
                  onClick: () => applyAction(m, IdentityMappingAction.Ignore),
                })
              }

              if (m.status === 'Ignored' || m.status === 'ManuallyMapped') {
                items.push({
                  key: 'clear',
                  label: 'Clear decision',
                  onClick: () => applyAction(m, IdentityMappingAction.Clear),
                })
              }

              return items
            },
          }),
        ]
      : []),
    {
      accessorKey: 'displayName',
      header: 'Name',
      cell: ({ row }) => {
        const m = row.original
        // Handle is the fallback identifier: some connectors report no address at all,
        // so it is the only human-readable thing left.
        return m.displayName ?? m.handle ?? m.externalId
      },
    },
    {
      accessorKey: 'email',
      header: 'Email',
      cell: ({ row }) => {
        const m = row.original
        return m.email ? (
          m.email
        ) : (
          <Tooltip title="This system did not report an address for this user.">
            <Text type="secondary">{m.handle ?? '-'}</Text>
          </Tooltip>
        )
      },
    },
    {
      accessorKey: 'status',
      header: 'Status',
      size: 130,
      cell: ({ row }) => {
        const status = row.original.status
        return (
          <Tag color={statusTagColor[status]}>
            {statusLabel[status] ?? status}
          </Tag>
        )
      },
    },
    {
      accessorKey: 'employeeId',
      header: 'Employee',
      size: 240,
      cell: ({ row }) => {
        const m = row.original
        const isSaving = savingIds.includes(m.id)
        const isPending = m.id in pendingNames

        if (isSaving || isPending) {
          const pending = pendingNames[m.id]
          return (
            <Space size="small">
              <LoadingOutlined />
              {pending ? pending : <Text type="secondary">Unmapped</Text>}
            </Space>
          )
        }

        if (editingId !== m.id) {
          // An ignored identity is a decision, not a blank to fill in — reopen it through the
          // row menu's "Clear decision" rather than making a stray click undo it.
          if (m.status === 'Ignored') {
            return <Text type="secondary">Ignored</Text>
          }

          const label = m.employeeName ?? 'Unmapped'
          const failure = failedIds[m.id]

          const value = failure ? (
            <Space size={4}>
              <Tooltip title={`${failure} Your change was not saved.`}>
                <WarningOutlined className={styles.failureIcon} />
              </Tooltip>
              {m.employeeName ? label : <Text type="secondary">{label}</Text>}
            </Space>
          ) : m.employeeName ? (
            label
          ) : (
            <Text type="secondary">{label}</Text>
          )

          if (!canUpdate) {
            return value
          }

          return (
            <button
              type="button"
              className={styles.employeeCellTrigger}
              onClick={() => setEditingId(m.id)}
              aria-label={
                failure
                  ? `Retry mapping an employee to ${m.displayName ?? m.handle ?? m.externalId}. The last attempt failed.`
                  : `Change the employee mapped to ${m.displayName ?? m.handle ?? m.externalId}`
              }
            >
              {value}
            </button>
          )
        }

        return (
          <Select
            size="small"
            autoFocus
            defaultOpen
            style={{ width: '100%' }}
            allowClear
            showSearch
            placeholder="Select an employee"
            optionFilterProp="label"
            filterOption={(input, option: BaseOptionType | undefined) =>
              (option?.label?.toLowerCase() ?? '').includes(input.toLowerCase())
            }
            options={employeeOptions ?? []}
            value={m.employeeId ?? undefined}
            onBlur={() => setEditingId(null)}
            onKeyDown={(e) => {
              if (e.key === 'Escape') setEditingId(null)
            }}
            onChange={(value?: string) =>
              value
                ? applyAction(m, IdentityMappingAction.Map, value)
                : applyAction(m, IdentityMappingAction.Clear)
            }
          />
        )
      },
    },
    {
      accessorKey: 'lastSeen',
      header: 'Last Seen',
      size: 170,
      cell: ({ getValue }) => formatDateTime(getValue()),
    },
  ]

  if (error) {
    return (
      <Alert
        type="error"
        title="Unable to load identities"
        description="An error occurred while loading the external identities for this connection."
      />
    )
  }

  return (
    <Flex vertical gap="middle">
      {unmappedCount > 0 && (
        <Alert
          type="info"
          showIcon
          title={`${unmappedCount} ${unmappedCount === 1 ? 'identity needs' : 'identities need'} mapping`}
          description="Work assigned to these users is not attributed to anyone in Wayd. Map them to an employee, or ignore the ones that are service accounts."
        />
      )}
      <WaydGrid<ExternalIdentityMappingDto>
        data={identities}
        isLoading={isLoading}
        columns={columns}
        onRefresh={refetch}
        csvFileName="connection-identities"
        emptyMessage={
          unmappedOnly
            ? 'Every identity this connection has seen is mapped.'
            : 'No external identities have been seen yet. They appear after the first sync.'
        }
        leftSlot={
          <Space>
            <Button
              type={unmappedOnly ? 'primary' : 'default'}
              onClick={() => setUnmappedOnly((v) => !v)}
            >
              {unmappedOnly ? 'Showing unmapped' : 'Show unmapped only'}
            </Button>
          </Space>
        }
        helpContent={
          <Typography.Paragraph>
            Users an external system reported on synced work items. Wayd matches
            them to employees by email address automatically; anyone it cannot
            match appears here as unmapped. Mapping or ignoring an identity is
            permanent — a later sync will not overwrite it.
          </Typography.Paragraph>
        }
      />
    </Flex>
  )
}

export default IdentityMappingsTab
