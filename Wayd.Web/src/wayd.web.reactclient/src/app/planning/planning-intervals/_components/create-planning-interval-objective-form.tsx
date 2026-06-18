'use client'

import { DatePicker, Form, Input, Modal, Select, Switch } from 'antd'
import { useEffect, useState } from 'react'
import { CreatePlanningIntervalObjectiveRequest } from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import dayjs, { type Dayjs } from 'dayjs'
import { RangePickerProps } from 'antd/es/date-picker'
import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  useCreatePlanningIntervalObjectiveMutation,
  useGetPlanningIntervalObjectiveStatusesQuery,
  useGetPlanningIntervalQuery,
  useGetPlanningIntervalTeamsQuery,
} from '@/src/store/features/planning/planning-interval-api'
import { useModalForm } from '@/src/hooks'

const { Item: FormItem } = Form
const { TextArea } = Input

export interface CreatePlanningIntervalObjectiveFormProps {
  planningIntervalKey: number
  teamId?: string
  order?: number
  onFormCreate: () => void
  onFormCancel: () => void
}

interface CreatePlanningIntervalObjectiveFormValues {
  planningIntervalId: string
  teamId: string
  name: string
  description?: string | null
  isStretch: boolean
  startDate?: Dayjs | null
  targetDate?: Dayjs | null
}

interface PlanningIntervalTeamSelectItem {
  value: string
  label: string
}

const mapToRequestValues = (
  values: CreatePlanningIntervalObjectiveFormValues,
  order?: number,
): CreatePlanningIntervalObjectiveRequest => {
  return {
    planningIntervalId: values.planningIntervalId,
    teamId: values.teamId,
    name: values.name,
    description: values.description ?? undefined,
    isStretch: values.isStretch,
    startDate: values.startDate?.format('YYYY-MM-DD'),
    targetDate: values.targetDate?.format('YYYY-MM-DD'),
    order: order,
  } as unknown as CreatePlanningIntervalObjectiveRequest
}

const CreatePlanningIntervalObjectiveForm = ({
  planningIntervalKey,
  teamId,
  order,
  onFormCreate,
  onFormCancel,
}: CreatePlanningIntervalObjectiveFormProps) => {
  const messageApi = useMessage()
  const [teams, setTeams] = useState<PlanningIntervalTeamSelectItem[]>([])

  const { data: planningIntervalData } =
    useGetPlanningIntervalQuery(planningIntervalKey)
  const { data: teamData } =
    useGetPlanningIntervalTeamsQuery(planningIntervalKey)
  const { data: statusData } = useGetPlanningIntervalObjectiveStatusesQuery()

  const [createObjective] = useCreatePlanningIntervalObjectiveMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreatePlanningIntervalObjectiveFormValues>({
      onSubmit: async (values: CreatePlanningIntervalObjectiveFormValues, form) => {
        try {
          const request = mapToRequestValues(values, order)
          const response = await createObjective({
            request,
            planningIntervalKey: planningIntervalData!.key,
          })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Successfully created PI objective.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An unexpected error occurred while creating the PI objective.',
            )
            console.error(error)
          }
          return false
        }
      },
      onComplete: onFormCreate,
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while creating the PI objective.',
      permission: 'Permissions.PlanningIntervalObjectives.Manage',
    })

  useEffect(() => {
    if (!teamData || !statusData) return

    setTeams(
      teamData
        .filter((t) => t.type === 'Team')
        .map((t) => ({ value: t.id, label: t.name })),
    )
    form.setFieldsValue({
      planningIntervalId: planningIntervalData?.id,
      teamId: teamId,
      isStretch: false,
    })
  }, [teamData, statusData, planningIntervalData?.id, teamId, form])

  const disabledDate: RangePickerProps['disabledDate'] = (current) => {
    return (
      current < dayjs(planningIntervalData?.start) ||
      current > dayjs(planningIntervalData?.end)
    )
  }

  const isDateWithinPiRange = (date: Date) => {
    return (
      dayjs(planningIntervalData!.start) <= dayjs(date) &&
      dayjs(date) <= dayjs(planningIntervalData!.end)
    )
  }

  return (
    <>
      <Modal
        title="Create PI Objective"
        open={isOpen}
        onOk={handleOk}
        okButtonProps={{ disabled: !isValid }}
        okText="Create"
        confirmLoading={isSaving}
        onCancel={handleCancel}
        keyboard={false}
        destroyOnHidden
      >
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="create-objective-form"
          initialValues={{ isStretch: false }}
        >
          <FormItem name="planningIntervalId" hidden={true}>
            <Input />
          </FormItem>
          <FormItem name="teamId" label="Team" rules={[{ required: true }]}>
            <Select
              showSearch
              disabled={teamId !== undefined}
              placeholder="Select a team"
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
              options={teams}
            />
          </FormItem>
          <FormItem label="Name" name="name" rules={[{ required: true }]}>
            <TextArea
              autoSize={{ minRows: 2, maxRows: 4 }}
              showCount
              maxLength={256}
            />
          </FormItem>
          <FormItem
            name="description"
            label="Description"
            initialValue=""
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
          <FormItem
            label="Is Stretch?"
            name="isStretch"
            valuePropName="checked"
          >
            <Switch checkedChildren="Yes" unCheckedChildren="No" />
          </FormItem>
          <FormItem
            label="Start"
            name="startDate"
            rules={[
              {
                validator: (_, value: Date) =>
                  !value || isDateWithinPiRange(value)
                    ? Promise.resolve()
                    : Promise.reject(
                        'The start date must be within the Planning Interval dates.',
                      ),
              },
            ]}
          >
            <DatePicker disabledDate={disabledDate} />
          </FormItem>
          <FormItem
            label="Target"
            name="targetDate"
            dependencies={['startDate']}
            rules={[
              {
                validator: (_, value: Date) =>
                  !value || isDateWithinPiRange(value)
                    ? Promise.resolve()
                    : Promise.reject(
                        'The target date must be within the Planning Interval dates.',
                      ),
              },
              ({ getFieldValue }) => ({
                validator(_, value) {
                  const start = getFieldValue('startDate')
                  if (!value || !start || start < value) {
                    return Promise.resolve()
                  }
                  return Promise.reject(
                    new Error('End date must be after start date'),
                  )
                },
              }),
            ]}
          >
            <DatePicker disabledDate={disabledDate} />
          </FormItem>
        </Form>
      </Modal>
    </>
  )
}

export default CreatePlanningIntervalObjectiveForm
