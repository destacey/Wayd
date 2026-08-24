'use client'

import { App } from 'antd'
import {
  useToggleFeatureFlagMutation,
  useArchiveFeatureFlagMutation,
} from '@/src/store/features/admin/feature-flags-api'
import { useMessage } from '@/src/components/contexts/messaging'

interface FeatureFlagInfo {
  id: number
  name: string
  displayName: string
  isEnabled: boolean
}

const useFeatureFlagActions = () => {
  const messageApi = useMessage()
  const { modal } = App.useApp()
  const [toggleFeatureFlag] = useToggleFeatureFlagMutation()
  const [archiveFeatureFlag] = useArchiveFeatureFlagMutation()

  const handleToggle = async (flag: FeatureFlagInfo) => {
    try {
      await toggleFeatureFlag({
        id: flag.id,
        isEnabled: !flag.isEnabled,
      }).unwrap()
      messageApi.success(
        `Feature flag ${!flag.isEnabled ? 'enabled' : 'disabled'}.`,
      )
    } catch {
      messageApi.error('Failed to toggle feature flag.')
    }
  }

  const handleArchive = (flag: FeatureFlagInfo) => {
    modal.confirm({
      title: 'Archive Feature Flag',
      content: `Are you sure you want to archive "${flag.displayName}"? This will also disable the flag.`,
      okText: 'Archive',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          const response = await archiveFeatureFlag(flag.id)
          if ('error' in response) throw response.error
          messageApi.success('Feature flag archived.')
        } catch {
          messageApi.error('Failed to archive feature flag.')
        }
      },
    })
  }

  return { handleToggle, handleArchive }
}

export default useFeatureFlagActions
