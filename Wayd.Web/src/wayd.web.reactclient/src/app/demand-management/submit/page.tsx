'use client'

import { PageTitle } from '@/src/components/common'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle, useLinkedEmployee } from '@/src/hooks'
import {
  useCreateIntakeRequestMutation,
  useGetIntakeChannelsQuery,
  useGetIntakeDesksQuery,
  useGetIntakeEmployeeOptionsQuery,
  useGetNeedByBasesQuery,
  useGetNeedByBasisFamiliesQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Alert, Button, Card, Flex, Form, Skeleton, Typography } from 'antd'
import { useRouter } from 'next/navigation'
import { FC, useState } from 'react'
import {
  NeedByFields,
  NeedByFormValues,
  needByFieldFor,
  toNeedByRequest,
} from '../_components/need-by'
import RequestTextFields from '../_components/request-text-fields'
import DeskChoice from './_components/desk-choice'

const { Item } = Form
const { Text } = Typography

interface SubmitRequestFormValues {
  deskId?: string
  requesterId?: string
  title: string
  description: string
  expectedBenefits?: string
  channelId: string
  needBy?: NeedByFormValues
}

/**
 * Submits a request. The desk is the first question only when more than one desk takes requests; with
 * one, the form says where the request goes, so a company with one review group never meets the concept.
 */
const SubmitIntakeRequestPage: FC = () => {
  useDocumentTitle('Submit a Request')
  const router = useRouter()
  const messageApi = useMessage()
  const [form] = Form.useForm<SubmitRequestFormValues>()
  const [isSaving, setIsSaving] = useState(false)
  const [createRequest] = useCreateIntakeRequestMutation()

  const { employeeId, hasLinkedEmployee } = useLinkedEmployee()
  const { data: desks, isLoading: desksLoading } = useGetIntakeDesksQuery({
    submittableOnly: true,
  })
  const { data: channels } = useGetIntakeChannelsQuery()
  const { data: employees } = useGetIntakeEmployeeOptionsQuery()
  const { data: bases } = useGetNeedByBasesQuery()
  const { data: families } = useGetNeedByBasisFamiliesQuery()

  const [chosenDeskId, setChosenDeskId] = useState<string>()
  const singleDesk = desks?.length === 1 ? desks[0] : undefined
  const deskId = singleDesk?.id ?? chosenDeskId

  const onSubmit = async (values: SubmitRequestFormValues) => {
    if (!deskId) return
    setIsSaving(true)
    try {
      const created = await createRequest({
        deskId,
        // The API defaults the requester to the caller; send one only when it is someone else.
        requesterId:
          values.requesterId && values.requesterId !== employeeId
            ? values.requesterId
            : undefined,
        title: values.title.trim(),
        description: values.description,
        expectedBenefits: values.expectedBenefits || undefined,
        channelId: values.channelId,
        needBy: toNeedByRequest(values.needBy, bases),
      }).unwrap()
      messageApi.success(`Request ${created.key} submitted.`)
      router.push(`/demand-management/requests/${created.key}`)
    } catch (error) {
      const apiError = toApiError(error)
      if (apiError.status === 422 && apiError.errors) {
        form.setFields(
          toFormErrors(apiError.errors, { fieldFor: needByFieldFor }),
        )
        messageApi.error('Correct the validation error(s) to continue.')
      } else {
        messageApi.error(apiError.detail ?? 'Failed to submit the request.')
      }
    } finally {
      setIsSaving(false)
    }
  }
  const content = () => {
    if (!hasLinkedEmployee) {
      return (
        <Alert
          type="info"
          showIcon
          title="Submitting a request needs your account linked to an employee record."
          description="Requests are made by and for employees. Ask an administrator to link your account."
        />
      )
    }
    if (desksLoading) return <Skeleton active paragraph={{ rows: 6 }} />
    if (!desks?.length) {
      return (
        <Alert
          type="info"
          showIcon
          title="No desk is taking requests right now."
          description="Requests can be submitted once a desk is open to them."
        />
      )
    }

    return (
      <Form
        form={form}
        layout="vertical"
        name="submit-intake-request-form"
        initialValues={{ requesterId: employeeId ?? undefined }}
        onFinish={onSubmit}
        onValuesChange={(changed: Partial<SubmitRequestFormValues>) => {
          if ('deskId' in changed) setChosenDeskId(changed.deskId)
        }}
        disabled={isSaving}
      >
        {singleDesk ? (
          <Item label="Where this request goes">
            <Flex vertical gap={2}>
              <Text>{singleDesk.description}</Text>
              <Text type="secondary" style={{ fontSize: 12 }}>
                {singleDesk.name}
              </Text>
            </Flex>
          </Item>
        ) : (
          <Item
            name="deskId"
            label="What kind of request is this?"
            rules={[{ required: true, message: 'Choose where this goes.' }]}
          >
            <DeskChoice desks={desks} />
          </Item>
        )}
        {deskId && (
          <>
            <Item
              name="requesterId"
              label="Who is this request for?"
              extra="You, unless you are submitting it on someone else's behalf."
              rules={[{ required: true, message: 'Choose who this is for.' }]}
            >
              <EmployeeSelect
                employees={(employees ?? []).map((e) => ({
                  label: e.name,
                  value: e.id,
                }))}
              />
            </Item>
            <RequestTextFields channels={channels} />
            <NeedByFields
              bases={bases}
              families={families}
              employees={employees}
            />
            <Flex gap={8}>
              <Button type="primary" htmlType="submit" loading={isSaving}>
                Submit Request
              </Button>
              <Button
                onClick={() => router.push('/demand-management/my-requests')}
                disabled={isSaving}
              >
                Cancel
              </Button>
            </Flex>
          </>
        )}
      </Form>
    )
  }

  return (
    <div className="page-gutters">
      <PageTitle title="Submit a Request" />
      <Card style={{ maxWidth: 800 }}>{content()}</Card>
    </div>
  )
}

// Submitting needs no permission, only a linked employee, which the page checks itself.
const SubmitIntakeRequestPageWithAuthorization = requireFeatureFlag(
  SubmitIntakeRequestPage,
  'demand-management',
)

export default SubmitIntakeRequestPageWithAuthorization
