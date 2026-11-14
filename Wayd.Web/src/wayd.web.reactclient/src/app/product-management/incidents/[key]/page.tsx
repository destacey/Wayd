'use client'

import { PageActions } from '@/src/components/common'
import {
  RecordLayout,
  RecordLoading,
  RecordSection,
} from '@/src/components/common/record'
import { WorkflowStatusTag } from '@/src/components/common/status-workflows'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useApplyIncidentWorkflowActionMutation,
  useGetIncidentQuery,
  useGetIncidentWorkflowActionsQuery,
  useUpdateIncidentDetailsMutation,
} from '@/src/store/features/product-management/incidents-api'
import { FollowOwnerTypes } from '@/src/store/features/common/follows-api'
import { useGetProductsQuery } from '@/src/store/features/product-management/products-api'
import { useGetReleasesQuery } from '@/src/store/features/product-management/releases-api'
import { useGetIncidentSeveritySetsQuery } from '@/src/store/features/product-management/incident-severity-sets-api'
import { useGetEmployeesQuery } from '@/src/store/features/organizations/employee-api'
import {
  App,
  Button,
  DatePicker,
  Flex,
  Form,
  Input,
  Result,
  Select,
} from 'antd'
import type { MenuProps } from 'antd'
import type { ItemType } from 'antd/es/menu/interface'
import dayjs, { Dayjs } from 'dayjs'
import { notFound } from 'next/navigation'
import { use, useEffect, useMemo, useState } from 'react'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { MarkdownEditor, MarkdownRenderer } from '@/src/components/common/markdown'
import { EmployeeSelect } from '@/src/components/common/organizations'
import IncidentDiscussion from '@/src/app/product-management/incidents/_components/incident-discussion'
import IncidentStatusStepper from '@/src/app/product-management/incidents/_components/incident-status-stepper'
import IncidentWorkItemsSection from '@/src/app/product-management/incidents/_components/incident-work-items-section'
import { IncidentSeverityRank } from '@/src/app/product-management/incidents/_components/incident-severity-rank'
import IncidentDurations from '@/src/app/product-management/incidents/_components/incident-durations'
import IncidentFacts from '@/src/app/product-management/incidents/_components/incident-facts'
import CancelIncidentForm from '@/src/app/product-management/incidents/_components/cancel-incident-form'
import { incidentTransitionDate } from '@/src/app/product-management/incidents/_components/incident-transition-date'
import { incidentWorkflowActionAvailability } from '@/src/app/product-management/incidents/_components/incident-workflow-actions'
import styles from '@/src/app/product-management/incidents/_components/incidents.module.css'
import {
  IncidentWorkflowAction,
  ApplyIncidentWorkflowActionRequest,
} from '@/src/services/wayd-api'
import { toApiError, toFormErrors } from '@/src/utils'

const { TextArea } = Input

enum IncidentSection {
  Overview = 'overview',
  WorkItems = 'work-items',
  Discussion = 'discussion',
}

const incidentSections: RecordSection[] = [
  { id: IncidentSection.Overview, label: 'Overview' },
  { id: IncidentSection.WorkItems, label: 'Work Items' },
  // The discussion draws its own heading, with its counts and filters beside it.
  {
    id: IncidentSection.Discussion,
    label: 'Discussion',
    hideHeading: true,
  },
]

const IncidentDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const messageApi = useMessage()
  // A confirm opened from the app context paints with the theme.
  const { modal } = App.useApp()
  const [editing, setEditing] = useState(false)
  const [cancelOpen, setCancelOpen] = useState(false)
  const [form] = Form.useForm()

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.Incidents.Update')

  const viewerZone = useMemo(
    () => Intl.DateTimeFormat().resolvedOptions().timeZone,
    [],
  )

  const { data: incident, error, isLoading, refetch } = useGetIncidentQuery(key)
  const [updateDetails, { isLoading: saving }] =
    useUpdateIncidentDetailsMutation()
  const [applyWorkflowAction, { isLoading: applyingWorkflow }] =
    useApplyIncidentWorkflowActionMutation()
  const {
    data: workflowActions,
    isError: workflowActionsFailed,
    isLoading: workflowActionsLoading,
  } = useGetIncidentWorkflowActionsQuery(key, { skip: !canUpdate })
  const { data: severitySets } = useGetIncidentSeveritySetsQuery(false)
  const { data: products } = useGetProductsQuery(undefined)
  const { data: releases } = useGetReleasesQuery(undefined)
  const { data: employees } = useGetEmployeesQuery(false)
  const employeeOptions = (employees ?? []).map((employee) => ({
    label: employee.displayName,
    value: employee.id,
  }))

  useDocumentTitle(incident ? `Incident ${incident.key}` : 'Incident')

  const isNotFound = (error as { status?: number })?.status === 404

  useEffect(() => {
    if (error && !isNotFound) {
      console.error(error)
      messageApi.error('Failed to load incident.')
    }
  }, [error, isNotFound, messageApi])

  if (isNotFound) {
    notFound()
  }

  if (error) {
    return (
      <Result
        status="error"
        title="Failed to load the incident"
        subTitle="Something went wrong fetching this incident."
        extra={
          <Button type="primary" onClick={() => refetch()}>
            Retry
          </Button>
        }
      />
    )
  }

  if (isLoading || !incident) {
    return <RecordLoading title="Incident" />
  }

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

  const startEditing = () => {
    form.setFieldsValue({
      title: incident.title,
      symptoms: incident.symptoms,
      remediation: incident.remediation,
      rootCause: incident.rootCause,
      severityLevelId: incident.severityLevel.id,
      noticedAt: dayjs(incident.noticedAt),
      startedAt: incident.startedAt ? dayjs(incident.startedAt) : undefined,
      recoveredAt: incident.recoveredAt
        ? dayjs(incident.recoveredAt)
        : undefined,
      productIds: incident.products.map((p) => p.id),
      releaseIds: incident.releases.map((release) => release.id),
      reportedByEmployeeId: incident.reportedBy.id,
      commanderEmployeeId: incident.commander?.id,
    })
    setEditing(true)
  }

  const save = async () => {
    const values = await form.validateFields()
    try {
      await updateDetails({
        id: incident.id,
        title: values.title,
        symptoms: values.symptoms,
        remediation: values.remediation,
        rootCause: values.rootCause,
        severityLevelId: values.severityLevelId,
        noticedAt: (values.noticedAt as Dayjs).toDate(),
        startedAt: values.startedAt
          ? (values.startedAt as Dayjs).toDate()
          : undefined,
        recoveredAt: values.recoveredAt
          ? (values.recoveredAt as Dayjs).toDate()
          : undefined,
        productIds: values.productIds ?? [],
        releaseIds: values.releaseIds ?? [],
        reportedByEmployeeId: values.reportedByEmployeeId,
        commanderEmployeeId: values.commanderEmployeeId,
      }).unwrap()
      messageApi.success('Incident updated.')
      setEditing(false)
      refetch()
    } catch (error) {
      const apiError = toApiError(error)
      if (apiError.errors) {
        form.setFields(toFormErrors(apiError.errors))
      }
      messageApi.error(apiError.detail ?? 'Failed to update incident.')
    }
  }

  const allowedActions =
    !canUpdate || workflowActionsLoading || workflowActionsFailed
      ? undefined
      : workflowActions?.map((a) => a.action)
  const workflowAvailability =
    incidentWorkflowActionAvailability(allowedActions)
  const lifecyclePending = canUpdate && workflowActionsLoading

  const applyLifecycleAction = async (
    action: IncidentWorkflowAction,
    request: ApplyIncidentWorkflowActionRequest,
    successMessage: string,
  ) => {
    try {
      await applyWorkflowAction({
        id: incident.id,
        idOrKey: key,
        request,
      }).unwrap()
      messageApi.success(successMessage)
      refetch()
    } catch (err) {
      messageApi.error(
        toApiError(err).detail ?? 'Could not update incident status.',
      )
      throw err
    }
  }

  const confirmLifecycleAction = (
    title: string,
    content: string,
    action: IncidentWorkflowAction,
    successMessage: string,
  ) => {
    const dateField = incidentTransitionDate(action, incident)
    const chosen = { current: dayjs() as Dayjs | null }
    const preset = chosen.current
    const dateLabel =
      dateField === 'recoveredAt' ? 'Recovered at' : 'Response started at'

    modal.confirm({
      title,
      content: dateField ? (
        <Flex vertical gap={8}>
          <span>{content}</span>
          {dateField === 'both' ? (
            <span>
              This time is recorded as the response start and the recovery.
            </span>
          ) : (
            <span>{dateLabel}</span>
          )}
          <DatePicker
            showTime
            defaultValue={preset ?? undefined}
            className="w-full"
            onChange={(value) => {
              chosen.current = value
            }}
          />
        </Flex>
      ) : (
        content
      ),
      okText: title,
      onOk: () => {
        const request: ApplyIncidentWorkflowActionRequest = { action }
        if (dateField) {
          request.occurredAt = (chosen.current ?? dayjs()).toDate()
        }
        return applyLifecycleAction(action, request, successMessage)
      },
    })
  }

  const actionsMenuItems: MenuProps['items'] = (() => {
    if (!canUpdate) {
      return []
    }

    const groups: ItemType[][] = []

    const corrections: ItemType[] = []
    if (!editing) {
      corrections.push({
        key: 'edit',
        label: 'Edit',
        onClick: startEditing,
      })
    }
    if (corrections.length > 0) {
      groups.push(corrections)
    }

    const lifecycle: ItemType[] = []
    if (workflowAvailability.canBeginResponding) {
      lifecycle.push({
        key: 'begin-responding',
        label: 'Begin responding',
        disabled: applyingWorkflow || lifecyclePending,
        onClick: () =>
          confirmLifecycleAction(
            'Begin responding',
            'Mark this incident as actively being worked.',
            IncidentWorkflowAction.BeginResponding,
            'Incident is now responding.',
          ),
      })
    }
    if (workflowAvailability.canMarkRecovered) {
      lifecycle.push({
        key: 'mark-recovered',
        label: 'Mark recovered',
        disabled: applyingWorkflow || lifecyclePending,
        onClick: () =>
          confirmLifecycleAction(
            'Mark recovered',
            'Record that service has been restored.',
            IncidentWorkflowAction.MarkRecovered,
            'Incident marked recovered.',
          ),
      })
    }
    if (workflowAvailability.canClose) {
      lifecycle.push({
        key: 'close',
        label: 'Close',
        disabled: applyingWorkflow || lifecyclePending,
        onClick: () =>
          confirmLifecycleAction(
            'Close incident',
            'Close the incident after recovery.',
            IncidentWorkflowAction.Close,
            'Incident closed.',
          ),
      })
    }
    if (workflowAvailability.canCancel) {
      lifecycle.push({
        key: 'cancel',
        label: 'Cancel',
        danger: true,
        disabled: applyingWorkflow || lifecyclePending,
        onClick: () => setCancelOpen(true),
      })
    }
    if (lifecycle.length > 0) {
      groups.push(lifecycle)
    }

    return groups
      .filter((group) => group.length > 0)
      .flatMap((group, index) =>
        index === 0
          ? group
          : [{ type: 'divider' as const, key: `divider-${index}` }, ...group],
      )
  })()

  const editToolbar = (
    <>
      <Button onClick={() => setEditing(false)}>Cancel</Button>
      <Button type="primary" loading={saving} onClick={() => void save()}>
        Save
      </Button>
    </>
  )

  const recordActions = editing ? (
    editToolbar
  ) : canUpdate && actionsMenuItems.length > 0 ? (
    <PageActions actionItems={actionsMenuItems} />
  ) : canUpdate ? (
    <Button onClick={startEditing}>Edit</Button>
  ) : null

  return (
    <>
      <RecordLayout
        // Editing replaces the overview. A single section keeps that form
        // mounted, so Save still has the fields if Edit was opened elsewhere.
        sections={
          editing
            ? incidentSections.filter(
                (section) => section.id === IncidentSection.Overview,
              )
            : incidentSections
        }
        defaultSection={IncidentSection.Overview}
        record={{
          name: incident.title,
          recordKey: String(incident.key),
          parent: {
            label: 'Incidents',
            href: '/product-management/incidents',
          },
          subtitle: 'Incident Details',
          tags: (
            <Flex gap="small" align="center" wrap>
              <IncidentSeverityRank
                name={incident.severityLevel.name}
                color={incident.severityLevel.color}
              />
              {incident.status ? (
                <WorkflowStatusTag
                  name={incident.status.name}
                  category={incident.status.category}
                />
              ) : null}
            </Flex>
          ),
          actions: recordActions,
          follow: {
            ownerType: FollowOwnerTypes.Incident,
            ownerId: incident.id,
          },
        }}
        facts={
          <IncidentFacts
            incident={incident}
            viewerZone={viewerZone}
            canUpdate={canUpdate}
            employees={employeeOptions}
          />
        }
      >
        {(section) => {
          if (section === IncidentSection.WorkItems) {
            return (
              <IncidentWorkItemsSection
                incidentId={incident.id}
                idOrKey={key}
                canManage={canUpdate}
              />
            )
          }

          if (section === IncidentSection.Discussion) {
            return <IncidentDiscussion incident={incident} />
          }

          if (editing) {
            return (
              <div className={styles.overview}>
                <IncidentStatusStepper
                  incidentId={incident.id}
                  currentStatusId={incident.status.id}
                  noticedAt={incident.noticedAt}
                  startedAt={incident.startedAt}
                  recoveredAt={incident.recoveredAt}
                  viewerZone={viewerZone}
                />
                <IncidentDurations durations={incident.durations} />
                <Form form={form} layout="vertical">
                  <Form.Item
                    name="title"
                    label="Title"
                    rules={[{ required: true }]}
                  >
                    <TextArea rows={2} />
                  </Form.Item>
                  <Form.Item name="symptoms" label="Symptoms">
                    <MarkdownEditor maxLength={4096} />
                  </Form.Item>
                  <Form.Item name="remediation" label="Remediation">
                    <MarkdownEditor maxLength={4096} />
                  </Form.Item>
                  <Form.Item name="rootCause" label="Root cause">
                    <MarkdownEditor maxLength={4096} />
                  </Form.Item>
                  <Form.Item
                    name="severityLevelId"
                    label="Severity"
                    rules={[{ required: true }]}
                  >
                    <Select options={severityOptions} />
                  </Form.Item>
                  <Form.Item name="productIds" label="Products">
                    <Select
                      mode="multiple"
                      options={productOptions}
                      allowClear
                    />
                  </Form.Item>
                  <Form.Item name="releaseIds" label="Releases">
                    <Select
                      mode="multiple"
                      options={releaseOptions}
                      allowClear
                    />
                  </Form.Item>
                  <Form.Item
                    name="noticedAt"
                    label="Noticed at"
                    rules={[{ required: true }]}
                  >
                    <DatePicker showTime className="w-full" />
                  </Form.Item>
                  <Form.Item name="startedAt" label="Response started at">
                    <DatePicker showTime className="w-full" />
                  </Form.Item>
                  <Form.Item name="recoveredAt" label="Recovered at">
                    <DatePicker showTime className="w-full" />
                  </Form.Item>
                  <Form.Item
                    name="reportedByEmployeeId"
                    label="Reported by"
                    rules={[
                      { required: true, message: 'Who reported it is required.' },
                    ]}
                  >
                    <EmployeeSelect employees={employeeOptions} />
                  </Form.Item>
                  <Form.Item name="commanderEmployeeId" label="Commander">
                    <EmployeeSelect employees={employeeOptions} />
                  </Form.Item>
                </Form>
              </div>
            )
          }

          return (
            <div className={styles.overview}>
              <IncidentStatusStepper
                incidentId={incident.id}
                currentStatusId={incident.status.id}
                noticedAt={incident.noticedAt}
                startedAt={incident.startedAt}
                recoveredAt={incident.recoveredAt}
                viewerZone={viewerZone}
              />
              <IncidentDurations durations={incident.durations} />
              {incident.symptoms ? (
                <div>
                  <h2 className={styles.fieldHeading}>Symptoms</h2>
                  <MarkdownRenderer markdown={incident.symptoms} />
                </div>
              ) : null}
              {incident.remediation ? (
                <div>
                  <h2 className={styles.fieldHeading}>Remediation</h2>
                  <MarkdownRenderer markdown={incident.remediation} />
                </div>
              ) : null}
              {incident.rootCause ? (
                <div>
                  <h2 className={styles.fieldHeading}>Root cause</h2>
                  <MarkdownRenderer markdown={incident.rootCause} />
                </div>
              ) : null}
            </div>
          )
        }}
      </RecordLayout>
      {cancelOpen ? (
        <CancelIncidentForm
          incident={incident}
          onFormComplete={() => {
            setCancelOpen(false)
            refetch()
          }}
          onFormCancel={() => setCancelOpen(false)}
        />
      ) : null}
    </>
  )
}

const IncidentDetailsPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    IncidentDetailsPage,
    'Permission',
    'Permissions.Incidents.View',
  ),
  'product-management',
)

export default requireFeatureFlag(
  IncidentDetailsPageWithAuthorization,
  'incident-management',
)
