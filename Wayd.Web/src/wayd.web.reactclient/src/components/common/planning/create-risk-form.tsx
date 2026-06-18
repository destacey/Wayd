'use client'

import { DatePicker, Form, Input, Modal, Radio, Select } from 'antd'
import { useEffect } from 'react'
import { CreateRiskRequest } from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { type Dayjs } from 'dayjs'
import { MarkdownEditor } from '../markdown'
import {
  useCreateRiskMutation,
  useGetRiskCategoryOptionsQuery,
  useGetRiskGradeOptionsQuery,
} from '@/src/store/features/planning/risks-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import { useGetTeamOptionsQuery } from '@/src/store/features/organizations/team-api'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input
const { Group: RadioGroup } = Radio

export interface CreateRiskFormProps {
  createForTeamId?: string | undefined
  onFormCreate: () => void
  onFormCancel: () => void
}

interface CreateRiskFormValues {
  teamId: string
  summary: string
  description?: string | undefined
  categoryId: number
  impactId: number
  likelihoodId: number
  assigneeId?: string | undefined
  followUpDate?: Dayjs | undefined
  response?: string | undefined
}

const mapToRequestValues = (values: CreateRiskFormValues) => {
  return {
    teamId: values.teamId,
    summary: values.summary,
    description: values.description,
    categoryId: values.categoryId,
    impactId: values.impactId,
    likelihoodId: values.likelihoodId,
    assigneeId: values.assigneeId,
    followUpDate: values.followUpDate?.format('YYYY-MM-DD'),
    response: values.response,
  } as unknown as CreateRiskRequest
}

const CreateRiskForm = ({
  createForTeamId,
  onFormCreate,
  onFormCancel,
}: CreateRiskFormProps) => {
  const [createRisk] = useCreateRiskMutation()
  const { data: riskCategoryOptions } = useGetRiskCategoryOptionsQuery()
  const { data: riskGradeOptions } = useGetRiskGradeOptionsQuery()
  const { data: employeeOptions } = useGetEmployeeOptionsQuery(false)

  const { data: teamOptions, isLoading: isTeamsLoading } =
    useGetTeamOptionsQuery(false)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateRiskFormValues>({
      onSubmit: async (values: CreateRiskFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await createRisk(request)
          if (response.error) {
            throw response.error
          }

          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
          } else {
            throw error
          }
          return false
        }
      },
      onComplete: onFormCreate,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the risk. Please try again.',
      permission: 'Permissions.Risks.Create',
    })

  useEffect(() => {
    if (!isOpen) return
    form.setFieldsValue({
      teamId: createForTeamId,
      description: '',
      response: '',
    })
  }, [form, isOpen, createForTeamId])

  return (
    <Modal
      title="Create Team Risk"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid || isTeamsLoading }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="create-risk-form">
        <Item name="teamId" label="Team" rules={[{ required: true }]}>
          <Select
            showSearch
            disabled={createForTeamId !== undefined}
            placeholder="Select a team"
            loading={isTeamsLoading}
            optionFilterProp="children"
            filterOption={(input, option) =>
              (option?.label.toLowerCase() ?? '').includes(input.toLowerCase())
            }
            filterSort={(optionA, optionB) =>
              (optionA?.label ?? '')
                .toLowerCase()
                .localeCompare((optionB?.label ?? '').toLowerCase())
            }
            options={teamOptions}
          />
        </Item>
        <Item label="Summary" name="summary" rules={[{ required: true }]}>
          <TextArea
            autoSize={{ minRows: 2, maxRows: 4 }}
            showCount
            maxLength={256}
          />
        </Item>
        <Item name="description" label="Description" rules={[{ max: 1024 }]}>
          <MarkdownEditor
            value={form.getFieldValue('description')}
            onChange={(value) => form.setFieldValue('description', value || '')}
            maxLength={1024}
          />
        </Item>
        <Item name="categoryId" label="Category" rules={[{ required: true }]}>
          <RadioGroup
            options={riskCategoryOptions}
            optionType="button"
            buttonStyle="solid"
          />
        </Item>
        <Item name="impactId" label="Impact" rules={[{ required: true }]}>
          <RadioGroup
            options={riskGradeOptions}
            optionType="button"
            buttonStyle="solid"
          />
        </Item>
        <Item
          name="likelihoodId"
          label="Likelihood"
          rules={[{ required: true }]}
        >
          <RadioGroup
            options={riskGradeOptions}
            optionType="button"
            buttonStyle="solid"
          />
        </Item>
        <Item name="assigneeId" label="Assignee">
          <Select
            allowClear
            showSearch
            placeholder="Select an assignee"
            optionFilterProp="children"
            filterOption={(input, option) =>
              (option?.label.toLowerCase() ?? '').includes(input.toLowerCase())
            }
            filterSort={(optionA, optionB) =>
              (optionA?.label ?? '')
                .toLowerCase()
                .localeCompare((optionB?.label ?? '').toLowerCase())
            }
            options={employeeOptions}
          />
        </Item>
        <Item label="Follow Up" name="followUpDate">
          <DatePicker />
        </Item>
        <Item name="response" label="Response" rules={[{ max: 1024 }]}>
          <MarkdownEditor
            value={form.getFieldValue('response')}
            onChange={(value) => form.setFieldValue('response', value || '')}
            maxLength={1024}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateRiskForm
