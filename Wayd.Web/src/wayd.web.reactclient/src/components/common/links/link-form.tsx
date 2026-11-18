'use client'

import { LinkDto, LinkRequest } from '@/src/services/api/wayd.schemas'
import { Form, Input, Modal } from 'antd'
import { useEffect } from 'react'
import { toApiError, toFormErrors } from '@/src/utils'
import {
  LinkOwner,
  useCreateLinkMutation,
  useUpdateLinkMutation,
} from '@/src/store/features/common/links-api'
import { useMessage } from '../../contexts/messaging'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface LinkFormProps {
  /** The record the link is on. */
  owner: LinkOwner
  /** The link to change. Leave out to add a new one. */
  link?: LinkDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface LinkFormValues {
  name: string
  url: string
}

/**
 * Adds a link to a record, or changes one. Shown only to a viewer the server
 * says may manage the record's links; the server checks again on save.
 */
const LinkForm = ({
  owner,
  link,
  onFormComplete,
  onFormCancel,
}: LinkFormProps) => {
  const messageApi = useMessage()

  const [createLink] = useCreateLinkMutation()
  const [updateLink] = useUpdateLinkMutation()

  const isEdit = link !== undefined

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<LinkFormValues>({
      onSubmit: async (values: LinkFormValues, form) => {
        const request: LinkRequest = { name: values.name, url: values.url }

        try {
          const response = link
            ? await updateLink({ ...owner, linkId: link.id, request })
            : await createLink({ ...owner, request })
          if (response.error) {
            throw response.error
          }

          messageApi.success(
            isEdit
              ? 'Successfully updated link.'
              : 'Successfully created link.',
          )
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                `An unexpected error occurred while ${isEdit ? 'updating' : 'creating'} the link.`,
            )
            console.error(error)
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
    })

  useEffect(() => {
    if (!link || !isOpen) return
    form.setFieldsValue({ name: link.name, url: link.url })
  }, [form, link, isOpen])

  return (
    <Modal
      title={isEdit ? 'Edit Link' : 'Create Link'}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText={isEdit ? 'Save' : 'Create'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="link-form">
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
          extra="A full web address, starting with http:// or https://."
          rules={[{ required: true }, { type: 'url' }]}
        >
          <TextArea autoSize={{ minRows: 6, maxRows: 10 }} />
        </Item>
      </Form>
    </Modal>
  )
}

export default LinkForm
