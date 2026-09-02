'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  ReleaseDto,
  SetReleaseContentsRequest,
} from '@/src/services/wayd-api'
import { useSetReleaseContentsMutation } from '@/src/store/features/product-management/releases-api'
import { useGetReleasePackagesQuery } from '@/src/store/features/product-management/release-packages-api'
import { useGetVersionsQuery } from '@/src/store/features/product-management/versions-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, Form, Modal } from 'antd'
import ContentsEditor, { type ContentsDraft } from './contents-editor'

const { Item } = Form

export interface SetReleaseContentsFormProps {
  release: ReleaseDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface SetReleaseContentsFormValues {
  contents: ContentsDraft
}

/**
 * Sets everything a release announces.
 *
 * Every package and version the release should end up with is sent, not a delta: contents are a set,
 * and a partially-applied change would claim a combination that was never announced. Submitting
 * nothing clears the release, which is a legitimate state rather than a draft.
 *
 * Both routes are in one form because the API sets them in one call. That is what lets a version move
 * from being carried directly into a package that ships it: judged against the release's current
 * contents the move would look like a double count, so it is judged against what the release ends up
 * containing instead.
 */
const SetReleaseContentsForm = ({
  release,
  onFormComplete,
  onFormCancel,
}: SetReleaseContentsFormProps) => {
  const messageApi = useMessage()

  const [setContents] = useSetReleaseContentsMutation()
  const { data: versions, isLoading: versionsLoading } =
    useGetVersionsQuery(undefined)
  const { data: packages, isLoading: packagesLoading } =
    useGetReleasePackagesQuery(undefined)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<SetReleaseContentsFormValues>({
      onSubmit: async (values: SetReleaseContentsFormValues, form) => {
        try {
          const request = {
            versionIds: values.contents?.versionIds ?? [],
            packageIds: values.contents?.packageIds ?? [],
          } as SetReleaseContentsRequest

          const response = await setContents({
            id: release.id,
            cacheKey: release.key,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Release contents updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the contents. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the contents. Please try again.',
      permission: 'Permissions.Releases.Update',
    })

  const initialContents: ContentsDraft = {
    versionIds: (release.versions ?? []).map((entry) => entry.version.id),
    packageIds: (release.packages ?? []).map((entry) => entry.package.id),
  }

  return (
    <Modal
      title="Edit Contents"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      width={720}
      destroyOnHidden
    >
      <Alert
        type="info"
        showIcon
        title="This replaces everything the release announces"
        description="A package or version removed here is removed from the release. Clearing both is allowed: a repackaging or a pricing change is announced with nothing deployed."
        style={{ marginBottom: 16 }}
      />
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="set-release-contents-form"
        initialValues={{ contents: initialContents }}
      >
        <Item
          name="contents"
          rules={[
            {
              validator: (_, contents: ContentsDraft) => {
                // Mirrors the aggregate: a version shipping inside one of the selected packages
                // cannot also be carried directly. The picker disables most of these, but a version
                // already chosen stays selectable so it can be removed — which leaves this the only
                // guard against saving the conflict.
                const selectedPackages = (packages ?? []).filter((pkg) =>
                  contents?.packageIds?.includes(pkg.id),
                )
                const coveredIds = new Set(
                  selectedPackages.flatMap((pkg) =>
                    (pkg.components ?? [])
                      .map((component) => component.versionRecord?.id)
                      .filter((id): id is string => !!id),
                  ),
                )

                if (contents?.versionIds?.some((id) => coveredIds.has(id))) {
                  return Promise.reject(
                    new Error(
                      'A version shipping inside one of these packages cannot also be carried directly',
                    ),
                  )
                }

                return Promise.resolve()
              },
            },
          ]}
        >
          <ContentsEditor
            versions={versions ?? []}
            packages={packages ?? []}
            isLoading={versionsLoading || packagesLoading}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default SetReleaseContentsForm
