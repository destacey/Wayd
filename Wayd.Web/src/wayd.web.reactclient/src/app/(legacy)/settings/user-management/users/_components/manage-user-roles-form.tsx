'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  useGetUserRolesQuery,
  useManageUserRolesMutation,
} from '@/src/store/features/user-management/users-api'
import { Modal, Spin, Transfer, TransferProps } from 'antd'
import { useEffect, useState } from 'react'
import type { Key } from 'react'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export interface ManageUserRolesFormProps {
  userId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface RecordType {
  key: string
  title: string
}

const ManageUserRolesForm: React.FC<ManageUserRolesFormProps> = ({
  userId,
  onFormComplete,
  onFormCancel,
}) => {
  const [roles, setRoles] = useState<RecordType[]>([])
  const [targetKeys, setTargetKeys] = useState<string[]>([])
  const [selectedKeys, setSelectedKeys] = useState<TransferProps['targetKeys']>(
    [],
  )

  const messageApi = useMessage()

  const {
    data: userRolesData,
    isLoading: userRolesLoading,
    error: userRolesError,
  } = useGetUserRolesQuery({
    id: userId,
    includeUnassigned: true,
  })

  const [manageUserRoles] =
    useManageUserRolesMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await manageUserRoles({
          userId: userId,
          roleNames: targetKeys,
        })
        if (response.error) {
          throw response.error
        }
        messageApi.success('Successfully updated user roles.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        if (apiError.status === 422 && apiError.errors) {
          messageApi.error('Correct the validation error(s) to continue.')
        } else {
          messageApi.error(
            apiError.detail ??
              'An error occurred while updating the user roles. Please try again.',
          )
        }
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while updating user roles. Please try again.',
    permission: 'Permissions.UserRoles.Update',
  })

  useEffect(() => {
    if (!userRolesData) return

    const transformedRoles = userRolesData
      .map((role) => ({
        key: role.roleName,
        title: role.roleName,
      }))
      .sort((a, b) => (a.title ?? '').localeCompare(b.title ?? '')) as RecordType[]

    setRoles(transformedRoles)

    const activeRoleNames = userRolesData
      .filter((role) => role.enabled)
      .map((role) => role.roleName)
      .filter((r): r is string => r !== undefined)
      .sort((a, b) => {
        const roleA =
          userRolesData.find((r) => r.roleName === a)?.roleName || ''
        const roleB =
          userRolesData.find((r) => r.roleName === b)?.roleName || ''
        return roleA.localeCompare(roleB)
      })

    setTargetKeys(activeRoleNames)
  }, [userRolesData])

  useEffect(() => {
    if (userRolesError) {
      messageApi.error('Failed to load user roles.')
      onFormCancel()
    }
  }, [userRolesError, onFormCancel, messageApi])

  useEffect(() => {
    setSelectedKeys([])
  }, [userRolesData])

  const onChange = (nextTargetKeys: Key[]) => {
    const keys = nextTargetKeys as string[]
    const sortedTargetKeys = keys.sort((a, b) => {
      const roleA = roles.find((r) => r.title === a)?.title || ''
      const roleB = roles.find((r) => r.title === b)?.title || ''
      return roleA.localeCompare(roleB)
    })

    setTargetKeys(sortedTargetKeys)
  }

  const onSelectChange: TransferProps['onSelectChange'] = (
    sourceSelectedKeys,
    targetSelectedKeys,
  ) => {
    setSelectedKeys([...sourceSelectedKeys, ...targetSelectedKeys])
  }

  return (
    <Modal
      title="Manage User Roles"
      open={isOpen}
      onOk={handleOk}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Spin spinning={userRolesLoading} size="large">
        <Transfer
          dataSource={roles || []}
          targetKeys={targetKeys}
          selectedKeys={selectedKeys}
          render={(item) => item.title}
          onChange={onChange}
          onSelectChange={onSelectChange}
          titles={['Available', 'Assigned']}
          listStyle={{ width: 300, height: 400 }}
          showSearch
        />
      </Spin>
    </Modal>
  )
}

export default ManageUserRolesForm
