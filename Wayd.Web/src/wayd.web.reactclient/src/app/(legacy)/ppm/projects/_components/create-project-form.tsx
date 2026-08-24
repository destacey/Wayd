'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  CreateProjectRequest,
  ProjectLifecycleState,
} from '@/src/services/wayd-api'
import {
  useGetProjectLifecycleQuery,
  useGetProjectLifecyclesQuery,
} from '@/src/store/features/ppm/project-lifecycles-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import { useGetExpenditureCategoryOptionsQuery } from '@/src/store/features/ppm/expenditure-categories-api'
import {
  useGetPortfolioOptionsQuery,
  useGetPortfolioProgramOptionsQuery,
} from '@/src/store/features/ppm/portfolios-api'
import { useCreateProjectMutation } from '@/src/store/features/ppm/projects-api'
import { useGetStrategicThemeOptionsQuery } from '@/src/store/features/strategic-management/strategic-themes-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { projectHelpText } from './project-help-text'
import {
  Card,
  DatePicker,
  Form,
  Input,
  Modal,
  Select,
  Timeline,
  Typography,
} from 'antd'
import TextArea from 'antd/es/input/TextArea'
import dayjs from 'dayjs'
import { useEffect } from 'react'

const { Item } = Form
const { RangePicker } = DatePicker
const { Text } = Typography

export interface CreateProjectFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateProjectFormValues {
  portfolioId: string
  programId?: string
  lifecycleId?: string
  key: string
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
  values: CreateProjectFormValues,
): CreateProjectRequest => {
  return {
    name: values.name,
    description: values.description,
    businessCase: values.businessCase || undefined,
    expectedBenefits: values.expectedBenefits || undefined,
    key: values.key,
    expenditureCategoryId: values.expenditureCategoryId,
    start: (values.dateRange?.[0] as any)?.format('YYYY-MM-DD'),
    end: (values.dateRange?.[1] as any)?.format('YYYY-MM-DD'),
    portfolioId: values.portfolioId,
    programId: values.programId,
    projectLifecycleId: values.lifecycleId,
    sponsorIds: values.sponsorIds,
    ownerIds: values.ownerIds,
    managerIds: values.managerIds,
    memberIds: values.memberIds,
    strategicThemeIds: values.strategicThemeIds,
  } as CreateProjectRequest
}

const keyPattern = /^[A-Z0-9]{2,20}$/

const CreateProjectForm = ({
  onFormComplete,
  onFormCancel,
}: CreateProjectFormProps) => {
  const messageApi = useMessage()

  const [createProject] = useCreateProjectMutation()

  const { data: lifecycleData, error: lifecycleOptionsError } =
    useGetProjectLifecyclesQuery(ProjectLifecycleState.Active)

  const lifecycleOptions = !lifecycleData ? [] : [...lifecycleData]
      .sort((a, b) => a.name.localeCompare(b.name))
      .map((lc) => ({
        label: lc.name,
        value: lc.id,
      }))

  const { data: expenditureData, error: expenditureOptionsError } =
    useGetExpenditureCategoryOptionsQuery(false)

  const { data: portfolioData, error: portfolioOptionsError } =
    useGetPortfolioOptionsQuery()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateProjectFormValues>({
      onSubmit: async (values: CreateProjectFormValues, form) => {
          try {
            const request = mapToRequestValues(values)
            const response = await createProject(request)
            if (response.error) throw response.error

            messageApi.success(
              'Project created successfully. Project key: ' + response.data!.key,
            )
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
                  'An error occurred while creating the project. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the project. Please try again.',
      permission: 'Permissions.Projects.Create',
    })

  const { data: programData, error: programOptionsError } =
    useGetPortfolioProgramOptionsQuery(form.getFieldValue('portfolioId'), {
      skip: !form.getFieldValue('portfolioId'),
    })

  const { data: employeeData, error: employeeOptionsError } =
    useGetEmployeeOptionsQuery(false)

  const { data: strategicThemeData, error: strategicThemeOptionsError } =
    useGetStrategicThemeOptionsQuery(false)

  useEffect(() => {
    const firstError =
      expenditureOptionsError ??
      portfolioOptionsError ??
      programOptionsError ??
      employeeOptionsError ??
      strategicThemeOptionsError ??
      lifecycleOptionsError
    if (firstError) {
      console.error(firstError)
      messageApi.error(
        (isApiError(firstError) ? firstError.detail : undefined) ??
          'An error occurred while loading form data. Please try again.',
      )
    }
  }, [
    employeeOptionsError,
    expenditureOptionsError,
    lifecycleOptionsError,
    portfolioOptionsError,
    programOptionsError,
    messageApi,
    strategicThemeOptionsError,
  ])

  const selectedLifecycleId = Form.useWatch('lifecycleId', form)

  const { data: selectedLifecycle } = useGetProjectLifecycleQuery(
    selectedLifecycleId!,
    { skip: !selectedLifecycleId },
  )

  const stageItems = !selectedLifecycle?.stages ? [] : [...selectedLifecycle.stages]
      .sort((a, b) => a.order - b.order)
      .map((stage) => ({
        content: (
          <>
            <Text strong>{stage.name}</Text>
            <br />
            <Text type="secondary">{stage.description}</Text>
          </>
        ),
      }))

  return (
    <Modal
      title="Create Project"
      open={isOpen}
      width="min(90vw, 900px)"
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
        name="create-project-form"
        style={{ paddingTop: 16, paddingBottom: 16 }}
      >
        <Item
          name="portfolioId"
          label="Portfolio"
          rules={[{ required: true, message: 'Portfolio is required' }]}
        >
          <Select
            allowClear
            options={portfolioData ?? []}
            placeholder="Select Portfolio"
          />
        </Item>
        <Item name="programId" label="Program">
          <Select
            allowClear
            options={programData ?? []}
            placeholder="Select Program"
            disabled={!form.getFieldValue('portfolioId')}
          />
        </Item>
        <Item
          name="key"
          label="Key"
          extra="2-20 uppercase alphanumeric characters (A-Z, 0-9)"
          rules={[
            { required: true, message: 'Key is required.' },
            {
              pattern: keyPattern,
              message:
                'Key must be 2-20 uppercase alphanumeric characters (A-Z, 0-9).',
            },
          ]}
          normalize={(value) => (value ?? '').toUpperCase()}
        >
          <Input
            placeholder="Enter new key"
            autoComplete="off"
            showCount
            maxLength={20}
          />
        </Item>
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
            optionFilterProp="children"
            filterOption={(input, option) =>
              (option?.label?.toLowerCase() ?? '').includes(input.toLowerCase())
            }
          />
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
        <Item name="lifecycleId" label="Lifecycle">
          <Select
            allowClear
            options={lifecycleOptions}
            placeholder="Select Lifecycle"
          />
        </Item>
      </Form>
      {stageItems.length > 0 && (
        <Card size="small" title="Stages">
          <Timeline items={stageItems} />
        </Card>
      )}
    </Modal>
  )
}

export default CreateProjectForm
