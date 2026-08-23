'use client'

import { DatePicker, Descriptions, Form, Modal } from 'antd'
import { useEffect } from 'react'
import {
  TeamMembershipDto,
  UpdateTeamMembershipRequest,
} from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { useUpdateTeamMembershipMutation } from '@/src/store/features/organizations/team-api'
import { TeamTypeName } from '../types'
import dayjs, { type Dayjs } from 'dayjs'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'

const { Item } = Descriptions
const { Item: FormItem } = Form

export interface UpdateTeamMembershipFormProps {
  membership: TeamMembershipDto
  teamType: TeamTypeName
  onFormSave: () => void
  onFormCancel: () => void
}

interface UpdateTeamMembershipFormValues {
  start: Dayjs
  end: Dayjs | null
}

const mapToRequestValues = (
  values: UpdateTeamMembershipFormValues,
  originalMembership: TeamMembershipDto,
  teamType: TeamTypeName,
) => {
  // The generated client types start/end as Date, but the API expects
  // date-only strings; cast through unknown to bridge the NSwag mismatch.
  const membership = {
    teamMembershipId: originalMembership.id,
    teamId: originalMembership.child.id,
    start: values.start?.format('YYYY-MM-DD'),
    end: values.end?.format('YYYY-MM-DD'),
  } as unknown as UpdateTeamMembershipRequest
  return {
    membership,
    parentTeamId: originalMembership.parent.id,
    teamType,
  }
}

const EditTeamMembershipForm = ({
  membership,
  teamType,
  onFormSave,
  onFormCancel,
}: UpdateTeamMembershipFormProps) => {
  const messageApi = useMessage()

  const [updateTeamMembership] = useUpdateTeamMembershipMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<UpdateTeamMembershipFormValues>({
      onSubmit: async (values: UpdateTeamMembershipFormValues, form) => {
        try {
          const request = mapToRequestValues(values, membership, teamType)
          await updateTeamMembership(request).unwrap()
          messageApi.success('Successfully updated team membership.')
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
                'An unexpected error occurred while updating the team membership.',
            )
          }
          return false
        }
      },
      onComplete: onFormSave,
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while updating the team membership.',
      permission: 'Permissions.Teams.ManageTeamMemberships',
    })

  // Initialize form values when membership data is available
  useEffect(() => {
    if (!membership) return
    form.setFieldsValue({
      start: membership.start ? dayjs(membership.start) : undefined,
      end: membership.end ? dayjs(membership.end) : undefined,
    })
  }, [membership, form])

  return (
    <Modal
      title="Edit Team Membership"
      open={isOpen}
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
        name="edit-team-membership-form"
      >
        <Descriptions size="small" column={1}>
          <Item label="Team">{membership?.child.name}</Item>
          <Item label="Parent Team">{membership?.parent.name}</Item>
        </Descriptions>
        <FormItem label="Start" name="start" rules={[{ required: true }]}>
          <DatePicker />
        </FormItem>
        <FormItem
          label="End"
          name="end"
          dependencies={['start']}
          rules={[
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
        </FormItem>
      </Form>
    </Modal>
  )
}

export default EditTeamMembershipForm
