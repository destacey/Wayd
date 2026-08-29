'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { UpdateProgramRequest } from '@/src/services/wayd-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import {
  useGetProgramQuery,
  useUpdateProgramMutation,
} from '@/src/store/features/ppm/programs-api'
import { useGetStrategicThemeOptionsQuery } from '@/src/store/features/strategic-management/strategic-themes-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Modal, Select } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useEffect } from 'react'
import dayjs from 'dayjs'

const { Item } = Form
const { RangePicker } = DatePicker

export interface EditProgramFormProps {
  programKey: number
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditProgramFormValues {
  name: string
  description: string
  dateRange?: [dayjs.Dayjs, dayjs.Dayjs] | null
  sponsorIds: string[]
  ownerIds: string[]
  managerIds: string[]
  strategicThemeIds: string[]
}

const mapToRequestValues = (
  values: EditProgramFormValues,
  programId: string,
): UpdateProgramRequest => {
  return {
    id: programId,
    name: values.name,
    description: values.description,
    start: (values.dateRange?.[0] as any)?.format('YYYY-MM-DD'),
    end: (values.dateRange?.[1] as any)?.format('YYYY-MM-DD'),
    sponsorIds: values.sponsorIds,
    ownerIds: values.ownerIds,
    managerIds: values.managerIds,
    strategicThemeIds: values.strategicThemeIds,
  } as UpdateProgramRequest
}

const EditProgramForm = ({
  programKey,
  onFormComplete,
  onFormCancel,
}: EditProgramFormProps) => {
  const messageApi = useMessage()

  const [updateProgram] = useUpdateProgramMutation()

  const { data: programData, isLoading, error } = useGetProgramQuery(programKey)

  const { data: employeeData } = useGetEmployeeOptionsQuery(true)

  const { data: strategicThemeData } = useGetStrategicThemeOptionsQuery(false)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditProgramFormValues>({
      onSubmit: async (values: EditProgramFormValues, form) => {
        try {
          const request = mapToRequestValues(values, programData!.id)
          const response = await updateProgram({
            request,
            cacheKey: programData!.key,
          })
          if (response.error) throw response.error

          messageApi.success('Program updated successfully.')
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
                'An error occurred while updating the program. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the program. Please try again.',
      permission: 'Permissions.Programs.Update',
    })

  // Initialize form values when data is loaded
  useEffect(() => {
    if (!programData) return

    form.setFieldsValue({
      name: programData.name,
      description: programData.description,
      dateRange:
        programData.start && programData.end
          ? [dayjs(programData.start), dayjs(programData.end)]
          : undefined,
      sponsorIds: programData.programSponsors.map((s) => s.id),
      ownerIds: programData.programOwners.map((o) => o.id),
      managerIds: programData.programManagers.map((m) => m.id),
      strategicThemeIds: programData.strategicThemes.map((t) => t.id),
    })
  }, [programData, form])

  return (
    <Modal
      title="Edit Program"
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
      <Form form={form} size="small" layout="vertical" name="edit-program-form">
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
        <Item name="dateRange" label="Planned Date Range">
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
        <Item name="managerIds" label="Program Managers">
          <EmployeeSelect
            employees={employeeData ?? []}
            allowMultiple={true}
            placeholder="Select Program Managers"
          />
        </Item>
        <Item name="strategicThemeIds" label="Strategic Themes">
          <Select
            mode="multiple"
            allowClear
            options={strategicThemeData ?? []}
            placeholder="Select Strategic Themes"
            optionFilterProp="label"
            filterOption={(input, option) =>
              (option?.label?.toLowerCase() ?? '').includes(input.toLowerCase())
            }
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditProgramForm
