'use client'

import { CreateLinkRequest } from '@/src/services/api/wayd.schemas'
import { Form, Input, Modal } from 'antd'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { useCreateLinkMutation } from '@/src/store/features/common/links-api'
import { useMessage } from '../../contexts/messaging'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface CreateLinkFormProps {
  objectId: string
  onFormCreate: () => void
  onFormCancel: () => void
}

interface CreateLinkFormValues {
  objectId: string
  name: string
  url: string
}

const mapToRequestValues = (values: CreateLinkFormValues) => {
  return {
    name: values.name,
    url: values.url,
  } as CreateLinkRequest
}

const CreateLinkForm = ({
  objectId,
  onFormCreate,
  onFormCancel,
}: CreateLinkFormProps) => {
  const messageApi = useMessage()

  const [createLink] = useCreateLinkMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateLinkFormValues>({
      onSubmit: async (values: CreateLinkFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          request.objectId = objectId
          const response = await createLink(request)
          if (response.error) {
            throw response.error
          }

          messageApi.success('Successfully created link.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              'An unexpected error occurred while creating the link.',
            )
            console.error(error)
          }
          return false
        }
      },
      onComplete: onFormCreate,
      onCancel: onFormCancel,
      permission: 'Permissions.Links.Create',
    })

  return (
    <Modal
      title="Create Link"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="create-link-form">
        <Item label="Name" name="name" rules={[{ required: true }]}>
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
        </Item>
        <Item
          name="url"
          label="URL"
          rules={[{ required: true }, { type: 'url' }]}
        >
          <TextArea autoSize={{ minRows: 6, maxRows: 10 }} />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateLinkForm
