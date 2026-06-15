'use client'

import { TenantMigrationCandidateDto } from '@/src/services/wayd-api'
import {
  useGetTenantMigrationCandidatesQuery,
  useStageBulkTenantMigrationMutation,
} from '@/src/store/features/user-management/oidc-providers-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { WaydEmpty } from '@/src/components/common'
import {
  Alert,
  Flex,
  Modal,
  Select,
  Space,
  Spin,
  Table,
  Tag,
  Typography,
} from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { useMemo, useState } from 'react'

const { Text } = Typography

// Matches StageBulkTenantMigrationCommandValidator.MaxUserIds on the backend.
const MAX_SELECTION = 500

export interface StageTenantMigrationFormProps {
  providerId: string
  allowedTenantIds: string[]
  onFormComplete: () => void
  onFormCancel: () => void
}

const columns: ColumnsType<TenantMigrationCandidateDto> = [
  {
    title: 'User',
    dataIndex: 'userName',
    key: 'userName',
    render: (_, record) => (
      <Space orientation="vertical" size={0}>
        <Text>
          {[record.firstName, record.lastName].filter(Boolean).join(' ') ||
            record.userName}
        </Text>
        <Text type="secondary">{record.email ?? record.userName}</Text>
      </Space>
    ),
  },
  {
    title: 'Status',
    dataIndex: 'isActive',
    key: 'isActive',
    width: 110,
    render: (isActive: boolean) =>
      isActive ? <Tag color="success">Active</Tag> : <Tag>Inactive</Tag>,
  },
]

const StageTenantMigrationForm = ({
  providerId,
  allowedTenantIds,
  onFormComplete,
  onFormCancel,
}: StageTenantMigrationFormProps) => {
  const messageApi = useMessage()

  const [sourceTenantId, setSourceTenantId] = useState<string>()
  const [targetTenantId, setTargetTenantId] = useState<string>()
  const [selectedUserIds, setSelectedUserIds] = useState<string[]>([])

  const [stageBulkMigration, { isLoading: isStaging }] =
    useStageBulkTenantMigrationMutation()

  const { data: candidates, isFetching: isLoadingCandidates } =
    useGetTenantMigrationCandidatesQuery(
      { providerId, sourceTenantId: sourceTenantId! },
      { skip: !sourceTenantId },
    )

  const sourceOptions = useMemo(
    () => allowedTenantIds.map((t) => ({ label: t, value: t })),
    [allowedTenantIds],
  )

  // The target list excludes the chosen source — you can't migrate a tenant to itself.
  const targetOptions = useMemo(
    () =>
      allowedTenantIds
        .filter((t) => t !== sourceTenantId)
        .map((t) => ({ label: t, value: t })),
    [allowedTenantIds, sourceTenantId],
  )

  const overCap = selectedUserIds.length > MAX_SELECTION
  const canSubmit =
    !!sourceTenantId &&
    !!targetTenantId &&
    selectedUserIds.length > 0 &&
    !overCap

  const handleSourceChange = (value: string) => {
    setSourceTenantId(value)
    setSelectedUserIds([])
    // Clear the target if it now collides with the new source.
    if (value === targetTenantId) {
      setTargetTenantId(undefined)
    }
  }

  const handleStage = async () => {
    if (!canSubmit) return
    try {
      const response = await stageBulkMigration({
        providerId,
        request: {
          sourceTenantId: sourceTenantId!,
          targetTenantId: targetTenantId!,
          userIds: selectedUserIds,
        },
      })
      if (response.error) throw response.error

      const result = response.data!
      const skippedCount = result.skipped?.length ?? 0
      if (skippedCount > 0) {
        messageApi.warning(
          `${result.stagedCount} user(s) staged. ${skippedCount} skipped (no longer eligible).`,
        )
      } else {
        messageApi.success(
          `${result.stagedCount} user(s) staged. Each rebind completes on the user’s next sign-in from the target tenant.`,
        )
      }
      onFormComplete()
    } catch (error: any) {
      messageApi.error(
        error?.data?.detail ??
          error?.detail ??
          'An unexpected error occurred while staging the migration.',
      )
    }
  }

  return (
    <Modal
      title="Stage Tenant Migration"
      open
      width={720}
      onOk={handleStage}
      okText={`Stage migration for ${selectedUserIds.length} user(s)`}
      okButtonProps={{ disabled: !canSubmit }}
      confirmLoading={isStaging}
      onCancel={onFormCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap="middle">
        <Alert
          type="info"
          showIcon
          description="Select a source and target tenant, choose the users to move, then stage the migration. Each user’s rebind completes automatically on their next sign-in from the target tenant; their identity and links are preserved."
        />

        <Space wrap size="large">
          <Space orientation="vertical" size={4}>
            <Text strong>Source tenant</Text>
            <Select
              style={{ minWidth: 320 }}
              placeholder="Select the tenant to migrate from"
              options={sourceOptions}
              value={sourceTenantId}
              onChange={handleSourceChange}
            />
          </Space>
          <Space orientation="vertical" size={4}>
            <Text strong>Target tenant</Text>
            <Select
              style={{ minWidth: 320 }}
              placeholder="Select the tenant to migrate to"
              options={targetOptions}
              value={targetTenantId}
              onChange={setTargetTenantId}
              disabled={!sourceTenantId}
            />
          </Space>
        </Space>

        {overCap && (
          <Alert
            type="warning"
            showIcon
            description={`Select at most ${MAX_SELECTION} users per migration.`}
          />
        )}

        {!sourceTenantId ? (
          <WaydEmpty message="Select a source tenant to list its users." />
        ) : isLoadingCandidates ? (
          <Spin />
        ) : (
          <Table<TenantMigrationCandidateDto>
            rowKey="userId"
            size="small"
            columns={columns}
            dataSource={candidates ?? []}
            pagination={{ pageSize: 10, hideOnSinglePage: true }}
            scroll={{ y: 320 }}
            locale={{
              emptyText: (
                <WaydEmpty message="No users on the selected source tenant without a pending migration." />
              ),
            }}
            rowSelection={{
              selectedRowKeys: selectedUserIds,
              onChange: (keys) => setSelectedUserIds(keys as string[]),
            }}
          />
        )}
      </Flex>
    </Modal>
  )
}

export default StageTenantMigrationForm
