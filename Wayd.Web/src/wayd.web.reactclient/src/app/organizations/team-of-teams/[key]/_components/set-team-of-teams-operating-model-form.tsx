'use client'

import { DatePicker, Form, Modal } from 'antd'
import { useEffect, useState } from 'react'
import { SetTeamOfTeamsOperatingModelRequest } from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import {
  useGetOperatingModelDefaultsQuery,
  useSetTeamOfTeamsOperatingModelMutation,
} from '@/src/store/features/organizations/team-api'
import { TimeZoneSelect } from '@/src/components/common/scheduling'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { type Dayjs } from 'dayjs'

const { Item: FormItem } = Form

export interface SetTeamOfTeamsOperatingModelFormProps {
  teamId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface SetTeamOfTeamsOperatingModelFormValues {
  startDate: Dayjs
  timeZone: string
}

const mapToRequestValues = (
  values: SetTeamOfTeamsOperatingModelFormValues,
): SetTeamOfTeamsOperatingModelRequest => {
  return {
    startDate: values.startDate?.format('YYYY-MM-DD'),
    timeZone: values.timeZone,
  } as SetTeamOfTeamsOperatingModelRequest
}

const SetTeamOfTeamsOperatingModelForm = ({
  teamId,
  onFormComplete,
  onFormCancel,
}: SetTeamOfTeamsOperatingModelFormProps) => {
  const messageApi = useMessage()

  const [setOperatingModel] = useSetTeamOfTeamsOperatingModelMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<SetTeamOfTeamsOperatingModelFormValues>({
      onSubmit: async (values, form) => {
        try {
          const request = mapToRequestValues(values)
          await setOperatingModel({ teamId, request }).unwrap()
          messageApi.success('Successfully set operating model.')
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
                'An unexpected error occurred while setting the operating model.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while setting the operating model.',
      permission: 'Permissions.Teams.Update',
    })

  // The suggested zone is the parent's on the start date, so it follows the date.
  const startDate = Form.useWatch('startDate', form)
  const { data: defaults } = useGetOperatingModelDefaultsQuery(
    {
      teamId,
      teamType: 'Team of Teams',
      startDate: startDate?.format('YYYY-MM-DD') ?? '',
    },
    { skip: !startDate },
  )

  // Pre-fill only what the user has not chosen themselves. The form's touched
  // flag cannot tell: setting a value programmatically marks it touched too,
  // which would stop the suggestion following later start dates.
  const [timeZoneChosen, setTimeZoneChosen] = useState(false)
  useEffect(() => {
    if (!defaults || timeZoneChosen) return
    form.setFieldsValue({ timeZone: defaults.timeZone })
  }, [defaults, form, timeZoneChosen])

  return (
    <Modal
      title="Set Operating Model"
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
        name="set-team-of-teams-operating-model-form"
        onValuesChange={(changed) => {
          if ('timeZone' in changed) setTimeZoneChosen(true)
        }}
      >
        <FormItem
          name="startDate"
          label="Start Date"
          rules={[{ required: true, message: 'Start date is required' }]}
        >
          <DatePicker style={{ width: '100%' }} />
        </FormItem>
        <FormItem
          name="timeZone"
          label="Time Zone"
          extra={
            <>
              The zone the team of teams&apos; own rollups count days in. Days
              before this model&apos;s start keep the previous model&apos;s
              zone, and child teams keep their own.
              {defaults?.timeZoneSource && (
                <> Suggested from {defaults.timeZoneSource}.</>
              )}
            </>
          }
          rules={[{ required: true, message: 'Time zone is required' }]}
        >
          <TimeZoneSelect aria-label="Time Zone" />
        </FormItem>
      </Form>
    </Modal>
  )
}

export default SetTeamOfTeamsOperatingModelForm
