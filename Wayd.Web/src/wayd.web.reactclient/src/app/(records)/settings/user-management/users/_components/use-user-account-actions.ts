import { App } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  useActivateUserMutation,
  useDeactivateUserMutation,
  useUnlockUserMutation,
} from '@/src/store/features/user-management/users-api'

interface UserAccountInfo {
  id: string
  userName: string
  firstName: string
  lastName: string
  isActive: boolean
  isLockedOut: boolean
}

export default function useUserAccountActions() {
  const { user: authUser } = useAuth()
  const { modal } = App.useApp()
  const messageApi = useMessage()
  const [activateUser] = useActivateUserMutation()
  const [deactivateUser] = useDeactivateUserMutation()
  const [unlockUser] = useUnlockUserMutation()

  const getAccountActionMenuItems = (user: UserAccountInfo): ItemType[] => {
      const items: ItemType[] = []
      const fullName = `${user.firstName} ${user.lastName}`
      const isCurrentUser =
        !!authUser && user.userName === authUser.username

      if (user.isLockedOut) {
        items.push({
          key: 'unlock',
          label: 'Unlock Account',
          onClick: () => {
            modal.confirm({
              title: 'Unlock Account',
              content: `Are you sure you want to unlock ${fullName}?`,
              okText: 'Unlock',
              onOk: async () => {
                try {
                  const result = await unlockUser(user.id)
                  if ('error' in result) {
                    throw result.error
                  }
                  messageApi.success('Account unlocked successfully.')
                } catch (error: any) {
                  messageApi.error(
                    error?.data?.detail ?? 'Failed to unlock the account.',
                  )
                }
              },
            })
          },
        })
      }

      if (user.isActive) {
        items.push({
          key: 'deactivate',
          label: 'Deactivate Account',
          disabled: isCurrentUser,
          title: isCurrentUser
            ? 'You cannot deactivate your own account'
            : undefined,
          onClick: () => {
            modal.confirm({
              title: 'Deactivate Account',
              content: `Are you sure you want to deactivate ${fullName}?`,
              okText: 'Deactivate',
              okButtonProps: { danger: true },
              onOk: async () => {
                try {
                  const result = await deactivateUser(user.id)
                  if ('error' in result) {
                    throw result.error
                  }
                  messageApi.success('Account deactivated successfully.')
                } catch (error: any) {
                  messageApi.error(
                    error?.data?.detail ??
                      'Failed to deactivate the account.',
                  )
                }
              },
            })
          },
        })
      } else {
        items.push({
          key: 'activate',
          label: 'Activate Account',
          onClick: () => {
            modal.confirm({
              title: 'Activate Account',
              content: `Are you sure you want to activate ${fullName}?`,
              okText: 'Activate',
              onOk: async () => {
                try {
                  const result = await activateUser(user.id)
                  if ('error' in result) {
                    throw result.error
                  }
                  messageApi.success('Account activated successfully.')
                } catch (error: any) {
                  messageApi.error(
                    error?.data?.detail ??
                      'Failed to activate the account.',
                  )
                }
              },
            })
          },
        })
      }

      return items
    }

  return { getAccountActionMenuItems }
}
