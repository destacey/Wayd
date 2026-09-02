'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  ReleasePackageDto,
  SetReleasePackageManifestRequest,
} from '@/src/services/wayd-api'
import { useSetReleasePackageManifestMutation } from '@/src/store/features/delivery/release-packages-api'
import { useGetReleasesQuery } from '@/src/store/features/delivery/releases-api'
import { useGetProductsQuery } from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, Form, Modal } from 'antd'
import ManifestEditor, { type ManifestEntryDraft } from './manifest-editor'

const { Item } = Form

export interface SetReleasePackageManifestFormProps {
  releasePackage: ReleasePackageDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface SetReleasePackageManifestFormValues {
  components: ManifestEntryDraft[]
}

/**
 * Replaces a package's manifest.
 *
 * Every line the package should end up with is sent, not a delta: components carry no id and cannot
 * be addressed individually, so a line left out is a line removed. The editor starts from what is
 * already recorded for that reason.
 */
const SetReleasePackageManifestForm = ({
  releasePackage,
  onFormComplete,
  onFormCancel,
}: SetReleasePackageManifestFormProps) => {
  const messageApi = useMessage()

  const [setManifest] = useSetReleasePackageManifestMutation()
  const { data: products, isLoading: productsLoading } =
    useGetProductsQuery(undefined)
  const { data: releases } = useGetReleasesQuery(undefined)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<SetReleasePackageManifestFormValues>({
      onSubmit: async (values: SetReleasePackageManifestFormValues, form) => {
        try {
          const request = {
            components: values.components.map((entry) => ({
              productId: entry.productId,
              releaseId: entry.releaseId,
              version: entry.version,
              kind: entry.kind,
            })),
          } as unknown as SetReleasePackageManifestRequest

          const response = await setManifest({
            id: releasePackage.id,
            cacheKey: releasePackage.key,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Manifest updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the manifest. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the manifest. Please try again.',
      permission: 'Permissions.ReleasePackages.Update',
    })

  const initialComponents: ManifestEntryDraft[] = (
    releasePackage.components ?? []
  ).map((component) => ({
    productId: component.product.id,
    releaseId: component.release?.id,
    version: component.version,
    kind: component.kind,
  }))

  return (
    <Modal
      title="Edit Manifest"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      width={760}
      destroyOnHidden
    >
      <Alert
        type="info"
        showIcon
        title="This replaces the whole manifest"
        description="A component removed here is removed from the package. Components are not individually addressable."
        style={{ marginBottom: 16 }}
      />
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="set-release-package-manifest-form"
        initialValues={{ components: initialComponents }}
      >
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
          extra="A component may appear only once."
        >
          <ManifestEditor
            products={products ?? []}
            releases={releases ?? []}
            isLoading={productsLoading}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default SetReleasePackageManifestForm
