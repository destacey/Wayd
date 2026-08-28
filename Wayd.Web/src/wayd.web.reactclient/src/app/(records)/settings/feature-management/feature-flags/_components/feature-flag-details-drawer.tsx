'use client'

import { useGetFeatureFlagQuery } from '@/src/store/features/admin/feature-flags-api'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { getDrawerWidthPixels } from '@/src/utils'
import { Button, Drawer, Dropdown, Flex } from 'antd'
import { MoreOutlined } from '@ant-design/icons'
import { ItemType } from 'antd/es/menu/interface'
import { FC, useEffect, useState } from 'react'
import { LabeledContent } from '@/src/components/common/content'
import EditFeatureFlagForm from './edit-feature-flag-form'
import useFeatureFlagActions from './use-feature-flag-actions'

export interface FeatureFlagDetailsDrawerProps {
  featureFlagId: number
  drawerOpen: boolean
  onDrawerClose: () => void
}

const FeatureFlagDetailsDrawer: FC<FeatureFlagDetailsDrawerProps> = ({
  featureFlagId,
  drawerOpen,
  onDrawerClose,
}) => {
  const [size, setSize] = useState(() => getDrawerWidthPixels())
  const [openEditForm, setOpenEditForm] = useState(false)
  const messageApi = useMessage()
  const { hasPermissionClaim } = useAuth()

  const canUpdate = hasPermissionClaim('Permissions.FeatureFlags.Update')
  const canDelete = hasPermissionClaim('Permissions.FeatureFlags.Delete')

  const {
    data: featureFlag,
    isLoading,
    error,
    refetch,
  } = useGetFeatureFlagQuery(featureFlagId)

  const { handleToggle, handleArchive } = useFeatureFlagActions()

  useEffect(() => {
    if (error) {
      messageApi.error(
        'An error occurred while loading feature flag data. Please try again.',
      )
    }
  }, [error, messageApi])

  const extraMenu = (() => {
    if (!canUpdate && !canDelete) return undefined
    if (!featureFlag) return undefined

    const items: ItemType[] = []

    if (canUpdate) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => setOpenEditForm(true),
      })
      items.push({
        key: 'toggle',
        label: featureFlag.isEnabled ? 'Disable' : 'Enable',
        onClick: () => handleToggle(featureFlag),
      })
    }

    if (canDelete && !featureFlag.isSystem && !featureFlag.isArchived) {
      if (items.length > 0) {
        items.push({ key: 'divider', type: 'divider' })
      }
      items.push({
        key: 'archive',
        label: 'Archive',
        danger: true,
        onClick: () => handleArchive(featureFlag),
      })
    }

    if (items.length === 0) return undefined

    return (
      <Dropdown menu={{ items }} trigger={['click']}>
        <Button type="text" size="small" icon={<MoreOutlined />} />
      </Dropdown>
    )
  })()

  return (
    <>
      <Drawer
        title={featureFlag?.displayName ?? 'Feature Flag Details'}
        placement="right"
        onClose={onDrawerClose}
        open={drawerOpen}
        loading={isLoading}
        size={size}
        resizable={{
          onResize: (newSize) => setSize(newSize),
        }}
        destroyOnHidden={true}
        extra={extraMenu}
      >
        <Flex vertical gap={10}>
          <LabeledContent label="Name">{featureFlag?.name}</LabeledContent>
          <LabeledContent label="Type">
            {featureFlag?.isSystem ? 'System' : 'User'}
          </LabeledContent>
          <LabeledContent label="Enabled">
            {featureFlag?.isEnabled ? 'Yes' : 'No'}
          </LabeledContent>
          {featureFlag?.isArchived && (
            <LabeledContent label="Archived">Yes</LabeledContent>
          )}
          {featureFlag?.description && (
            <LabeledContent label="Description">
              {featureFlag.description}
            </LabeledContent>
          )}
        </Flex>
      </Drawer>
      {openEditForm && (
        <EditFeatureFlagForm
          featureFlagId={featureFlagId}
          onFormSave={() => {
            setOpenEditForm(false)
            refetch()
          }}
          onFormCancel={() => setOpenEditForm(false)}
        />
      )}
    </>
  )
}

export default FeatureFlagDetailsDrawer
