'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { UpdateEstimationScaleRequest } from '@/src/services/wayd-api'
import {
  useGetEstimationScaleQuery,
  useUpdateEstimationScaleMutation,
} from '@/src/store/features/planning/estimation-scales-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Form, Input, Modal, Space } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item, List, ErrorList } = Form

export interface EditEstimationScaleFormProps {
  estimationScaleId: number
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditEstimationScaleFormValues {
  name: string
  description?: string
  values: string[]
}

const EditEstimationScaleForm = ({
  estimationScaleId,
  onFormComplete,
  onFormCancel,
}: EditEstimationScaleFormProps) => {
  const messageApi = useMessage()

  const { data: scaleData, isLoading: scaleLoading } =
    useGetEstimationScaleQuery(estimationScaleId)
  const [updateEstimationScale] = useUpdateEstimationScaleMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditEstimationScaleFormValues>({
      onSubmit: async (values: EditEstimationScaleFormValues, form) => {
          try {
            const request: UpdateEstimationScaleRequest = {
              estimationScaleId,
              name: values.name,
              description: values.description,
              values: values.values,
            }
            const response = await updateEstimationScale(request)
            if (response.error) {
              throw response.error
            }
            messageApi.success('Estimation scale updated successfully.')
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
                  'An error occurred while updating the estimation scale. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the estimation scale. Please try again.',
      permission: 'Permissions.EstimationScales.Update',
    })

  useEffect(() => {
    if (!scaleData) return
    form.setFieldsValue({
      name: scaleData.name,
      description: scaleData.description,
      values: [...scaleData.values],
    })
  }, [scaleData, form])

  return (
    <Modal
      title="Edit Estimation Scale"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      loading={scaleLoading}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={600}
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-estimation-scale-form"
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
        <Item name="description" label="Description" rules={[{ max: 1024 }]}>
          <TextArea
            autoSize={{ minRows: 3, maxRows: 6 }}
            showCount
            maxLength={1024}
          />
        </Item>
        <Item label="Scale Values">
          <List
            name="values"
            rules={[
              {
                validator: async (_, values) => {
                  if (!values || values.length < 2) {
                    return Promise.reject(
                      new Error('At least 2 scale values are required'),
                    )
                  }
                },
              },
            ]}
          >
            {(fields, { add, remove }, { errors }) => (
              <>
                {fields.map((field) => (
                  <Space
                    key={field.key}
                    style={{ display: 'flex', marginBottom: 4 }}
                    align="baseline"
                  >
                    <Item
                      {...field}
                      rules={[
                        { required: true, message: 'Value is required' },
                        { max: 32 },
                      ]}
                      style={{ marginBottom: 0 }}
                    >
                      <Input
                        placeholder={`Value ${field.name + 1}`}
                        maxLength={32}
                      />
                    </Item>
                    {fields.length > 2 && (
                      <MinusCircleOutlined onClick={() => remove(field.name)} />
                    )}
                  </Space>
                ))}
                <Item>
                  <Button
                    type="dashed"
                    onClick={() => add('')}
                    block
                    icon={<PlusOutlined />}
                  >
                    Add Value
                  </Button>
                  <ErrorList errors={errors} />
                </Item>
              </>
            )}
          </List>
        </Item>
      </Form>
    </Modal>
  )
}

export default EditEstimationScaleForm
