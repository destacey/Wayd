'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { VersionDto, UpdateVersionRequest } from '@/src/services/wayd-api'
import { useUpdateVersionMutation } from '@/src/store/features/delivery/versions-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'

const { Item } = Form

export interface EditVersionFormProps {
  version: VersionDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditVersionFormValues {
  version: string
  name?: string
  notes?: string
}

/**
 * Edits a version's descriptive fields.
 *
 * The dates are not here. Each carries a rule the aggregate enforces — cutting is one-way, releasing
 * cannot precede cutting — and folding them into a blanket save would hide which rule refused.
 */
const EditVersionForm = ({
  version,
  onFormComplete,
  onFormCancel,
}: EditVersionFormProps) => {
  const messageApi = useMessage()

  const [updateVersion] = useUpdateVersionMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditVersionFormValues>({
      onSubmit: async (values: EditVersionFormValues, form) => {
        try {
          const request = {
            id: version.id,
            version: values.version,
            name: values.name,
            notes: values.notes,
            // Passed through rather than edited. The update is a whole-record overwrite, so omitting
            // this would clear an ordering an import had set — and there is no way to set one here.
            sequence: version.sequence,
          } as UpdateVersionRequest

          const response = await updateVersion({ id: version.id, cacheKey: version.key, request })
          if (response.error) throw response.error

          messageApi.success('Version updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the version. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the version. Please try again.',
      permission: 'Permissions.Delivery.Update',
    })

  return (
    <Modal
      title="Edit Version"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-version-form"
        initialValues={{
          version: version.number,
          name: version.name,
          notes: version.notes,
        }}
      >
        <Item
          label="Version"
          name="version"
          rules={[
            { required: true, message: 'Version is required' },
            { max: 64, message: 'Version cannot be longer than 64 characters' },
          ]}
          extra="For example 4.8.2, 2026.04, or v3-beta."
        >
          <Input />
        </Item>
        <Item
          label="Name"
          name="name"
          rules={[{ max: 128, message: 'Name cannot be longer than 128 characters' }]}
        >
          <Input />
        </Item>
        <Item label="Notes" name="notes">
          <MarkdownEditor maxLength={4000} />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditVersionForm
