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
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import dayjs, { type Dayjs } from 'dayjs'
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
  start: Dayjs
  end: Dayjs
}

interface ManagePlanningIntervalDatesFormValues {
  start: Dayjs
  end: Dayjs
  iterations: UpsertIterationFormValues[]
}

const mapToRequestValues = (
  values: ManagePlanningIntervalDatesFormValues,
  id: string,
) => {
  return {
    id,
    start: values.start?.format('YYYY-MM-DD'),
    end: values.end?.format('YYYY-MM-DD'),
    iterations: values.iterations.map((iteration) => ({
      iterationId: iteration.iterationId,
      name: iteration.name,
      categoryId: iteration.categoryId,
      start: iteration.start?.format('YYYY-MM-DD'),
      end: iteration.end?.format('YYYY-MM-DD'),
    })),
  } as unknown as ManagePlanningIntervalDatesRequest
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
            const formErrors = toFormErrors(apiError.errors)
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
      start: dayjs(planningIntervalData.start),
      end: dayjs(planningIntervalData.end),
      iterations: iterationsData.map((iteration) => ({
        iterationId: iteration.id,
        name: iteration.name,
        categoryId: iteration.category.id,
        start: dayjs(iteration.start),
        end: dayjs(iteration.end),
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
        <Item label="Start" name="start" rules={[{ required: true }]}>
          <DatePicker />
        </Item>
        <Item
          label="End"
          name="end"
          dependencies={['start']}
          rules={[
            { required: true },
            ({ getFieldValue }) => ({
              validator(_, value) {
                const start = getFieldValue('start')
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
          <DatePicker />
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
                          style={{ margin: '0' }}
                          name={[name, 'start']}
                          rules={[{ required: true, message: 'Missing start' }]}
                        >
                          <DatePicker />
                        </Item>
                        <Item
                          {...restField}
                          style={{ margin: '0' }}
                          name={[name, 'end']}
                          rules={[{ required: true, message: 'Missing end' }]}
                        >
                          <DatePicker />
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
