'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useGetRoleUsersQuery } from '@/src/store/features/user-management/roles-api'
import {
  useGetUsersQuery,
  useManageRoleUsersMutation,
} from '@/src/store/features/user-management/users-api'
import { Modal, Spin, Transfer, Typography, Flex, theme } from 'antd'
import { useEffect, useState } from 'react'
import type { Key } from 'react'
import { useConfirmModal } from '@/src/hooks'

const { Text } = Typography

export interface ManageRoleUsersFormProps {
  roleId: string
  roleName: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface TransferItem {
  key: string
  title: string
  email: string
  roles: string[]
}

const ManageRoleUsersForm: React.FC<ManageRoleUsersFormProps> = ({
  roleId,
  roleName,
  onFormComplete,
  onFormCancel,
}) => {
  const [targetKeys, setTargetKeys] = useState<string[]>([])
  const [initialTargetKeys, setInitialTargetKeys] = useState<string[]>([])
  const [selectedKeys, setSelectedKeys] = useState<string[]>([])

  const { token } = theme.useToken()
  const messageApi = useMessage()

  const {
    data: allUsersData,
    isLoading: allUsersLoading,
    error: allUsersError,
  } = useGetUsersQuery()

  const {
    data: roleUsersData,
    isLoading: roleUsersLoading,
    error: roleUsersError,
  } = useGetRoleUsersQuery(roleId)

  const [manageRoleUsers] = useManageRoleUsersMutation()

  const isLoading = allUsersLoading || roleUsersLoading

  // Compute pending changes
  const usersToAdd = targetKeys.filter((key) => !initialTargetKeys.includes(key))

  const usersToRemove = initialTargetKeys.filter((key) => !targetKeys.includes(key))

  const hasChanges = usersToAdd.length > 0 || usersToRemove.length > 0

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await manageRoleUsers({
          roleId: roleId,
          userIdsToAdd: usersToAdd,
          userIdsToRemove: usersToRemove,
        })

        if (response.error) {
          throw response.error
        }

        messageApi.success('Successfully updated role users.')
        return true
      } catch (error: any) {
        if (error.status === 422 && error.errors) {
          messageApi.error('Correct the validation error(s) to continue.')
        } else {
          messageApi.error(
            error.detail ??
              'An error occurred while updating role users. Please try again.',
          )
        }
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while updating role users. Please try again.',
    permission: 'Permissions.UserRoles.Update',
  })

  // Initialize transfer data when role users load
  useEffect(() => {
    if (!roleUsersData) return

    const roleUserIds = roleUsersData.map((u) => u.id).sort()
    setTargetKeys(roleUserIds)
    setInitialTargetKeys(roleUserIds)
  }, [roleUsersData])

  // Handle data loading errors
  useEffect(() => {
    if (allUsersError || roleUsersError) {
      messageApi.error('Failed to load users.')
      onFormCancel()
    }
  }, [allUsersError, roleUsersError, onFormCancel, messageApi])

  // Map all users to Transfer data source
  const dataSource: TransferItem[] = !allUsersData ? [] : allUsersData
      .map((user) => ({
        key: user.id,
        title: user.isActive
          ? `${user.firstName} ${user.lastName}`
          : `${user.firstName} ${user.lastName} (Inactive)`,
        email: user.email ?? '',
        roles: user.roles?.map((r) => r.name) ?? [],
      }))
      .sort((a, b) => a.title.localeCompare(b.title))

  const onChange = (nextTargetKeys: Key[]) => {
    const keys = nextTargetKeys as string[]
    const sortedTargetKeys = keys.sort(
      (a, b) =>
        (dataSource.findIndex((item) => item.key === a) || 0) -
        (dataSource.findIndex((item) => item.key === b) || 0),
    )
    setTargetKeys(sortedTargetKeys)
  }

  const onSelectChange = (sourceSelectedKeys: Key[], targetSelectedKeys: Key[]) => {
    setSelectedKeys([...sourceSelectedKeys, ...targetSelectedKeys] as string[])
  }

  const filterOption = (inputValue: string, option: TransferItem) => {
    const search = inputValue.toLowerCase()
    return (
      option.title.toLowerCase().includes(search) ||
      option.email.toLowerCase().includes(search)
    )
  }

  return (
    <Modal
      title={
        <>
          <div>Manage Role Users</div>
          <Text type="secondary" style={{ fontSize: 14, fontWeight: 'normal' }}>
            {roleName}
          </Text>
        </>
      }
      open={isOpen}
      onOk={handleOk}
      okText="Save Changes"
      okButtonProps={{ disabled: !hasChanges }}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={'80vw'}
    >
      <Spin spinning={isLoading} size="large">
        <Flex vertical gap="small">
          <Flex vertical style={{ width: '100%' }}>
            <Transfer
              dataSource={dataSource}
              targetKeys={targetKeys}
              selectedKeys={selectedKeys}
              onChange={onChange}
              onSelectChange={onSelectChange}
              render={(item) => (
                <Flex
                  justify="space-between"
                  wrap="wrap"
                  align="flex-start"
                  gap="small"
                  style={{ width: '100%' }}
                >
                  <Flex vertical style={{ flex: '1 1 auto' }}>
                    <Text>{item.title}</Text>
                    {item.email && (
                      <Text
                        type="secondary"
                        style={{ fontSize: 12, marginTop: 4 }}
                      >
                        {item.email}
                      </Text>
                    )}
                  </Flex>
                  {item.roles.length > 0 && (
                    <Text
                      style={{
                        fontSize: 12,
                        whiteSpace: 'normal',
                        textAlign: 'right',
                        flex: '0 1 auto',
                        color: token.colorTextSecondary,
                      }}
                    >
                      {item.roles.join(', ')}
                    </Text>
                  )}
                </Flex>
              )}
              titles={['Available Users', 'Users with Role']}
              showSearch
              filterOption={filterOption}
              showSelectAll
              styles={{
                section: { width: '100%', height: '60vh', minHeight: 300 },
              }}
            />
          </Flex>
          {hasChanges && (
            <Flex vertical>
              <Text strong style={{ color: token.colorPrimary }}>
                Pending Changes
              </Text>
              <Flex wrap="wrap" gap="large">
                {usersToAdd.length > 0 && (
                  <Text style={{ color: token.colorSuccess }}>
                    +{usersToAdd.length} user
                    {usersToAdd.length !== 1 ? 's' : ''} will be added to this
                    role
                  </Text>
                )}
                {usersToRemove.length > 0 && (
                  <Text style={{ color: token.colorError }}>
                    -{usersToRemove.length} user
                    {usersToRemove.length !== 1 ? 's' : ''} will be removed from
                    this role
                  </Text>
                )}
              </Flex>
            </Flex>
          )}
        </Flex>
      </Spin>
    </Modal>
  )
}

export default ManageRoleUsersForm
