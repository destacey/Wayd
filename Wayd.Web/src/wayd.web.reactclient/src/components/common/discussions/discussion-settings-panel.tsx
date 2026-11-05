'use client'

import {
  DiscussionChannelType,
  useGetDiscussionSettingsQuery,
} from '@/src/store/features/common/discussions-api'
import { Button, Flex, Skeleton, Typography } from 'antd'
import { FC, useState } from 'react'
import ChangeDiscussionSettingsForm from './change-discussion-settings-form'

const { Text } = Typography

export interface DiscussionSettingsPanelProps {
  channelType: DiscussionChannelType
  /** The scope, such as a desk's id; left out for the default every scope follows. */
  scopeId?: string
  /** What the settings belong to, such as the desk's name, for the form's wording. */
  scopeName: string
}

/**
 * How conversations behave in one scope, each setting saying plainly what it
 * does, with a way to change them for the people who run it. Shows only the
 * settings the kind of record supports.
 */
const DiscussionSettingsPanel: FC<DiscussionSettingsPanelProps> = ({
  channelType,
  scopeId,
  scopeName,
}) => {
  const [changing, setChanging] = useState(false)
  const {
    data: settings,
    isLoading,
    isError,
  } = useGetDiscussionSettingsQuery({
    channelType,
    scopeId,
  })

  if (isLoading) return <Skeleton.Input active size="small" />
  if (isError)
    return (
      <Text type="secondary">Could not load the conversation settings.</Text>
    )
  if (!settings) return null

  return (
    <Flex vertical gap={4}>
      {settings.supportsInternalNotes && (
        <Text>
          {settings.internalNotesEnabled
            ? 'Internal notes are on.'
            : 'Internal notes are off: nobody can post a new one.'}
        </Text>
      )}
      {settings.supportsMentionAccess && (
        <Text>
          {settings.mentionsGrantAccess
            ? 'Mentioning someone can give them access.'
            : 'Only people who already have the record can be mentioned.'}
        </Text>
      )}
      {settings.isInherited && scopeId && (
        <Text type="secondary">Following the default.</Text>
      )}
      {settings.canManage && (
        <div>
          <Button size="small" onClick={() => setChanging(true)}>
            Change Conversation Settings
          </Button>
        </div>
      )}
      {changing && settings.canManage && (
        <ChangeDiscussionSettingsForm
          settings={settings}
          scopeName={scopeName}
          onFormComplete={() => setChanging(false)}
          onFormCancel={() => setChanging(false)}
        />
      )}
    </Flex>
  )
}

export default DiscussionSettingsPanel
