'use client'

import { Form, Input, Modal } from 'antd'
import { useEffect, useState } from 'react'
import { TeamDetailsDto, TeamOfTeamsDetailsDto } from '@/src/services/wayd-api'
import { EditTeamFormValues } from '../types'
import { useUpdateTeamMutation } from '@/src/store/features/organizations/team-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { MarkdownEditor } from '@/src/components/common/markdown'

const { Item } = Form

interface EditTeamFormProps {
  team: TeamDetailsDto | TeamOfTeamsDetailsDto
  open: boolean
  onClose: (success: boolean) => void
}

const EditTeamForm = ({ team, open, onClose }: EditTeamFormProps) => {
  const [form] = Form.useForm<EditTeamFormValues>()
  const formValues = Form.useWatch([], form)
  const [isValid, setIsValid] = useState(false)
  const [updateTeam, { isLoading, error, reset }] = useUpdateTeamMutation()
  const messageApi = useMessage()

  useEffect(() => {
    if (!team || !open) return
    form.setFieldsValue({
      id: team.id,
      key: team.key,
      name: team.name,
      code: team.code,
      description: team.description || '',
      type: team.type,
    } as EditTeamFormValues)
  }, [form, team, open])

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
      const result = await updateTeam(values)
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
      title="Edit Team"
      open={open}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isLoading}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden={true}
    >
      <Form form={form} size="small" layout="vertical" name="update-team-form">
        <Item name="id" hidden={true}>
          <Input />
        </Item>
        <Item name="key" hidden={true}>
          <Input />
        </Item>
        <Item name="type" hidden={true}>
          <Input />
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
        <Item name="description" label="Description" rules={[{ max: 1024 }]}>
          <MarkdownEditor
            value={form.getFieldValue('description')}
            onChange={(value) => form.setFieldValue('description', value || '')}
            maxLength={1024}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditTeamForm
