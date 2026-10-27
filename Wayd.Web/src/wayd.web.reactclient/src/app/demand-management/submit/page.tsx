'use client'

import { PageTitle } from '@/src/components/common'
import { EmployeeSelect } from '@/src/components/common/organizations'
import { useMessage } from '@/src/components/contexts/messaging'
import { requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle, useLinkedEmployee } from '@/src/hooks'
import {
  IntakeChannelDto,
  IntakeDeskDto,
  NeedByBasisDto,
} from '@/src/services/wayd-api'
import {
  useCreateIntakeRequestMutation,
  useGetIntakeChannelsQuery,
  useGetIntakeDesksQuery,
  useGetIntakeEmployeeOptionsQuery,
  useGetNeedByBasesQuery,
  useGetNeedByBasisFamiliesQuery,
} from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import {
  Alert,
  Button,
  Card,
  ConfigProvider,
  Flex,
  Form,
  Grid,
  Skeleton,
  Steps,
  Tag,
  Typography,
} from 'antd'
import type { NamePath } from 'antd/es/form/interface'
import { useRouter } from 'next/navigation'
import { FC, ReactNode, useState } from 'react'
import { NeedByFields } from '../_components/need-by-fields'
import {
  NeedByFormValues,
  NeedByTag,
  needByFieldFor,
  toNeedByRequest,
} from '../_components/need-by'
import {
  ExpectedBenefitsField,
  RequestAskFields,
} from '../_components/request-text-fields'
import { deskPhrase } from '../_components/desk-status'
import DeskChoice from './_components/desk-choice'
import WhatHappensNext from './_components/what-happens-next'
import { TOUCH_THEME } from '../_components/touch-theme'
import { STEPS, stepWithError } from './_components/submit-steps'
import styles from './page.module.css'

const { Item } = Form
const { Text, Title } = Typography
const { useBreakpoint } = Grid

interface SubmitRequestFormValues {
  requesterId?: string
  title: string
  description: string
  expectedBenefits?: string
  channelId: string
  needBy?: NeedByFormValues
}

const StepHeading: FC<{ title: string; children?: ReactNode }> = ({
  title,
  children,
}) => (
  <div className={styles.stepHeading}>
    <Title level={5} className={styles.stepTitle}>
      {title}
    </Title>
    {children && <Text type="secondary">{children}</Text>}
  </div>
)

const Fact: FC<{ label: string; children: ReactNode; wide?: boolean }> = ({
  label,
  children,
  wide,
}) => (
  <div className={wide ? styles.factWide : undefined}>
    <Text type="secondary" className={styles.factLabel}>
      {label}
    </Text>
    <div>{children}</div>
  </div>
)

/** The request as it will be sent, for the requester to check before submitting. */
const RequestReview: FC<{
  values: SubmitRequestFormValues
  desk: IntakeDeskDto
  channels: IntakeChannelDto[] | undefined
  requesterName: string | undefined
  bases: NeedByBasisDto[] | undefined
}> = ({ values, desk, channels, requesterName, bases }) => {
  const needBy = toNeedByRequest(values.needBy, bases)
  const family = bases?.find((b) => b.code === needBy?.basis)?.family
  return (
    <div className={styles.review}>
      <Fact label="Title" wide>
        <Text strong>{values.title}</Text>
      </Fact>
      <Fact label="Goes to">
        <Tag color="processing">{deskPhrase(desk.name)}</Tag>
      </Fact>
      <Fact label="Channel">
        {channels?.find((c) => c.id === values.channelId)?.name}
      </Fact>
      <Fact label="Requested by">{requesterName}</Fact>
      <Fact label="Needed by">
        <NeedByTag
          needBy={
            needBy && family
              ? {
                  date: needBy.date,
                  basis: needBy.basis,
                  family,
                  reference: needBy.reference,
                }
              : undefined
          }
        />
      </Fact>
    </div>
  )
}

/**
 * Submits a request. The desk is the first question only when more than one desk takes requests; with
 * one, the form says where the request goes, so a company with one review group never meets the concept.
 * The form itself is the same for every desk, in three steps: the ask, why and by when, and a check.
 */
const SubmitIntakeRequestPage: FC = () => {
  useDocumentTitle('Submit a Request')
  const router = useRouter()
  const messageApi = useMessage()
  const screens = useBreakpoint()
  const [form] = Form.useForm<SubmitRequestFormValues>()
  const [isSaving, setIsSaving] = useState(false)
  const [step, setStep] = useState(0)
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
  const desk = desks?.find((d) => d.id === deskId)

  const goTo = async (target: number) => {
    // Moving on checks the steps being left; going back never does.
    if (target > step) {
      const fields = STEPS.slice(0, target).flatMap((s) => s.fields)
      try {
        await form.validateFields(fields)
      } catch (error) {
        const names = (
          (error as { errorFields?: { name: NamePath }[] }).errorFields ?? []
        ).map((f) => f.name)
        setStep(stepWithError(names) ?? step)
        return
      }
    }
    setStep(target)
  }

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
        const fieldErrors = toFormErrors(apiError.errors, {
          fieldFor: needByFieldFor,
        })
        form.setFields(fieldErrors)
        const errorStep = stepWithError(fieldErrors.map((f) => f.name))
        if (errorStep !== undefined) setStep(errorStep)
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

    if (!desk) return <DeskChoice desks={desks} onPick={setChosenDeskId} />

    const values = form.getFieldsValue(true) as SubmitRequestFormValues
    const isLast = step === STEPS.length - 1

    return (
      <div className={styles.layout}>
        <Card className={styles.main}>
          <Form
            form={form}
            layout="vertical"
            name="submit-intake-request-form"
            initialValues={{ requesterId: employeeId ?? undefined }}
            onFinish={onSubmit}
            disabled={isSaving}
          >
            <Steps
              size="small"
              // On a phone the titles sit under the numbers, so the three steps stay one short row.
              responsive={false}
              titlePlacement={screens.md === false ? 'vertical' : 'horizontal'}
              current={step}
              onChange={goTo}
              className={styles.steps}
              items={STEPS.map((s) => ({ title: s.title }))}
            />

            {/* Every step stays mounted so its answers survive moving between steps. */}
            <div hidden={step !== 0}>
              <StepHeading title="What are you asking for?" />
              <RequestAskFields
                channels={channels}
                descriptionLabel="Describe it in your own words"
              />
              <Item
                name="requesterId"
                label="Requested by"
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
            </div>

            <div hidden={step !== 1}>
              <StepHeading title="Why does it matter, and by when?">
                This is what reviewers weigh it on when capacity is short.
              </StepHeading>
              <ExpectedBenefitsField label="What is it worth?" />
              <NeedByFields
                bases={bases}
                families={families}
                employees={employees}
              />
            </div>

            {isLast && (
              <div>
                <StepHeading title="Check and submit">
                  You can edit it while it is under review.
                </StepHeading>
                <RequestReview
                  values={values}
                  desk={desk}
                  channels={channels}
                  requesterName={
                    employees?.find((e) => e.id === values.requesterId)?.name
                  }
                  bases={bases}
                />
              </div>
            )}

            <Flex gap={8} wrap className={styles.actions}>
              {/* From the first step, Back returns to the desk question when there was one. */}
              {(step > 0 || !singleDesk) && (
                <Button
                  onClick={() =>
                    step > 0 ? goTo(step - 1) : setChosenDeskId(undefined)
                  }
                >
                  Back
                </Button>
              )}
              {isLast ? (
                <Button type="primary" htmlType="submit" loading={isSaving}>
                  Submit request
                </Button>
              ) : (
                <Button type="primary" onClick={() => goTo(step + 1)}>
                  Continue
                </Button>
              )}
              <Button
                type="text"
                className={styles.cancel}
                onClick={() => router.push('/demand-management/my-requests')}
                disabled={isSaving}
              >
                Cancel
              </Button>
            </Flex>
          </Form>
        </Card>
        <aside className={styles.aside}>
          <WhatHappensNext />
        </aside>
      </div>
    )
  }

  const page = (
    <div className="page-gutters">
      {/* Once there is a desk, the title names it, so it stays in view through every step. */}
      <PageTitle
        title={
          desk && hasLinkedEmployee
            ? `Submit a Request – ${desk.name}`
            : 'Submit a Request'
        }
      />
      {content()}
    </div>
  )

  // Always rendered, with the theme switched, so crossing the breakpoint never remounts the form.
  return (
    <ConfigProvider theme={screens.md === false ? TOUCH_THEME : undefined}>
      {page}
    </ConfigProvider>
  )
}

// Submitting needs no permission, only a linked employee, which the page checks itself.
const SubmitIntakeRequestPageWithAuthorization = requireFeatureFlag(
  SubmitIntakeRequestPage,
  'demand-management',
)

export default SubmitIntakeRequestPageWithAuthorization
