'use client'

import {
  DatePicker,
  Descriptions,
  Flex,
  Form,
  Input,
  Modal,
  Radio,
  Select,
} from 'antd'
import { useEffect, useState } from 'react'
import { RiskDetailsDto, UpdateRiskRequest } from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import dayjs, { type Dayjs } from 'dayjs'
import { MarkdownEditor } from '../markdown'
import {
  useGetRiskCategoryOptionsQuery,
  useGetRiskGradeOptionsQuery,
  useGetRiskQuery,
  useGetRiskStatusOptionsQuery,
  useUpdateRiskMutation,
} from '@/src/store/features/planning/risks-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import { useModalForm } from '@/src/hooks'

const { Item } = Descriptions
const { Item: FormItem } = Form
const { TextArea } = Input
const { Group: RadioGroup } = Radio

export interface EditRiskFormProps {
  riskKey: number
  onFormSave: () => void
  onFormCancel: () => void
}

interface EditRiskFormValues {
  riskId: string
  teamId: string
  summary: string
  description?: string | undefined
  statusId: number
  categoryId: number
  impactId: number
  likelihoodId: number
  assigneeId?: string | undefined
  followUpDate?: Dayjs | undefined
  response?: string | undefined
}

const mapToRequestValues = (values: EditRiskFormValues) => {
  return {
    riskId: values.riskId,
    teamId: values.teamId,
    summary: values.summary,
    description: values.description,
    statusId: values.statusId,
    categoryId: values.categoryId,
    impactId: values.impactId,
    likelihoodId: values.likelihoodId,
    assigneeId: values.assigneeId,
    followUpDate: values.followUpDate?.format('YYYY-MM-DD'),
    response: values.response,
  } as unknown as UpdateRiskRequest
}

const EditRiskForm = ({
  riskKey,
  onFormSave,
  onFormCancel,
}: EditRiskFormProps) => {
  const [teamName, setTeamName] = useState<string>('')

  const { data: riskData } = useGetRiskQuery(riskKey)

  const [updateRisk] = useUpdateRiskMutation()

  const { data: riskStatusOptions } = useGetRiskStatusOptionsQuery()
  const { data: riskCategoryOptions } = useGetRiskCategoryOptionsQuery()
  const { data: riskGradeOptions } = useGetRiskGradeOptionsQuery()
  const { data: employeeOptions } = useGetEmployeeOptionsQuery(true)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditRiskFormValues>({
      onSubmit: async (values: EditRiskFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await updateRisk({ request, cacheKey: riskKey })

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
      onComplete: onFormSave,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the risk. Please try again.',
      permission: 'Permissions.Risks.Update',
    })

  useEffect(() => {
    if (!riskData || !isOpen) return
    const mapToFormValues = (risk: RiskDetailsDto) => {
      form.setFieldsValue({
        riskId: risk.id,
        teamId: risk.team?.id,
        summary: risk.summary,
        description: risk.description || '',
        statusId: risk.status.id,
        categoryId: risk.category.id,
        impactId: risk.impact.id,
        likelihoodId: risk.likelihood.id,
        assigneeId: risk.assignee?.id,
        followUpDate: risk.followUpDate ? dayjs(risk.followUpDate) : undefined,
        response: risk.response || '',
      })
    }
    setTeamName(riskData.team?.name ?? '')
    mapToFormValues(riskData)
  }, [form, riskData, isOpen])

  return (
    <Modal
      title="Edit Team Risk"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap="small">
        <Descriptions size="small" column={1}>
          <Item label="Number">{riskKey}</Item>
          <Item label="Team">{teamName}</Item>
        </Descriptions>
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="update-risk-form"
        >
          <FormItem name="riskId" hidden={true}>
            <Input />
          </FormItem>
          <FormItem name="teamId" hidden={true}>
            <Input />
          </FormItem>
          <FormItem label="Summary" name="summary" rules={[{ required: true }]}>
            <TextArea
              autoSize={{ minRows: 2, maxRows: 4 }}
              showCount
              maxLength={256}
            />
          </FormItem>
          <FormItem
            name="description"
            label="Description"
            rules={[{ max: 1024 }]}
          >
            <MarkdownEditor
              value={form.getFieldValue('description')}
              onChange={(value) =>
                form.setFieldValue('description', value || '')
              }
              maxLength={1024}
            />
          </FormItem>
          <FormItem name="statusId" label="Status" rules={[{ required: true }]}>
            <RadioGroup
              options={riskStatusOptions}
              optionType="button"
              buttonStyle="solid"
            />
          </FormItem>
          <FormItem
            name="categoryId"
            label="Category"
            rules={[{ required: true }]}
          >
            <RadioGroup
              options={riskCategoryOptions}
              optionType="button"
              buttonStyle="solid"
            />
          </FormItem>
          <FormItem name="impactId" label="Impact" rules={[{ required: true }]}>
            <RadioGroup
              options={riskGradeOptions}
              optionType="button"
              buttonStyle="solid"
            />
          </FormItem>
          <FormItem
            name="likelihoodId"
            label="Likelihood"
            rules={[{ required: true }]}
          >
            <RadioGroup
              options={riskGradeOptions}
              optionType="button"
              buttonStyle="solid"
            />
          </FormItem>
          <FormItem name="assigneeId" label="Assignee">
            <Select
              allowClear
              showSearch
              placeholder="Select an assignee"
              optionFilterProp="children"
              filterOption={(input, option) =>
                (option?.label.toLowerCase() ?? '').includes(
                  input.toLowerCase(),
                )
              }
              filterSort={(optionA, optionB) =>
                (optionA?.label ?? '')
                  .toLowerCase()
                  .localeCompare((optionB?.label ?? '').toLowerCase())
              }
              options={employeeOptions}
            />
          </FormItem>
          <FormItem label="Follow Up" name="followUpDate">
            <DatePicker />
          </FormItem>
          <FormItem name="response" label="Response" rules={[{ max: 1024 }]}>
            <MarkdownEditor
              value={form.getFieldValue('response')}
              onChange={(value) => form.setFieldValue('response', value || '')}
              maxLength={1024}
            />
          </FormItem>
        </Form>
      </Flex>
    </Modal>
  )
}

export default EditRiskForm
