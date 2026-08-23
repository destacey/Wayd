'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { CreateEstimationScaleRequest } from '@/src/services/wayd-api'
import { useCreateEstimationScaleMutation } from '@/src/store/features/planning/estimation-scales-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { MinusCircleOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Form, Input, Modal, Space } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useModalForm } from '@/src/hooks'

const { Item, List } = Form

export interface CreateEstimationScaleFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateEstimationScaleFormValues {
  name: string
  description?: string
  values: string[]
}

const CreateEstimationScaleForm = ({
  onFormComplete,
  onFormCancel,
}: CreateEstimationScaleFormProps) => {
  const messageApi = useMessage()

  const [createEstimationScale] = useCreateEstimationScaleMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateEstimationScaleFormValues>({
      onSubmit: async (values: CreateEstimationScaleFormValues, form) => {
          try {
            const request: CreateEstimationScaleRequest = {
              name: values.name,
              description: values.description,
              values: values.values,
            }
            const response = await createEstimationScale(request)
            if (response.error) {
              throw response.error
            }
            messageApi.success('Estimation scale created successfully.')
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
                  'An error occurred while creating the estimation scale. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the estimation scale. Please try again.',
      permission: 'Permissions.EstimationScales.Create',
    })

  return (
    <Modal
      title="Create Estimation Scale"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
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
        name="create-estimation-scale-form"
        initialValues={{ values: ['', ''] }}
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
          rules={[{ max: 1024 }]}
        >
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
                  <Form.ErrorList errors={errors} />
                </Item>
              </>
            )}
          </List>
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateEstimationScaleForm
