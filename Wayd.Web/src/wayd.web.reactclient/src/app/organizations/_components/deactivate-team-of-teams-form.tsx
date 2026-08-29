'use client'

import {
  DeactivateTeamRequest as DeactivateTeamOfTeamsRequest,
  TeamOfTeamsDetailsDto,
} from '@/src/services/wayd-api'
import { useDeactivateTeamOfTeamsMutation } from '@/src/store/features/organizations/team-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Modal } from 'antd'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { type Dayjs } from 'dayjs'

const { Item } = Form

interface DeactivateTeamOfTeamsFormProps {
  team: TeamOfTeamsDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface DeactivateTeamFormValues {
  inactiveDate: Dayjs
}

const mapToRequestValues = (
  id: string,
  inactiveDate: Dayjs,
): DeactivateTeamOfTeamsRequest => {
  return {
    id,
    inactiveDate: inactiveDate?.format('YYYY-MM-DD'),
  } as unknown as DeactivateTeamOfTeamsRequest
}

const DeactivateTeamOfTeamsForm = ({
  team,
  onFormComplete,
  onFormCancel,
}: DeactivateTeamOfTeamsFormProps) => {
  const messageApi = useMessage()

  const [deactivateTeam] = useDeactivateTeamOfTeamsMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<DeactivateTeamFormValues>({
      onSubmit: async (values: DeactivateTeamFormValues, form) => {
        try {
          const request = mapToRequestValues(team.id, values.inactiveDate)
          const response = await deactivateTeam(request)
          if (response.error) throw response.error

          messageApi.success('Team deactivated successfully')
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
                'An error occurred while deactivating the team. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'An unexpected error occurred while deactivating the team.',
      permission: 'Permissions.Teams.Update',
    })

  return (
    <Modal
      title="Are you sure you want to deactivate this Team of Teams?"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Deactivate"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="deactivate-team-form"
      >
        <Item
          name="inactiveDate"
          label="Inactive Date"
          rules={[
            { required: true, message: 'Please select an inactive date' },
          ]}
        >
          <DatePicker />
        </Item>
      </Form>
    </Modal>
  )
}

export default DeactivateTeamOfTeamsForm
