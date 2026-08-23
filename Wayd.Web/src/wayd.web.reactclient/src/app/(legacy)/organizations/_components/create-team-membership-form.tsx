'use client'

import { DatePicker, Form, Modal, Select } from 'antd'
import { AddTeamMembershipRequest } from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import {
  useCreateTeamMembershipMutation,
  useGetTeamOfTeamsOptionsQuery,
} from '@/src/store/features/organizations/team-api'
import { TeamTypeName } from '../types'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { type Dayjs } from 'dayjs'

const { Item: FormItem } = Form

export interface CreateTeamMembershipFormProps {
  teamId: string
  teamType: TeamTypeName
  onFormCreate: () => void
  onFormCancel: () => void
}

interface CreateTeamMembershipFormValues {
  parentTeamId: string
  start: Dayjs
  end: Dayjs | null
}

const mapToRequestValues = (
  values: CreateTeamMembershipFormValues,
  teamType: TeamTypeName,
  teamId: string,
) => {
  const membership = {
    teamId: teamId,
    parentTeamId: values.parentTeamId,
    start: values.start?.format('YYYY-MM-DD'),
    end: values.end?.format('YYYY-MM-DD'),
  } as unknown as AddTeamMembershipRequest
  return { membership, teamType }
}

const CreateTeamMembershipForm = ({
  teamId,
  teamType,
  onFormCreate,
  onFormCancel,
}: CreateTeamMembershipFormProps) => {
  const messageApi = useMessage()
  // TODO: only get teams that are not in the hierarchy
  const { data: teamOptions } = useGetTeamOfTeamsOptionsQuery(false)

  const [createTeamMembership] = useCreateTeamMembershipMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateTeamMembershipFormValues>({
      onSubmit: async (values: CreateTeamMembershipFormValues, form) => {
        try {
          const request = mapToRequestValues(values, teamType, teamId)
          await createTeamMembership(request).unwrap()
          messageApi.success('Successfully created team membership.')
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
                'An unexpected error occurred while creating the team membership.',
            )
          }
          return false
        }
      },
      onComplete: onFormCreate,
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while creating the team membership.',
      permission: 'Permissions.Teams.ManageTeamMemberships',
    })

  return (
    <Modal
      title="Create Team Membership"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-team-membership-form"
      >
        <FormItem
          name="parentTeamId"
          label="Parent Team"
          rules={[{ required: true }]}
        >
          <Select
            showSearch
            placeholder="Select a parent team"
            optionFilterProp="children"
            filterOption={(input, option) =>
              (option?.label.toLowerCase() ?? '').includes(input.toLowerCase())
            }
            filterSort={(optionA, optionB) =>
              (optionA?.label ?? '')
                .toLowerCase()
                .localeCompare((optionB?.label ?? '').toLowerCase())
            }
            options={teamOptions?.filter((t) => t.value !== teamId)}
          />
        </FormItem>
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

export default CreateTeamMembershipForm
