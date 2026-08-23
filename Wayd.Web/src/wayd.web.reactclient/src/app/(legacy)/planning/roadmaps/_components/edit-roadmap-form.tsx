'use client'

import { UpdateRoadmapRequest } from '@/src/services/wayd-api'
import {
  useUpdateRoadmapMutation,
  useGetVisibilityOptionsQuery,
  useGetRoadmapQuery,
} from '@/src/store/features/planning/roadmaps-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Input, Modal, Radio } from 'antd'
import { useEffect } from 'react'
import dayjs from 'dayjs'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import useAuth from '@/src/components/contexts/auth'
import { MarkdownEditor } from '@/src/components/common/markdown'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input
const { Group: RadioGroup } = Radio
const { RangePicker } = DatePicker

export interface EditRoadmapFormProps {
  roadmapKey: number
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditRoadmapFormValues {
  name: string
  description?: string
  range: any[]
  roadmapManagerIds: string[]
  visibilityId: number
}

const mapToRequestValues = (
  values: EditRoadmapFormValues,
  objectiveId: string,
): UpdateRoadmapRequest => {
  return {
    id: objectiveId,
    name: values.name,
    description: values.description,
    start: (values.range?.[0] as any)?.format('YYYY-MM-DD'),
    end: (values.range?.[1] as any)?.format('YYYY-MM-DD'),
    roadmapManagerIds: values.roadmapManagerIds,
    visibilityId: values.visibilityId,
  } as UpdateRoadmapRequest
}

const EditRoadmapForm = ({
  roadmapKey,
  onFormComplete,
  onFormCancel,
}: EditRoadmapFormProps) => {
  const messageApi = useMessage()
  const { user } = useAuth()
  const currentUserInternalEmployeeId = user?.employeeId

  const { data: roadmapData, error } = useGetRoadmapQuery(roadmapKey.toString())

  const { data: visibilityData, error: visibilityError } =
    useGetVisibilityOptionsQuery()
  const [updateRoadmap] = useUpdateRoadmapMutation()

  const { data: employeeData, error: employeeOptionsError } =
    useGetEmployeeOptionsQuery(true)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditRoadmapFormValues>({
      onSubmit: async (values: EditRoadmapFormValues, form) => {
          try {
            const request = mapToRequestValues(values, roadmapData!.id)
            const response = await updateRoadmap({
              request,
              cacheKey: roadmapData!.key,
            })
            if (response.error) throw response.error
            messageApi.success('Roadmap updated successfully.')
            return true
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            console.error('update error', error)
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                (isApiError(apiError) ? apiError.detail : undefined) ??
                  'An error occurred while updating the roadmap. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the roadmap. Please try again.',
      permission: 'Permissions.Roadmaps.Update',
    })

  // Initialize form values when data is loaded
  useEffect(() => {
    if (!roadmapData || !visibilityData) return
    form.setFieldsValue({
      name: roadmapData.name,
      description: roadmapData.description || '',
      range: [dayjs(roadmapData.start), dayjs(roadmapData.end)],
      visibilityId: roadmapData.visibility.id,
      roadmapManagerIds: roadmapData.roadmapManagers.map(
        (rm: { id: string }) => rm.id,
      ),
    })
  }, [roadmapData, visibilityData, form])

  // Query error display
  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading the roadmap. Please try again.',
      )
    }
    if (visibilityError) {
      messageApi.error(
        (isApiError(visibilityError) ? visibilityError.supportMessage : undefined) ??
          'An error occurred while loading visibility options. Please try again.',
      )
    }
    if (employeeOptionsError) {
      messageApi.error(
        (isApiError(employeeOptionsError) ? employeeOptionsError.supportMessage : undefined) ??
          'An error occurred while loading employee options. Please try again.',
      )
    }
  }, [
    employeeOptionsError,
    error,
    messageApi,
    visibilityError,
  ])

  return (
    <Modal
      title="Edit Roadmap"
      open={isOpen}
      width="60vw"
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="update-roadmap-form"
      >
        <Item label="Name" name="name" rules={[{ required: true }]}>
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
        </Item>
        <Item name="description" label="Description" rules={[{ max: 2048 }]}>
          <MarkdownEditor
            value={form.getFieldValue('description')}
            onChange={(value) => form.setFieldValue('description', value || '')}
            maxLength={2048}
          />
        </Item>
        <Item
          name="range"
          label="Dates"
          rules={[
            { required: true, message: 'Select start and end dates' },
            {
              validator: (_, value) => {
                if (!value || !value[0] || !value[1]) {
                  return Promise.reject(
                    new Error('Start and end dates are required'),
                  )
                }
                const [start, end] = value
                if (!start || !end || end.isBefore(start, 'day')) {
                  return Promise.reject(
                    new Error('End date must be on or after start date'),
                  )
                }
                return Promise.resolve()
              },
            },
          ]}
        >
          <RangePicker />
        </Item>
        <Item
          name="roadmapManagerIds"
          label="Roadmap Managers"
          rules={[
            {
              required: true,
              message: 'Select at least one roadmap manager',
            },
            {
              validator: async (_, value) => {
                if (!value.includes(currentUserInternalEmployeeId)) {
                  return Promise.reject(
                    new Error(
                      'You must also be a roadmap manager to update this roadmap',
                    ),
                  )
                }
                return Promise.resolve()
              },
            },
          ]}
        >
          <EmployeeSelect
            employees={employeeData ?? []}
            allowMultiple={true}
            placeholder="Select one or more roadmap managers"
          />
        </Item>
        <Item
          name="visibilityId"
          label="Visibility"
          rules={[{ required: true }]}
        >
          <RadioGroup
            options={visibilityData}
            optionType="button"
            buttonStyle="solid"
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditRoadmapForm
