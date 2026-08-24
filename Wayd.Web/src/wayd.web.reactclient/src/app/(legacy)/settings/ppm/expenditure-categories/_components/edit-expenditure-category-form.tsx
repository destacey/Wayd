'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { UpdateExpenditureCategoryRequest } from '@/src/services/wayd-api'
import {
  useGetExpenditureCategoryQuery,
  useUpdateExpenditureCategoryMutation,
} from '@/src/store/features/ppm/expenditure-categories-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal, Switch } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useEffect, useState } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface EditExpenditureCategoryFormProps {
  expenditureCategoryId: number
  onFormComplete: () => void
  onFormCancel: () => void
}

interface UpdateExpenditureCategoryFormValues {
  name: string
  description: string
  isCapitalizable: boolean
  requiresDepreciation: boolean
  accountingCode?: string
}

const mapToRequestValues = (
  values: UpdateExpenditureCategoryFormValues,
  expenditureCategoryId: number,
): UpdateExpenditureCategoryRequest => {
  return {
    id: expenditureCategoryId,
    name: values.name,
    description: values.description,
    isCapitalizable: values.isCapitalizable,
    requiresDepreciation: values.requiresDepreciation,
    accountingCode: values.accountingCode,
  } as UpdateExpenditureCategoryRequest
}

const EditExpenditureCategoryForm = ({
  expenditureCategoryId,
  onFormComplete,
  onFormCancel,
}: EditExpenditureCategoryFormProps) => {
  const messageApi = useMessage()
  const [hideReadOnlyFields, setHideReadOnlyFields] = useState(false)

  const { data: categoryData, error } = useGetExpenditureCategoryQuery(
    expenditureCategoryId,
  )

  const [updateExpenditureCategory] = useUpdateExpenditureCategoryMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<UpdateExpenditureCategoryFormValues>({
      onSubmit: async (values: UpdateExpenditureCategoryFormValues, form) => {
          try {
            const request = mapToRequestValues(values, categoryData!.id)
            const response = await updateExpenditureCategory(request)
            if (response.error) {
              throw response.error
            }
            messageApi.success('Expenditure Category updated successfully.')
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
                  'An error occurred while updating the expenditure category. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the expenditure category. Please try again.',
      permission: 'Permissions.ExpenditureCategories.Update',
    })

  useEffect(() => {
    if (!categoryData) return
    if (categoryData.state.name !== 'Proposed') {
      setHideReadOnlyFields(true)
    }
    form.setFieldsValue({
      name: categoryData.name,
      description: categoryData.description,
      isCapitalizable: categoryData.isCapitalizable,
      requiresDepreciation: categoryData.requiresDepreciation,
      accountingCode: categoryData.accountingCode,
    })
  }, [categoryData, form])

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading the expenditure category. Please try again.',
      )
    }
  }, [error, messageApi])

  return (
    <Modal
      title="Edit Expenditure Category"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="update-expenditure-category-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
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
          hidden={hideReadOnlyFields}
        >
          <Switch checkedChildren="Yes" unCheckedChildren="No" />
        </Item>
        <Item
          name="requiresDepreciation"
          label="Requires Depreciation?"
          valuePropName="checked"
          hidden={hideReadOnlyFields}
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

export default EditExpenditureCategoryForm
