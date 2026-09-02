'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  CreateDeploymentEnvironmentRequest,
  DeploymentEnvironmentDto,
  EnvironmentCategory,
  UpdateDeploymentEnvironmentRequest,
} from '@/src/services/wayd-api'
import {
  useCreateDeploymentEnvironmentMutation,
  useUpdateDeploymentEnvironmentMutation,
} from '@/src/store/features/product-management/deployment-environments-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, Form, Input, InputNumber, Modal, Select } from 'antd'

const { Item } = Form

export interface DeploymentEnvironmentFormProps {
  /** The environment being edited, or undefined to create one. */
  environment?: DeploymentEnvironmentDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface DeploymentEnvironmentFormValues {
  name: string
  category: EnvironmentCategory
  ringOrder: number
}

const categoryOptions = [
  {
    value: EnvironmentCategory.Development,
    label: 'Development',
  },
  { value: EnvironmentCategory.Testing, label: 'Testing' },
  { value: EnvironmentCategory.Staging, label: 'Staging' },
  { value: EnvironmentCategory.Production, label: 'Production' },
]

/**
 * Creates or edits a deployment environment.
 *
 * Creating and editing share a form because they take the same three fields — the category is not a
 * separate reclassify endpoint, it is part of the update.
 *
 * Changing the category is called out when editing, because it is not an ordinary rename: every
 * delivery measure scoped to production counts on the category, so moving an environment in or out of
 * Production changes what future deployments there count toward. Deployments already recorded keep the
 * category they froze at the time.
 */
const DeploymentEnvironmentForm = ({
  environment,
  onFormComplete,
  onFormCancel,
}: DeploymentEnvironmentFormProps) => {
  const messageApi = useMessage()

  const isEdit = !!environment

  const [createEnvironment] = useCreateDeploymentEnvironmentMutation()
  const [updateEnvironment] = useUpdateDeploymentEnvironmentMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<DeploymentEnvironmentFormValues>({
      onSubmit: async (values: DeploymentEnvironmentFormValues, form) => {
        try {
          const response = isEdit
            ? await updateEnvironment({
                id: environment.id,
                request: {
                  id: environment.id,
                  name: values.name,
                  category: values.category,
                  ringOrder: values.ringOrder,
                } as UpdateDeploymentEnvironmentRequest,
              })
            : await createEnvironment({
                name: values.name,
                category: values.category,
                ringOrder: values.ringOrder,
              } as CreateDeploymentEnvironmentRequest)

          if (response.error) throw response.error

          messageApi.success(
            isEdit
              ? 'Environment updated successfully.'
              : 'Environment created successfully.',
          )
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                `An error occurred while ${isEdit ? 'updating' : 'creating'} the environment. Please try again.`,
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: `An error occurred while ${isEdit ? 'updating' : 'creating'} the environment. Please try again.`,
      permission: isEdit
        ? 'Permissions.DeploymentEnvironments.Update'
        : 'Permissions.DeploymentEnvironments.Create',
    })

  const category = Form.useWatch('category', form)
  const categoryChanged = isEdit && !!category && category !== environment.category

  return (
    <Modal
      title={isEdit ? 'Edit Environment' : 'Create Environment'}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText={isEdit ? 'Save' : 'Create'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      {categoryChanged && (
        <Alert
          type="warning"
          showIcon
          title="This changes what deployments here count toward"
          description="Delivery measures scoped to production count on the category, not the name. Deployments already recorded keep the category they had at the time."
          style={{ marginBottom: 16 }}
        />
      )}
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="deployment-environment-form"
        initialValues={
          environment
            ? {
                name: environment.name,
                category: environment.category,
                ringOrder: environment.ringOrder,
              }
            : { category: EnvironmentCategory.Development, ringOrder: 1 }
        }
      >
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 128, message: 'Name cannot be longer than 128 characters' },
          ]}
          extra='What your organization calls it — "Production", "prod-eu", "QA2".'
        >
          <Input />
        </Item>
        <Item
          label="Category"
          name="category"
          rules={[{ required: true, message: 'Category is required' }]}
          extra="Every delivery measure scoped to production counts on this rather than on the name."
        >
          <Select options={categoryOptions} />
        </Item>
        <Item
          label="Ring Order"
          name="ringOrder"
          rules={[{ required: true, message: 'Ring order is required' }]}
          extra="Position in a progressive rollout, lowest first. Environments sharing a ring are deployed to together."
        >
          <InputNumber min={0} style={{ width: '100%' }} />
        </Item>
      </Form>
    </Modal>
  )
}

export default DeploymentEnvironmentForm
