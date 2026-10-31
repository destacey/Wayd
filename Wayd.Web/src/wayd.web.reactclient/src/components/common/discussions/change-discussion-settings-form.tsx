'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { DiscussionSettingsDto } from '@/src/services/wayd-api'
import {
  DiscussionChannelType,
  useSaveDiscussionSettingsMutation,
} from '@/src/store/features/common/discussions-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Modal, Switch, Typography } from 'antd'
import { FC } from 'react'

const { Item } = Form
const { Paragraph } = Typography

export interface ChangeDiscussionSettingsFormProps {
  settings: DiscussionSettingsDto
  /** What the settings belong to, such as "IT Help Desk", for the form's wording. */
  scopeName: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ChangeDiscussionSettingsFormValues {
  internalNotesEnabled: boolean
  mentionsGrantAccess: boolean
}

/**
 * Changes how conversations behave in one scope, such as an intake desk. Shows
 * only the settings its kind of record supports. Offered where the settings'
 * `canManage` says so.
 */
const ChangeDiscussionSettingsForm: FC<ChangeDiscussionSettingsFormProps> = ({
  settings,
  scopeName,
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()
  const [saveSettings] = useSaveDiscussionSettingsMutation()

  const { form, isOpen, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeDiscussionSettingsFormValues>({
      onSubmit: async (values, form) => {
        try {
          await saveSettings({
            channelType: settings.channelType as DiscussionChannelType,
            scopeId: settings.scopeId ?? undefined,
            internalNotesEnabled:
              settings.supportsInternalNotes && values.internalNotesEnabled,
            mentionsGrantAccess:
              settings.supportsMentionAccess && values.mentionsGrantAccess,
          }).unwrap()
          messageApi.success('Conversation settings saved.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          // Someone else saved first. Close rather than let this save over a change the form never showed; the
          // panel reloads the settings as they now are.
          if (apiError.status === 409) {
            messageApi.warning(
              'Someone else changed these settings. Review them and try again.',
            )
            return true
          }
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ?? 'Failed to save the conversation settings.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to save the conversation settings.',
    })

  return (
    <Modal
      title="Change Conversation Settings"
      open={isOpen}
      onOk={handleOk}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph type="secondary">
        How conversations work at {scopeName}. Changes apply at once and are
        recorded in the Activity log. Turning a setting off never shows anyone
        something they could not already read.
      </Paragraph>
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="change-discussion-settings-form"
        initialValues={{
          internalNotesEnabled: settings.internalNotesEnabled,
          mentionsGrantAccess: settings.mentionsGrantAccess,
        }}
      >
        {settings.supportsInternalNotes && (
          <Item
            name="internalNotesEnabled"
            label="Internal Notes"
            valuePropName="checked"
            extra="When off, nobody can post a new note. Existing notes stay readable to the people who could read them."
          >
            <Switch />
          </Item>
        )}
        {settings.supportsMentionAccess && (
          <Item
            name="mentionsGrantAccess"
            label="Mentions Give Access"
            valuePropName="checked"
            extra="When on, mentioning someone the author could give access to gives it. When off, only people who already have the record can be mentioned."
          >
            <Switch />
          </Item>
        )}
      </Form>
    </Modal>
  )
}

export default ChangeDiscussionSettingsForm
