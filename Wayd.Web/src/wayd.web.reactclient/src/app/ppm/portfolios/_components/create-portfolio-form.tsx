'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { CreatePortfolioRequest } from '@/src/services/wayd-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import { useCreatePortfolioMutation } from '@/src/store/features/ppm/portfolios-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Modal } from 'antd'
import TextArea from 'antd/es/input/TextArea'

const { Item } = Form

export interface CreatePortfolioFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreatePortfolioFormValues {
  name: string
  description: string
  sponsorIds: string[]
  ownerIds: string[]
  managerIds: string[]
}

const mapToRequestValues = (
  values: CreatePortfolioFormValues,
): CreatePortfolioRequest => {
  return {
    name: values.name,
    description: values.description,
    sponsorIds: values.sponsorIds,
    ownerIds: values.ownerIds,
    managerIds: values.managerIds,
  } as CreatePortfolioRequest
}

const CreatePortfolioForm = ({
  onFormComplete,
  onFormCancel,
}: CreatePortfolioFormProps) => {
  const messageApi = useMessage()

  const [createPortfolio] = useCreatePortfolioMutation()

  const { data: employeeData } = useGetEmployeeOptionsQuery(false)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreatePortfolioFormValues>({
      onSubmit: async (values: CreatePortfolioFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await createPortfolio(request)
          if (response.error) throw response.error

          messageApi.success(
            'Portfolio created successfully. Portfolio key: ' +
              response.data!.key,
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
              apiError.detail ??
                'An error occurred while creating the portfolio. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the portfolio. Please try again.',
      permission: 'Permissions.ProjectPortfolios.Create',
    })

  return (
    <Modal
      title="Create Portfolio"
      open={isOpen}
      width={'60vw'}
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
        name="create-portfolio-form"
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
            { max: 1024 },
          ]}
        >
          <MarkdownEditor
            value={form.getFieldValue('description')}
            onChange={(value) => form.setFieldValue('description', value || '')}
            maxLength={1024}
          />
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
        <Item name="managerIds" label="Portfolio Managers">
          <EmployeeSelect
            employees={employeeData ?? []}
            allowMultiple={true}
            placeholder="Select Portfolio Managers"
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreatePortfolioForm
