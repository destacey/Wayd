'use client'

import { DatePicker, Form, Input, Modal, Radio } from 'antd'
import { useEffect, useState } from 'react'
import { CreateTeamFormValues } from '../types'
import { useCreateTeamMutation } from '@/src/store/features/organizations/team-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { MarkdownEditor } from '@/src/components/common/markdown'

const { Item } = Form
const { Group: RadioGroup } = Radio

interface ModalCreateTeamFormProps {
  open: boolean
  onClose: (success: boolean) => void
}

export const ModalCreateTeamForm = ({ open, onClose }: ModalCreateTeamFormProps) => {
  const [form] = Form.useForm<CreateTeamFormValues>()
  const formValues = Form.useWatch([], form)
  const [isValid, setIsValid] = useState(false)
  const [createTeam, { isLoading, error, reset }] = useCreateTeamMutation()
  const messageApi = useMessage()

  useEffect(() => {
    form.validateFields({ validateOnly: true }).then(
      () => setIsValid(form.isFieldsTouched()),
      () => setIsValid(false),
    )
  }, [form, formValues])

  useEffect(() => {
    if (error) {
      console.error(error)
      const apiError: ApiError = isApiError(error) ? error : {}
      if (apiError.status === 422 && apiError.errors) {
        form.setFields(toFormErrors(apiError.errors))
        messageApi.error('Correct the validation error(s) to continue.')
      } else {
        messageApi.error(apiError.detail ?? 'An unexpected error occurred while saving.')
      }
    }
  }, [error, form, messageApi])

  const handleOk = async () => {
    try {
      const values = await form.validateFields()
      const result = await createTeam(values)
      if (!('error' in result)) {
        form.resetFields()
        reset()
        onClose(true)
      }
    } catch (errorInfo) {
      console.error(errorInfo)
    }
  }

  const handleCancel = () => {
    form.resetFields()
    reset()
    onClose(false)
  }

  return (
    <Modal
      title="Create Team"
      open={open}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isLoading}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden={true}
    >
      <Form form={form} size="small" layout="vertical" name="create-team-form">
        <Item label="Team Type" name="type" rules={[{ required: true }]}>
          <RadioGroup>
            <Radio value="Team">Team</Radio>
            <Radio value="Team of Teams">Team of Teams</Radio>
          </RadioGroup>
        </Item>
        <Item label="Name" name="name" rules={[{ required: true }]}>
          <Input showCount maxLength={128} />
        </Item>
        <Item
          label="Code"
          name="code"
          rules={[
            { required: true, message: 'The Code field is required.' },
            {
              min: 2,
              max: 10,
              message: 'The Code field must be between 2-10 characters.',
            },
            {
              pattern: /^[A-Z0-9]+$/,
              message:
                'The Code field is invalid. Uppercase letters and numbers only.',
            },
          ]}
          normalize={(value) => (value ?? '').toUpperCase()}
        >
          <Input showCount maxLength={10} />
        </Item>
        <Item
          name="description"
          label="Description"
          initialValue=""
          rules={[{ max: 1024 }]}
        >
          <MarkdownEditor
            value={form.getFieldValue('description')}
            onChange={(value) => form.setFieldValue('description', value || '')}
            maxLength={1024}
          />
        </Item>
        <Item name="activeDate" label="Active Date" rules={[{ required: true }]}>
          <DatePicker />
        </Item>
      </Form>
    </Modal>
  )
}

export default ModalCreateTeamForm
