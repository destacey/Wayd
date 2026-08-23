'use client'

import {
  Alert,
  DatePicker,
  Descriptions,
  Form,
  Input,
  Modal,
  Radio,
  Slider,
  Switch,
} from 'antd'
import { useEffect } from 'react'
import { UpdatePlanningIntervalObjectiveRequest } from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import dayjs, { type Dayjs } from 'dayjs'
import { RangePickerProps } from 'antd/es/date-picker'
import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  useGetPlanningIntervalObjectiveQuery,
  useGetPlanningIntervalObjectiveStatusOptionsQuery,
  useGetPlanningIntervalQuery,
  useUpdatePlanningIntervalObjectiveMutation,
} from '@/src/store/features/planning/planning-interval-api'
import { useModalForm } from '@/src/hooks'

const { Item } = Descriptions
const { Item: FormItem } = Form
const { TextArea } = Input
const { Group: RadioGroup } = Radio

export interface EditPlanningIntervalObjectiveFormProps {
  planningIntervalKey: number
  objectiveKey: number
  onFormSave: () => void
  onFormCancel: () => void
}

interface EditPlanningIntervalObjectiveFormValues {
  objectiveId: string
  planningIntervalId: string
  teamId: string
  name: string
  statusId: number
  description?: string | null
  isStretch: boolean
  progress: number
  startDate?: Dayjs | null
  targetDate?: Dayjs | null
}

const mapToRequestValues = (
  values: EditPlanningIntervalObjectiveFormValues,
  objectiveKey: number,
  planningIntervalKey: number,
  teamId: string,
) => {
  const objective = {
    planningIntervalId: values.planningIntervalId,
    objectiveId: values.objectiveId,
    name: values.name,
    description: values.description,
    statusId: values.statusId,
    progress: values.progress,
    startDate: values.startDate?.format('YYYY-MM-DD'),
    targetDate: values.targetDate?.format('YYYY-MM-DD'),
    isStretch: values.isStretch,
  } as unknown as UpdatePlanningIntervalObjectiveRequest

  return {
    request: objective,
    objectiveKey,
    planningIntervalKey,
    teamId,
  }
}

const EditPlanningIntervalObjectiveForm = ({
  planningIntervalKey,
  objectiveKey,
  onFormSave,
  onFormCancel,
}: EditPlanningIntervalObjectiveFormProps) => {
  const messageApi = useMessage()

  const { data: planningIntervalData } =
    useGetPlanningIntervalQuery(planningIntervalKey)

  const { data: objectiveData } = useGetPlanningIntervalObjectiveQuery({
    planningIntervalKey: planningIntervalKey.toString(),
    objectiveKey: objectiveKey.toString(),
  })

  const { data: statusOptions } =
    useGetPlanningIntervalObjectiveStatusOptionsQuery()
  const [updateObjective] = useUpdatePlanningIntervalObjectiveMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditPlanningIntervalObjectiveFormValues>({
      onSubmit: async (values: EditPlanningIntervalObjectiveFormValues, form) => {
        try {
          const request = mapToRequestValues(
            values,
            objectiveKey,
            planningIntervalKey,
            objectiveData!.team.id,
          )
          const response = await updateObjective(request)
          if (response.error) {
            throw response.error
          }
          messageApi.success('PI objective updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              'An unexpected error occurred while updating the PI objective.',
            )
            console.error(error)
          }
          return false
        }
      },
      onComplete: onFormSave,
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while updating the PI objective.',
      permission: 'Permissions.PlanningIntervalObjectives.Manage',
    })

  useEffect(() => {
    if (!objectiveData) return
    form.setFieldsValue({
      objectiveId: objectiveData.id,
      planningIntervalId: objectiveData.planningInterval.id,
      teamId: objectiveData.team.id,
      statusId: objectiveData.status.id,
      name: objectiveData.name,
      description: objectiveData.description || '',
      startDate: objectiveData.startDate
        ? dayjs(objectiveData.startDate)
        : undefined,
      targetDate: objectiveData.targetDate
        ? dayjs(objectiveData.targetDate)
        : undefined,
      progress: objectiveData.progress,
      isStretch: objectiveData.isStretch,
    })
  }, [objectiveData, form])

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
        title="Edit PI Objective"
        open={isOpen}
        onOk={handleOk}
        okButtonProps={{ disabled: !isValid }}
        okText="Save"
        confirmLoading={isSaving}
        onCancel={handleCancel}
        keyboard={false}
        destroyOnHidden
      >
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="update-objective-form"
        >
          {planningIntervalData?.objectivesLocked && (
            <Alert title="PI Objectives are locked." type="info" showIcon />
          )}
          <Descriptions size="small" column={1}>
            <Item label="Key">{objectiveData?.key}</Item>
            <Item label="Team">{objectiveData?.team.name}</Item>
          </Descriptions>
          <FormItem name="objectiveId" hidden={true}>
            <Input />
          </FormItem>
          <FormItem name="planningIntervalId" hidden={true}>
            <Input />
          </FormItem>
          <FormItem name="teamId" hidden={true}>
            <Input />
          </FormItem>
          <FormItem label="Name" name="name" rules={[{ required: true }]}>
            <TextArea
              autoSize={{ minRows: 2, maxRows: 4 }}
              showCount
              maxLength={256}
              disabled={planningIntervalData?.objectivesLocked}
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
          <FormItem
            label="Is Stretch?"
            name="isStretch"
            valuePropName="checked"
          >
            <Switch
              checkedChildren="Yes"
              unCheckedChildren="No"
              disabled={planningIntervalData?.objectivesLocked}
            />
          </FormItem>
          <FormItem name="statusId" label="Status" rules={[{ required: true }]}>
            <RadioGroup
              options={statusOptions}
              optionType="button"
              buttonStyle="solid"
            />
          </FormItem>
          <FormItem label="Progress" name="progress">
            <Slider />
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

export default EditPlanningIntervalObjectiveForm
