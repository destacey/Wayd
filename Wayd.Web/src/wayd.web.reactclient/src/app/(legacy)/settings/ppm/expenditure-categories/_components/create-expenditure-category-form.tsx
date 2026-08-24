'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { CreateExpenditureCategoryRequest } from '@/src/services/wayd-api'
import { useCreateExpenditureCategoryMutation } from '@/src/store/features/ppm/expenditure-categories-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal, Switch } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface CreateExpenditureCategoryFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateExpenditureCategoryFormValues {
  name: string
  description: string
  isCapitalizable: boolean
  requiresDepreciation: boolean
  accountingCode?: string
}

const mapToRequestValues = (
  values: CreateExpenditureCategoryFormValues,
): CreateExpenditureCategoryRequest => {
  return {
    name: values.name,
    description: values.description,
    isCapitalizable: values.isCapitalizable,
    requiresDepreciation: values.requiresDepreciation,
    accountingCode: values.accountingCode,
  } as CreateExpenditureCategoryRequest
}

const CreateExpenditureCategoryForm = ({
  onFormComplete,
  onFormCancel,
}: CreateExpenditureCategoryFormProps) => {
  const messageApi = useMessage()

  const [createExpenditureCategory] =
    useCreateExpenditureCategoryMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateExpenditureCategoryFormValues>({
      onSubmit: async (values: CreateExpenditureCategoryFormValues, form) => {
          try {
            const request = mapToRequestValues(values)
            const response = await createExpenditureCategory(request)
            if (response.error) {
              throw response.error
            }
            messageApi.success(
              'Expenditure Category created successfully. Expenditure Category key: ' +
                response.data,
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
                  'An error occurred while creating the expenditure category. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the expenditure category. Please try again.',
      permission: 'Permissions.ExpenditureCategories.Create',
    })

  return (
    <Modal
      title="Create Expenditure Category"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-expenditure-category-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 64 },
          ]}
        >
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={64}
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
          <TextArea
            autoSize={{ minRows: 6, maxRows: 8 }}
            showCount
            maxLength={1024}
          />
        </Item>
        <Item
          name="isCapitalizable"
          label="Capitalizable?"
          valuePropName="checked"
          initialValue={false}
        >
          <Switch checkedChildren="Yes" unCheckedChildren="No" />
        </Item>
        <Item
          name="requiresDepreciation"
          label="Requires Depreciation?"
          valuePropName="checked"
          initialValue={false}
        >
          <Switch checkedChildren="Yes" unCheckedChildren="No" />
        </Item>
        <Item
          name="accountingCode"
          label="Accounting Code"
          rules={[{ max: 64 }]}
        >
          <Input showCount maxLength={64} />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateExpenditureCategoryForm
