'use client'

import { Form, Modal, Radio, Spin } from 'antd'
import { useEffect } from 'react'
import {
  Methodology,
  SizingMethod,
  UpdateTeamOperatingModelRequest,
} from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import {
  useGetTeamOperatingModelQuery,
  useUpdateTeamOperatingModelMutation,
} from '@/src/store/features/organizations/team-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'

const { Item: FormItem } = Form
const { Group: RadioGroup } = Radio

export interface EditTeamOperatingModelFormProps {
  teamId: string
  operatingModelId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditTeamOperatingModelFormValues {
  methodology: Methodology
  sizingMethod: SizingMethod
}

const methodologyOptions = [
  { value: Methodology.Scrum, label: 'Scrum' },
  { value: Methodology.Kanban, label: 'Kanban' },
]

const sizingMethodOptions = [
  { value: SizingMethod.StoryPoints, label: 'Story Points' },
  { value: SizingMethod.Count, label: 'Count' },
]

const mapToRequestValues = (
  values: EditTeamOperatingModelFormValues,
): UpdateTeamOperatingModelRequest => {
  return {
    methodology: values.methodology,
    sizingMethod: values.sizingMethod,
  } as UpdateTeamOperatingModelRequest
}

const EditTeamOperatingModelForm = ({
  teamId,
  operatingModelId,
  onFormComplete,
  onFormCancel,
}: EditTeamOperatingModelFormProps) => {
  const messageApi = useMessage()

  const {
    data: operatingModel,
    isLoading,
    isFetching,
  } = useGetTeamOperatingModelQuery({ teamId, operatingModelId })

  const [updateOperatingModel] = useUpdateTeamOperatingModelMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditTeamOperatingModelFormValues>({
      onSubmit: async (values: EditTeamOperatingModelFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          await updateOperatingModel({
            teamId,
            operatingModelId,
            request,
          }).unwrap()
          messageApi.success('Successfully updated operating model.')
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
                'An unexpected error occurred while updating the operating model.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while updating the operating model.',
      permission: 'Permissions.Teams.Update',
    })

  // Set form values when operating model data is loaded
  useEffect(() => {
    if (operatingModel && !isLoading && !isFetching) {
      form.setFieldsValue({
        methodology: operatingModel.methodology,
        sizingMethod: operatingModel.sizingMethod,
      })
    }
  }, [operatingModel, isLoading, isFetching, form])

  const isLoadingData = isLoading || isFetching

  return (
    <Modal
      title="Edit Operating Model"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid || isLoadingData }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Spin spinning={isLoadingData} description="Loading operating model...">
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="edit-team-operating-model-form"
        >
          <FormItem
            name="methodology"
            label="Methodology"
            rules={[{ required: true, message: 'Methodology is required' }]}
          >
            <RadioGroup
              options={methodologyOptions}
              optionType="button"
              buttonStyle="solid"
            />
          </FormItem>
          <FormItem
            name="sizingMethod"
            label="Sizing Method"
            rules={[{ required: true, message: 'Sizing method is required' }]}
          >
            <RadioGroup
              options={sizingMethodOptions}
              optionType="button"
              buttonStyle="solid"
            />
          </FormItem>
        </Form>
      </Spin>
    </Modal>
  )
}

export default EditTeamOperatingModelForm
