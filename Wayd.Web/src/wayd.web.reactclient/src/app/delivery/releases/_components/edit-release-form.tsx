'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ReleaseDto, UpdateReleaseRequest } from '@/src/services/wayd-api'
import { useUpdateReleaseMutation } from '@/src/store/features/delivery/releases-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, InputNumber, Modal } from 'antd'

const { Item } = Form

export interface EditReleaseFormProps {
  release: ReleaseDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditReleaseFormValues {
  version: string
  name?: string
  notes?: string
  sequence?: number
}

/**
 * Edits a release's descriptive fields.
 *
 * The dates are not here. Each carries a rule the aggregate enforces — cutting is one-way, releasing
 * cannot precede cutting — and folding them into a blanket save would hide which rule refused.
 */
const EditReleaseForm = ({
  release,
  onFormComplete,
  onFormCancel,
}: EditReleaseFormProps) => {
  const messageApi = useMessage()

  const [updateRelease] = useUpdateReleaseMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditReleaseFormValues>({
      onSubmit: async (values: EditReleaseFormValues, form) => {
        try {
          const request = {
            id: release.id,
            version: values.version,
            name: values.name,
            notes: values.notes,
            sequence: values.sequence,
          } as UpdateReleaseRequest

          const response = await updateRelease({ id: release.id, request })
          if (response.error) throw response.error

          messageApi.success('Release updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the release. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the release. Please try again.',
      permission: 'Permissions.Releases.Update',
    })

  return (
    <Modal
      title="Edit Release"
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
        name="edit-release-form"
        initialValues={{
          version: release.version,
          name: release.name,
          notes: release.notes,
          sequence: release.sequence,
        }}
      >
        <Item
          label="Version"
          name="version"
          rules={[
            { required: true, message: 'Version is required' },
            { max: 64, message: 'Version cannot be longer than 64 characters' },
          ]}
          extra="Free text — Wayd never parses or orders by it."
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
        <Item
          label="Sequence"
          name="sequence"
          extra="Only needed where release order differs from date order, as a backport does."
        >
          <InputNumber style={{ width: '100%' }} />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditReleaseForm
