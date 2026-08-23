'use client'

import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { CopyRoadmapRequest } from '@/src/services/wayd-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import {
  useCopyRoadmapMutation,
  useGetVisibilityOptionsQuery,
} from '@/src/store/features/planning/roadmaps-api'
import useAuth from '@/src/components/contexts/auth'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal, Radio } from 'antd'
import { useRouter } from 'next/navigation'
import { useEffect } from 'react'

const { Item } = Form
const { TextArea } = Input
const { Group: RadioGroup } = Radio

export interface CopyRoadmapFormProps {
  sourceRoadmapId: string
  sourceRoadmapName: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CopyRoadmapFormValues {
  name: string
  roadmapManagerIds: string[]
  visibilityId: number
}

const mapToRequestValues = (
  values: CopyRoadmapFormValues,
  sourceRoadmapId: string,
): CopyRoadmapRequest => {
  return {
    sourceRoadmapId: sourceRoadmapId,
    name: values.name,
    roadmapManagerIds: values.roadmapManagerIds,
    visibilityId: values.visibilityId,
  } as CopyRoadmapRequest
}

const CopyRoadmapForm = ({
  sourceRoadmapId,
  sourceRoadmapName,
  onFormComplete,
  onFormCancel,
}: CopyRoadmapFormProps) => {
  const router = useRouter()
  const messageApi = useMessage()
  const { user } = useAuth()
  const currentUserInternalEmployeeId = user?.employeeId

  const {
    data: visibilityData,
    error,
  } = useGetVisibilityOptionsQuery()
  const [copyRoadmap] = useCopyRoadmapMutation()

  const {
    data: employeeData,
    error: employeeOptionsError,
  } = useGetEmployeeOptionsQuery(false)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CopyRoadmapFormValues>({
      onSubmit: async (values: CopyRoadmapFormValues, form) => {
          try {
            const request = mapToRequestValues(values, sourceRoadmapId)
            const response = await copyRoadmap(request)
            if (response.error) throw response.error

            messageApi.success(
              `Roadmap copied successfully. Roadmap key ${response.data!.key}`,
            )

            // Navigate to the new roadmap
            router.push(`/planning/roadmaps/${response.data!.key}`)

            return true
          } catch (error) {
            const apiError: ApiError = isApiError(error) ? error : {}
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                (isApiError(apiError) ? apiError.detail : undefined) ??
                  'An error occurred while copying the roadmap. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while copying the roadmap. Please try again.',
      permission: 'Permissions.Roadmaps.Create',
    })

  // Initialize form defaults when opened
  useEffect(() => {
    if (isOpen && currentUserInternalEmployeeId) {
      form.setFieldsValue({
        name: `Copy of ${sourceRoadmapName}`,
        roadmapManagerIds: [currentUserInternalEmployeeId],
      })
    }
  }, [isOpen, currentUserInternalEmployeeId, sourceRoadmapName, form])

  // Query error display
  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
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
  ])

  return (
    <Modal
      title="Copy Roadmap"
      open={isOpen}
      width={'60vw'}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Copy"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="copy-roadmap-form"
      >
        <Item label="Name" name="name" rules={[{ required: true }]}>
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
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
                      'You must also be a roadmap manager to copy this roadmap',
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

export default CopyRoadmapForm
