'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { KpiCategoryDto } from '@/src/services/api/wayd.schemas'
import {
  useCreateKpiCategoryMutation,
  useUpdateKpiCategoryMutation,
} from '@/src/store/features/common/kpis-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useEffect } from 'react'

const { Item } = Form
const { TextArea } = Input

export interface KpiCategoryFormProps {
  /** The category to edit, or none to add one to the end of the list. */
  category?: KpiCategoryDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface KpiCategoryFormValues {
  name: string
  description?: string
}

/** Adds a KPI category, or renames and re-describes one. */
const KpiCategoryForm = ({
  category,
  onFormComplete,
  onFormCancel,
}: KpiCategoryFormProps) => {
  const messageApi = useMessage()
  const isEdit = category !== undefined
  const action = isEdit ? 'updating' : 'adding'

  const [createCategory] = useCreateKpiCategoryMutation()
  const [updateCategory] = useUpdateKpiCategoryMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<KpiCategoryFormValues>({
      onSubmit: async (values, form) => {
        const request = {
          name: values.name.trim(),
          description: values.description?.trim() || undefined,
        }
        try {
          if (category) {
            await updateCategory({ id: category.id, request }).unwrap()
            messageApi.success('Category updated.')
          } else {
            await createCategory(request).unwrap()
            messageApi.success('Category added.')
          }
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                `An error occurred while ${action} the category. Please try again.`,
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: `An error occurred while ${action} the category. Please try again.`,
      permission: 'Permissions.Kpis.Administer',
    })

  useEffect(() => {
    if (category) {
      form.setFieldsValue({
        name: category.name,
        description: category.description ?? undefined,
      })
    }
  }, [category, form])

  return (
    <Modal
      title={isEdit ? 'Edit KPI Category' : 'Add KPI Category'}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText={isEdit ? 'Save' : 'Add'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="kpi-category-form">
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
          extra={
            isEdit
              ? 'Every scorecard grouping KPIs by this category shows the new name.'
              : undefined
          }
        >
          <Input showCount maxLength={64} />
        </Item>
        <Item label="Description" name="description" rules={[{ max: 512 }]}>
          <TextArea
            autoSize={{ minRows: 2, maxRows: 6 }}
            showCount
            maxLength={512}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default KpiCategoryForm
