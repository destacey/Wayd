'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { AssembleReleasePackageRequest } from '@/src/services/wayd-api'
import { useAssembleReleasePackageMutation } from '@/src/store/features/delivery/release-packages-api'
import { useGetVersionsQuery } from '@/src/store/features/delivery/versions-api'
import { useGetProductsQuery } from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Input, Modal } from 'antd'
import { Dayjs } from 'dayjs'
import ManifestEditor, {
  emptyManifestEntry,
  type ManifestEntryDraft,
} from './manifest-editor'

const { Item } = Form

export interface AssembleReleasePackageFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AssembleReleasePackageFormValues {
  version: string
  name?: string
  targetDate?: Dayjs
  components: ManifestEntryDraft[]
}

/**
 * Assembles several component releases into one shipment.
 *
 * The manifest is authored here rather than added afterwards because a package with no components is
 * not a package — the domain refuses one, so an empty manifest would only produce a failed submit.
 */
const AssembleReleasePackageForm = ({
  onFormComplete,
  onFormCancel,
}: AssembleReleasePackageFormProps) => {
  const messageApi = useMessage()

  const [assembleReleasePackage] = useAssembleReleasePackageMutation()
  const { data: products, isLoading: productsLoading } =
    useGetProductsQuery(undefined)
  const { data: versions } = useGetVersionsQuery(undefined)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AssembleReleasePackageFormValues>({
      onSubmit: async (values: AssembleReleasePackageFormValues, form) => {
        try {
          const request = {
            version: values.version,
            name: values.name,
            targetDate: values.targetDate?.format('YYYY-MM-DD'),
            components: values.components.map((entry) => ({
              productId: entry.productId,
              versionId: entry.versionId,
              version: entry.version,
              kind: entry.kind,
            })),
          } as unknown as AssembleReleasePackageRequest

          const response = await assembleReleasePackage(request)
          if (response.error) throw response.error

          messageApi.success(
            `Release package created successfully. Package key: ${response.data!.key}`,
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
                'An error occurred while assembling the package. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while assembling the package. Please try again.',
      permission: 'Permissions.Delivery.Create',
    })

  return (
    <Modal
      title="Assemble Package"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Assemble"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      width={760}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="assemble-release-package-form"
        initialValues={{ components: [emptyManifestEntry()] }}
      >
        <Item
          label="Version"
          name="version"
          rules={[
            { required: true, message: 'Version is required' },
            { max: 128, message: 'Version cannot be longer than 128 characters' },
          ]}
          extra="The package's own version, distinct from any component's."
        >
          <Input />
        </Item>
        <Item
          label="Name"
          name="name"
          rules={[{ max: 256, message: 'Name cannot be longer than 256 characters' }]}
        >
          <Input />
        </Item>
        <Item label="Target Date" name="targetDate">
          <DatePicker style={{ width: '100%' }} />
        </Item>
        <Item
          label="Components"
          name="components"
          rules={[
            {
              validator: (_, entries: ManifestEntryDraft[]) => {
                if (!entries?.length) {
                  return Promise.reject(
                    new Error('A package ships at least one component'),
                  )
                }
                if (entries.some((entry) => !entry.productId)) {
                  return Promise.reject(
                    new Error('Every component needs a product'),
                  )
                }
                if (entries.some((entry) => !entry.version?.trim())) {
                  return Promise.reject(
                    new Error('Every component needs a version'),
                  )
                }
                return Promise.resolve()
              },
            },
          ]}
          extra="A component may appear only once. Carried-forward entries record what shipped unchanged."
        >
          <ManifestEditor
            products={products ?? []}
            versions={versions ?? []}
            isLoading={productsLoading}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default AssembleReleasePackageForm
