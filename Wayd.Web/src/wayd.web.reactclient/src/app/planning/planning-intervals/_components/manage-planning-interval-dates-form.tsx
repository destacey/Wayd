'use client'

import {
  Button,
  DatePicker,
  Divider,
  Flex,
  Form,
  Input,
  Modal,
  Select,
} from 'antd'
import { useEffect } from 'react'
import { ManagePlanningIntervalDatesRequest } from '@/src/services/wayd-api'
import {
  parseCalendarDate,
  toCalendarDate,
  toFormErrors,
  isApiError,
  type ApiError,
} from '@/src/utils'
import { type Dayjs } from 'dayjs'
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  useGetPlanningIntervalIterationCategoryOptionsQuery,
  useGetPlanningIntervalIterationsQuery,
  useGetPlanningIntervalQuery,
  useUpdatePlanningIntervalDatesMutation,
} from '@/src/store/features/planning/planning-interval-api'
import { useModalForm } from '@/src/hooks'

const { Item, List } = Form
const { RangePicker } = DatePicker

export interface ManagePlanningIntervalDatesFormProps {
  id: string
  planningIntervalKey: number
  onFormSave: () => void
  onFormCancel: () => void
}

interface UpsertIterationFormValues {
  iterationId?: string
  name: string
  categoryId: number
  /** The iteration's first and last days. */
  dates: [Dayjs, Dayjs]
}

interface ManagePlanningIntervalDatesFormValues {
  /** The PI's first and last days. */
  dates: [Dayjs, Dayjs]
  iterations: UpsertIterationFormValues[]
}

const mapToRequestValues = (
  values: ManagePlanningIntervalDatesFormValues,
  id: string,
) => {
  return {
    id,
    start: toCalendarDate(values.dates[0]),
    end: toCalendarDate(values.dates[1]),
    iterations: values.iterations.map((iteration) => ({
      iterationId: iteration.iterationId,
      name: iteration.name,
      categoryId: iteration.categoryId,
      start: toCalendarDate(iteration.dates[0]),
      end: toCalendarDate(iteration.dates[1]),
    })),
  } as unknown as ManagePlanningIntervalDatesRequest
}

/**
 * Shows the server's `start` and `end` errors on the range picker that holds
 * both, for the PI and for each iteration row.
 */
export const fieldForError = (key: string) => {
  if (key === 'start' || key === 'end') return 'dates'
  const iteration = /^iterations\[(\d+)\]\.(start|end)$/.exec(key)
  return iteration ? ['iterations', Number(iteration[1]), 'dates'] : undefined
}

const ManagePlanningIntervalDatesForm = ({
  id,
  planningIntervalKey,
  onFormSave,
  onFormCancel,
}: ManagePlanningIntervalDatesFormProps) => {
  const messageApi = useMessage()

  const { data: planningIntervalData } =
    useGetPlanningIntervalQuery(planningIntervalKey)
  const { data: iterationsData } =
    useGetPlanningIntervalIterationsQuery(planningIntervalKey)
  const { data: iterationCategoriesOptions } =
    useGetPlanningIntervalIterationCategoryOptionsQuery()

  const [managePlanningIntervalDates] = useUpdatePlanningIntervalDatesMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ManagePlanningIntervalDatesFormValues>({
      onSubmit: async (values: ManagePlanningIntervalDatesFormValues, form) => {
        try {
          const request = mapToRequestValues(values, id)
          const response = await managePlanningIntervalDates({
            request,
            cacheKey: planningIntervalKey,
          })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Planning interval dates updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors, {
              fieldFor: fieldForError,
            })
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the planning interval dates.',
            )
            console.error(error)
          }
          return false
        }
      },
      onComplete: onFormSave,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the planning interval dates.',
      permission: 'Permissions.PlanningIntervals.Update',
    })

  useEffect(() => {
    if (!planningIntervalData || !iterationsData) return
    form.setFieldsValue({
      dates: [
        parseCalendarDate(planningIntervalData.start),
        parseCalendarDate(planningIntervalData.end),
      ],
      iterations: iterationsData.map((iteration) => ({
        iterationId: iteration.id,
        name: iteration.name,
        categoryId: iteration.category.id,
        dates: [
          parseCalendarDate(iteration.start),
          parseCalendarDate(iteration.end),
        ],
      })),
    })
  }, [planningIntervalData, iterationsData, form])

  return (
    <Modal
      title="Manage PI Dates"
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
        name="manage-planning-interval-dates-form"
      >
        <Item
          label="Date Range"
          name="dates"
          rules={[
            { required: true, message: 'Select the start and end dates.' },
            {
              validator: (_, value?: [Dayjs, Dayjs]) =>
                !value || value[1].isAfter(value[0], 'day')
                  ? Promise.resolve()
                  : Promise.reject(
                      new Error('End date must be after start date'),
                    ),
            },
          ]}
        >
          <RangePicker />
        </Item>

        <Divider titlePlacement="start">Iterations</Divider>
        <List name="iterations">
          {(fields, { add, remove }) => (
            <>
              {fields.map(({ key, name, ...restField }) => (
                <div key={key}>
                  <Flex align="center" justify="space-between">
                    <Flex vertical style={{ width: '90%' }}>
                      <Flex gap="small">
                        <Item
                          {...restField}
                          name={[name, 'iterationId']}
                          hidden={true}
                        >
                          <Input hidden={true} />
                        </Item>
                        <Item
                          {...restField}
                          style={{ width: '100%', marginBottom: '10px' }}
                          name={[name, 'name']}
                          rules={[{ required: true }]}
                        >
                          <Input
                            showCount
                            maxLength={128}
                            placeholder="Iteration Name"
                          />
                        </Item>
                      </Flex>
                      <Flex gap="small">
                        <Item
                          {...restField}
                          style={{ margin: '0', width: '35%' }}
                          name={[name, 'categoryId']}
                          rules={[{ required: true }]}
                        >
                          <Select
                            placeholder="Select Category"
                            options={iterationCategoriesOptions}
                          />
                        </Item>
                        <Item
                          {...restField}
                          style={{ margin: '0', flex: 1 }}
                          name={[name, 'dates']}
                          rules={[{ required: true, message: 'Missing dates' }]}
                        >
                          <RangePicker style={{ width: '100%' }} />
                        </Item>
                      </Flex>
                    </Flex>
                    <MinusCircleOutlined
                      onClick={() => remove(name)}
                      title="Remove Iteration"
                    />
                  </Flex>
                  <Divider dashed />
                </div>
              ))}
              <Item>
                <Button
                  type="dashed"
                  onClick={() => add()}
                  block
                  icon={<PlusOutlined />}
                >
                  Add Iteration
                </Button>
              </Item>
            </>
          )}
        </List>
      </Form>
    </Modal>
  )
}

export default ManagePlanningIntervalDatesForm
