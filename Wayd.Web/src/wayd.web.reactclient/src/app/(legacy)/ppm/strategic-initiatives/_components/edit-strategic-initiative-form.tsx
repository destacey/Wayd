'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { UpdateStrategicInitiativeRequest } from '@/src/services/wayd-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import {
  useGetStrategicInitiativeQuery,
  useUpdateStrategicInitiativeMutation,
} from '@/src/store/features/ppm/strategic-initiatives-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Modal } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import dayjs from 'dayjs'
import { useEffect } from 'react'

const { Item } = Form
const { RangePicker } = DatePicker

export interface EditStrategicInitiativeFormProps {
  strategicInitiativeKey: number
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditStrategicInitiativeFormValues {
  name: string
  description: string
  dateRange: [dayjs.Dayjs, dayjs.Dayjs]
  sponsorIds: string[]
  ownerIds: string[]
}

const mapToRequestValues = (
  values: EditStrategicInitiativeFormValues,
  strategicInitiativeId: string,
): UpdateStrategicInitiativeRequest => {
  return {
    id: strategicInitiativeId,
    name: values.name,
    description: values.description,
    start: (values.dateRange?.[0] as any)?.format('YYYY-MM-DD'),
    end: (values.dateRange?.[1] as any)?.format('YYYY-MM-DD'),
    sponsorIds: values.sponsorIds,
    ownerIds: values.ownerIds,
  }
}

const EditStrategicInitiativeForm = ({
  strategicInitiativeKey,
  onFormComplete,
  onFormCancel,
}: EditStrategicInitiativeFormProps) => {
  const messageApi = useMessage()

  const [updateStrategicInitiative] = useUpdateStrategicInitiativeMutation()

  const {
    data: strategicInitiativeData,
    isLoading,
    error,
  } = useGetStrategicInitiativeQuery(strategicInitiativeKey)

  const { data: employeeData, error: employeeOptionsError } =
    useGetEmployeeOptionsQuery(false)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditStrategicInitiativeFormValues>({
      onSubmit: async (values: EditStrategicInitiativeFormValues, form) => {
        try {
          const request = mapToRequestValues(
            values,
            strategicInitiativeData!.id,
          )
          const response = await updateStrategicInitiative({
            request,
            cacheKey: strategicInitiativeData!.key,
          })
          if (response.error) throw response.error

          messageApi.success('Strategic initiative updated successfully.')
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
                'An error occurred while updating the strategic initiative. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the strategic initiative. Please try again.',
      permission: 'Permissions.StrategicInitiatives.Update',
    })

  // Initialize form values when data is loaded
  useEffect(() => {
    if (!strategicInitiativeData) return

    form.setFieldsValue({
      name: strategicInitiativeData.name,
      description: strategicInitiativeData.description,
      dateRange: [
        dayjs(strategicInitiativeData.start),
        dayjs(strategicInitiativeData.end),
      ],
      sponsorIds: strategicInitiativeData.strategicInitiativeSponsors.map(
        (s) => s.id,
      ),
      ownerIds: strategicInitiativeData.strategicInitiativeOwners.map(
        (o) => o.id,
      ),
    })
  }, [strategicInitiativeData, form])

  useEffect(() => {
    if (error || employeeOptionsError) {
      console.error(error || employeeOptionsError)
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ||
          (isApiError(employeeOptionsError) ? employeeOptionsError.detail : undefined) ||
          'An error occurred while loading form data.',
      )
    }
  }, [employeeOptionsError, error, messageApi])

  return (
    <Modal
      title="Edit Strategic Initiative"
      open={isOpen}
      width={'60vw'}
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
        name="edit-strategic-initiative-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 128 },
          ]}
        >
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
        </Item>
        <Item
          name="description"
          label="Description"
          rules={[
            { required: true, message: 'Description is required' },
            { max: 2048 },
          ]}
        >
          <MarkdownEditor maxLength={2048} />
        </Item>
        <Item
          name="dateRange"
          label="Planned Date Range"
          rules={[{ required: true, message: 'Date range is required' }]}
        >
          <RangePicker style={{ width: '60%' }} format="MMM D, YYYY" />
        </Item>
        <Item name="sponsorIds" label="Sponsors">
          <EmployeeSelect
            employees={employeeData ?? []}
            allowMultiple={true}
            placeholder="Select Sponsors"
          />
        </Item>
        <Item name="ownerIds" label="Owners">
          <EmployeeSelect
            employees={employeeData ?? []}
            allowMultiple={true}
            placeholder="Select Owners"
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditStrategicInitiativeForm
