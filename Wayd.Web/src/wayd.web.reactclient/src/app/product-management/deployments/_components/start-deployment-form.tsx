'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { StartDeploymentRequest } from '@/src/services/wayd-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { useStartDeploymentMutation } from '@/src/store/features/product-management/deployments-api'
import { useGetDeploymentEnvironmentsQuery } from '@/src/store/features/product-management/deployment-environments-api'
import { useGetReleasePackagesQuery } from '@/src/store/features/product-management/release-packages-api'
import { useGetVersionsQuery } from '@/src/store/features/product-management/versions-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Input, Modal, Segmented, Select } from 'antd'
import { Dayjs } from 'dayjs'
import { useState } from 'react'

const { Item } = Form

export interface StartDeploymentFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

/** What a deployment carries. Exactly one, never both. */
type DeploymentSubject = 'Version' | 'Package'

interface StartDeploymentFormValues {
  versionId?: string
  packageId?: string
  environmentId: string
  artifactId?: string
  startedAt?: Dayjs
}

/**
 * Records a deployment starting.
 *
 * A deployment carries a version or a package, never both and never neither — the API validates that
 * in three places and answers a violation with a 422. The toggle makes it one choice with one picker
 * rather than two optional pickers, so the invalid combinations cannot be expressed at all.
 *
 * Only active environments are offered, because the handler refuses an inactive one. A retired
 * environment in the list would turn that into a failed submit rather than an unavailable choice.
 */
const StartDeploymentForm = ({
  onFormComplete,
  onFormCancel,
}: StartDeploymentFormProps) => {
  const messageApi = useMessage()

  // Which side is being deployed is UI state rather than a submitted field: the request carries a
  // release id or a package id, never a discriminator, so the toggle's only job is to decide which
  // picker is on screen.
  const [subject, setSubject] = useState<DeploymentSubject>('Version')

  const [startDeployment] = useStartDeploymentMutation()
  const { data: versions, isLoading: versionsLoading } =
    useGetVersionsQuery(undefined)
  const { data: packages, isLoading: packagesLoading } =
    useGetReleasePackagesQuery(undefined)
  const { data: environments, isLoading: environmentsLoading } =
    useGetDeploymentEnvironmentsQuery({ isActive: true })

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<StartDeploymentFormValues>({
      onSubmit: async (values: StartDeploymentFormValues, form) => {
        try {
          const request = {
            // Only the side the toggle names is sent. Carrying the other would be the exact
            // both-set case the API refuses.
            versionId: subject === 'Version' ? values.versionId : undefined,
            packageId: subject === 'Package' ? values.packageId : undefined,
            environmentId: values.environmentId,
            artifactId: values.artifactId,
            startedAt: values.startedAt?.toDate(),
          } as StartDeploymentRequest

          const response = await startDeployment(request)
          if (response.error) throw response.error

          messageApi.success(
            `Deployment started. Deployment key: ${response.data!.key}`,
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
                'An error occurred while starting the deployment. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while starting the deployment. Please try again.',
      permission: 'Permissions.Delivery.Create',
    })

  const versionOptions = (versions ?? [])
    .map((version) => ({
      value: version.id,
      label: `${version.product.name} ${version.number}`,
    }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  const packageOptions = (packages ?? [])
    .map((releasePackage) => ({
      value: releasePackage.id,
      label: releasePackage.name
        ? `${releasePackage.version} — ${releasePackage.name}`
        : releasePackage.version,
    }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  const environmentOptions = (environments ?? [])
    .map((environment) => ({
      value: environment.id,
      label: `${environment.name} (${environment.category})`,
    }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  return (
    <Modal
      title="Start Deployment"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Start"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="start-deployment-form"
      >
        <Item label="Deploying">
          <Segmented<DeploymentSubject>
            block
            options={['Version', 'Package']}
            value={subject}
            onChange={(value) => {
              setSubject(value)
              // Clearing the other side keeps a value picked before the toggle moved from being
              // submitted under the wrong field — the both-set case the API refuses.
              form.setFieldsValue(
                value === 'Version'
                  ? { packageId: undefined }
                  : { versionId: undefined },
              )
            }}
          />
        </Item>

        {subject === 'Version' ? (
          <Item
            label="Version"
            name="versionId"
            rules={[{ required: true, message: 'Version is required' }]}
          >
            <Select
              options={versionOptions}
              loading={versionsLoading}
              placeholder="Select a version"
              showSearch
              optionFilterProp="label"
            />
          </Item>
        ) : (
          <Item
            label="Package"
            name="packageId"
            rules={[{ required: true, message: 'Package is required' }]}
          >
            <Select
              options={packageOptions}
              loading={packagesLoading}
              placeholder="Select a package"
              showSearch
              optionFilterProp="label"
            />
          </Item>
        )}

        <Item
          label="Environment"
          name="environmentId"
          rules={[{ required: true, message: 'Environment is required' }]}
          extra="Only active environments can be deployed into."
        >
          <Select
            options={environmentOptions}
            loading={environmentsLoading}
            placeholder="Select an environment"
            showSearch
            optionFilterProp="label"
          />
        </Item>

        <Item
          label="Artifact"
          name="artifactId"
          rules={[{ max: 256, message: 'Artifact cannot be longer than 256 characters' }]}
          extra="The build that actually shipped — 4.8.2.008 where the release version is 4.8.2."
        >
          <Input />
        </Item>

        <Item
          label="Started At"
          name="startedAt"
          extra="Leave empty to record it as starting now."
        >
          <DatePicker showTime style={{ width: '100%' }} />
        </Item>
      </Form>
    </Modal>
  )
}

export default StartDeploymentForm
