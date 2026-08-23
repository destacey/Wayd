'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { UpdateProjectRequest } from '@/src/services/wayd-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import { useGetExpenditureCategoryOptionsQuery } from '@/src/store/features/ppm/expenditure-categories-api'
import {
  useGetProjectQuery,
  useUpdateProjectMutation,
} from '@/src/store/features/ppm/projects-api'
import { useGetStrategicThemeOptionsQuery } from '@/src/store/features/strategic-management/strategic-themes-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { projectHelpText } from './project-help-text'
import { DatePicker, Form, Modal, Select } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import dayjs from 'dayjs'
import { useEffect } from 'react'

const { Item } = Form
const { RangePicker } = DatePicker

export interface EditProjectFormProps {
  projectKey: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditProjectFormValues {
  name: string
  description: string
  businessCase?: string
  expectedBenefits?: string
  expenditureCategoryId: number
  dateRange?: [dayjs.Dayjs, dayjs.Dayjs] | null
  sponsorIds: string[]
  ownerIds: string[]
  managerIds: string[]
  memberIds: string[]
  strategicThemeIds: string[]
}

const mapToRequestValues = (
  values: EditProjectFormValues,
  projectId: string,
): UpdateProjectRequest => {
  return {
    id: projectId,
    name: values.name,
    description: values.description,
    businessCase: values.businessCase || undefined,
    expectedBenefits: values.expectedBenefits || undefined,
    expenditureCategoryId: values.expenditureCategoryId,
    start: (values.dateRange?.[0] as any)?.format('YYYY-MM-DD'),
    end: (values.dateRange?.[1] as any)?.format('YYYY-MM-DD'),
    sponsorIds: values.sponsorIds,
    ownerIds: values.ownerIds,
    managerIds: values.managerIds,
    memberIds: values.memberIds,
    strategicThemeIds: values.strategicThemeIds,
  } as UpdateProjectRequest
}

const EditProjectForm = ({
  projectKey,
  onFormComplete,
  onFormCancel,
}: EditProjectFormProps) => {
  const messageApi = useMessage()

  const [updateProject] = useUpdateProjectMutation()

  const { data: projectData, isLoading, error } = useGetProjectQuery(projectKey)

  const { data: expenditureData, error: expenditureOptionsError } =
    useGetExpenditureCategoryOptionsQuery(false)

  const { data: employeeData, error: employeeOptionsError } =
    useGetEmployeeOptionsQuery(true)

  const { data: strategicThemeData, error: strategicThemeOptionsError } =
    useGetStrategicThemeOptionsQuery(false)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditProjectFormValues>({
      onSubmit: async (values: EditProjectFormValues, form) => {
          try {
            const request = mapToRequestValues(values, projectData!.id)
            const response = await updateProject({
              request,
              cacheKey: projectData!.key,
            })
            if (response.error) throw response.error

            messageApi.success('Project updated successfully.')
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
                  'An error occurred while updating the project. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the project. Please try again.',
      permission: 'Permissions.Projects.Update',
    })

  // Initialize form values when data is loaded
  useEffect(() => {
    if (!projectData) return

    form.setFieldsValue({
      name: projectData.name,
      description: projectData.description,
      businessCase: projectData.businessCase,
      expectedBenefits: projectData.expectedBenefits,
      expenditureCategoryId: projectData.expenditureCategory.id,
      dateRange:
        projectData.start && projectData.end
          ? [dayjs(projectData.start), dayjs(projectData.end)]
          : undefined,
      sponsorIds: projectData.projectSponsors.map((s) => s.id),
      ownerIds: projectData.projectOwners.map((o) => o.id),
      managerIds: projectData.projectManagers.map((m) => m.id),
      memberIds: projectData.projectMembers.map((m) => m.id),
      strategicThemeIds: projectData.strategicThemes.map((t) => t.id),
    })
  }, [projectData, form])

  useEffect(() => {
    if (
      error ||
      expenditureOptionsError ||
      employeeOptionsError ||
      strategicThemeOptionsError
    ) {
      console.error(
        expenditureOptionsError ??
          error ??
          employeeOptionsError ??
          strategicThemeOptionsError,
      )
      messageApi.error(
        (isApiError(expenditureOptionsError) ? expenditureOptionsError.detail : undefined) ??
          (isApiError(error) ? error.detail : undefined) ??
          (isApiError(employeeOptionsError) ? employeeOptionsError.detail : undefined) ??
          (isApiError(strategicThemeOptionsError) ? strategicThemeOptionsError.detail : undefined) ??
          'An error occurred while loading form data. Please try again.',
      )
    }
  }, [
    employeeOptionsError,
    error,
    expenditureOptionsError,
    messageApi,
    strategicThemeOptionsError,
  ])

  return (
    <Modal
      title="Edit Project"
      open={isOpen}
      width="min(90vw, 900px)"
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
        name="edit-project-form"
        style={{ paddingTop: 16, paddingBottom: 16 }}
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
          extra={projectHelpText.description}
          rules={[
            { required: true, message: 'Description is required' },
            { max: 4096 },
          ]}
        >
          <MarkdownEditor maxLength={4096} />
        </Item>
        <Item
          name="businessCase"
          label="Business Case"
          extra={projectHelpText.businessCase}
          rules={[{ max: 4096 }]}
        >
          <MarkdownEditor maxLength={4096} />
        </Item>
        <Item
          name="expectedBenefits"
          label="Expected Benefits"
          extra={projectHelpText.expectedBenefits}
          rules={[{ max: 4096 }]}
        >
          <MarkdownEditor maxLength={4096} />
        </Item>
        <Item
          name="expenditureCategoryId"
          label="Expenditure Category"
          rules={[
            { required: true, message: 'Expenditure Category is required' },
          ]}
        >
          <Select
            allowClear
            options={expenditureData ?? []}
            placeholder="Select Expenditure Category"
          />
        </Item>
        <Item
          name="dateRange"
          label="Planned Date Range"
          rules={[
            {
              validator: (_, value) => {
                const status = projectData?.status.name
                if (
                  (status === 'Active' || status === 'Completed') &&
                  (!value || !value[0] || !value[1])
                ) {
                  return Promise.reject(
                    new Error(
                      'Dates are required for active or completed projects',
                    ),
                  )
                }
                return Promise.resolve()
              },
            },
          ]}
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
        <Item name="managerIds" label="Project Managers">
          <EmployeeSelect
            employees={employeeData ?? []}
            allowMultiple={true}
            placeholder="Select Project Managers"
          />
        </Item>
        <Item name="memberIds" label="Members">
          <EmployeeSelect
            employees={employeeData ?? []}
            allowMultiple={true}
            placeholder="Select Members"
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

export default EditProjectForm
