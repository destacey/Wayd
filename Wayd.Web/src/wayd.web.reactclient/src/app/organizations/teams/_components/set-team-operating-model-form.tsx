'use client'

import { DatePicker, Form, Modal, Radio } from 'antd'
import {
  Methodology,
  SetTeamOperatingModelRequest,
  SizingMethod,
} from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { useSetTeamOperatingModelMutation } from '@/src/store/features/organizations/team-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { type Dayjs } from 'dayjs'

const { Item: FormItem } = Form
const { Group: RadioGroup } = Radio

export interface SetTeamOperatingModelFormProps {
  teamId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface SetTeamOperatingModelFormValues {
  startDate: Dayjs
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
  values: SetTeamOperatingModelFormValues,
): SetTeamOperatingModelRequest => {
  return {
    startDate: values.startDate?.format('YYYY-MM-DD'),
    methodology: values.methodology,
    sizingMethod: values.sizingMethod,
  } as unknown as SetTeamOperatingModelRequest
}

const SetTeamOperatingModelForm = ({
  teamId,
  onFormComplete,
  onFormCancel,
}: SetTeamOperatingModelFormProps) => {
  const messageApi = useMessage()

  const [setOperatingModel] = useSetTeamOperatingModelMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<SetTeamOperatingModelFormValues>({
      onSubmit: async (values: SetTeamOperatingModelFormValues, form) => {
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
        name="set-team-operating-model-form"
      >
        <FormItem
          name="startDate"
          label="Start Date"
          rules={[{ required: true, message: 'Start date is required' }]}
        >
          <DatePicker style={{ width: '100%' }} />
        </FormItem>
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
    </Modal>
  )
}

export default SetTeamOperatingModelForm
