'use client'

import { Form, Modal, Spin } from 'antd'
import { useEffect } from 'react'
import { UpdateTeamOfTeamsOperatingModelRequest } from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import {
  useGetTeamOfTeamsOperatingModelQuery,
  useUpdateTeamOfTeamsOperatingModelMutation,
} from '@/src/store/features/organizations/team-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { TimeZoneSelect } from '@/src/components/common/scheduling'

const { Item: FormItem } = Form

export interface EditTeamOfTeamsOperatingModelFormProps {
  teamId: string
  operatingModelId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditTeamOfTeamsOperatingModelFormValues {
  timeZone: string
}

const EditTeamOfTeamsOperatingModelForm = ({
  teamId,
  operatingModelId,
  onFormComplete,
  onFormCancel,
}: EditTeamOfTeamsOperatingModelFormProps) => {
  const messageApi = useMessage()

  const {
    data: operatingModel,
    isLoading,
    isFetching,
  } = useGetTeamOfTeamsOperatingModelQuery({ teamId, operatingModelId })

  const [updateOperatingModel] = useUpdateTeamOfTeamsOperatingModelMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditTeamOfTeamsOperatingModelFormValues>({
      onSubmit: async (values, form) => {
        try {
          const request: UpdateTeamOfTeamsOperatingModelRequest = {
            timeZone: values.timeZone,
          }
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

  // A background refetch must not overwrite a zone the user has already picked.
  useEffect(() => {
    if (
      operatingModel &&
      !isLoading &&
      !isFetching &&
      !form.isFieldTouched('timeZone')
    ) {
      form.setFieldsValue({ timeZone: operatingModel.timeZone })
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
          name="edit-team-of-teams-operating-model-form"
        >
          <FormItem
            name="timeZone"
            label="Time Zone"
            extra="Corrects the zone for this model's whole period. If the team of teams moved, set a new operating model instead."
            rules={[{ required: true, message: 'Time zone is required' }]}
          >
            <TimeZoneSelect aria-label="Time Zone" />
          </FormItem>
        </Form>
      </Spin>
    </Modal>
  )
}

export default EditTeamOfTeamsOperatingModelForm
