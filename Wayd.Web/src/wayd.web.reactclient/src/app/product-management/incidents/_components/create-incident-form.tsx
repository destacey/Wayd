'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { CreateIncidentRequest } from '@/src/services/wayd-api'
import { useGetProductsQuery } from '@/src/store/features/product-management/products-api'
import { useGetReleasesQuery } from '@/src/store/features/product-management/releases-api'
import { useGetIncidentSeveritySetsQuery } from '@/src/store/features/product-management/incident-severity-sets-api'
import { useCreateIncidentMutation } from '@/src/store/features/product-management/incidents-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { isApiError, toFormErrors, type ApiError } from '@/src/utils'
import { DatePicker, Form, Modal, Select } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { Dayjs } from 'dayjs'
import { useRouter } from 'next/navigation'

const { Item } = Form

export interface CreateIncidentFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateIncidentFormValues {
  title: string
  symptoms?: string
  rootCause?: string
  severityLevelId: string
  noticedAt?: Dayjs
  startedAt?: Dayjs
  recoveredAt?: Dayjs
  productIds?: string[]
  releaseIds?: string[]
}

export function CreateIncidentForm({
  onFormComplete,
  onFormCancel,
}: CreateIncidentFormProps) {
  const messageApi = useMessage()
  const router = useRouter()
  const [createIncident] = useCreateIncidentMutation()
  const { data: severitySets } = useGetIncidentSeveritySetsQuery(false)
  const { data: products } = useGetProductsQuery(undefined)
  const { data: releases } = useGetReleasesQuery(undefined)

  const selectedSet = severitySets?.find((set) => set.isSelected)
  const severityOptions = (selectedSet?.levels ?? [])
    .map((level) => ({ value: level.id, label: level.name }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  const productOptions = (products ?? [])
    .map((product) => ({ value: product.id, label: product.name }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  const releaseOptions = (releases ?? [])
    .map((release) => ({
      value: release.id,
      label: release.name
        ? `${release.version} — ${release.name}`
        : release.version,
    }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateIncidentFormValues>({
      onSubmit: async (values, form) => {
        try {
          const request: CreateIncidentRequest = {
            title: values.title,
            symptoms: values.symptoms,
            rootCause: values.rootCause,
            severityLevelId: values.severityLevelId,
            noticedAt: values.noticedAt?.toDate(),
            startedAt: values.startedAt?.toDate(),
            recoveredAt: values.recoveredAt?.toDate(),
            productIds: values.productIds ?? [],
            releaseIds: values.releaseIds ?? [],
          }
          const response = await createIncident(request)
          if (response.error) throw response.error

          messageApi.success(
            `Incident opened. Incident key: ${response.data!.key}`,
          )
          router.push(`/product-management/incidents/${response.data!.key}`)
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while opening the incident. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while opening the incident. Please try again.',
      permission: 'Permissions.Incidents.Create',
    })

  return (
    <Modal
      title="Open Incident"
      open={isOpen}
      onOk={handleOk}
      onCancel={handleCancel}
      okButtonProps={{ disabled: !isValid }}
      okText="Open"
      confirmLoading={isSaving}
      destroyOnHidden
      width={640}
    >
      <Form form={form} layout="vertical" size="small">
        <Item
          name="title"
          label="Title"
          rules={[{ required: true, message: 'Title is required.' }]}
        >
          <TextArea rows={2} />
        </Item>
        <Item name="symptoms" label="Symptoms">
          <MarkdownEditor maxLength={4096} />
        </Item>
        <Item name="rootCause" label="Root cause">
          <MarkdownEditor maxLength={4096} />
        </Item>
        <Item
          name="severityLevelId"
          label="Severity"
          rules={[{ required: true, message: 'Severity is required.' }]}
        >
          <Select options={severityOptions} showSearch optionFilterProp="label" />
        </Item>
        <Item name="productIds" label="Products">
          <Select
            mode="multiple"
            options={productOptions}
            allowClear
            showSearch
            optionFilterProp="label"
          />
        </Item>
        <Item name="releaseIds" label="Releases">
          <Select
            mode="multiple"
            options={releaseOptions}
            allowClear
            showSearch
            optionFilterProp="label"
          />
        </Item>
        <Item name="noticedAt" label="Noticed at">
          <DatePicker showTime className="w-full" />
        </Item>
        <Item name="startedAt" label="Response started at">
          <DatePicker showTime className="w-full" />
        </Item>
        <Item name="recoveredAt" label="Recovered at">
          <DatePicker showTime className="w-full" />
        </Item>
      </Form>
    </Modal>
  )
}
