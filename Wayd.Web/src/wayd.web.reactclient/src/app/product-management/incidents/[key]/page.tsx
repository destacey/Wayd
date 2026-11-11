'use client'

import { PageActions } from '@/src/components/common'
import { RecordShell } from '@/src/components/common/record'
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
import { formatInstantInZone } from '@/src/utils'
import { Button, DatePicker, Flex, Form, Modal, Select } from 'antd'
import type { MenuProps } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import TextArea from 'antd/es/input/TextArea'
import dayjs, { Dayjs } from 'dayjs'
import Link from 'next/link'
import { notFound } from 'next/navigation'
import { use, useEffect, useMemo, useState } from 'react'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { MarkdownEditor } from '@/src/components/common/markdown'
import IncidentLinks from '@/src/app/product-management/incidents/_components/incident-links'
import IncidentDiscussion from '@/src/app/product-management/incidents/_components/incident-discussion'
import { IncidentSeverityRank } from '@/src/app/product-management/incidents/_components/incident-severity-rank'
import CancelIncidentForm from '@/src/app/product-management/incidents/_components/cancel-incident-form'
import { incidentWorkflowActionAvailability } from '@/src/app/product-management/incidents/_components/incident-workflow-actions'
import styles from '@/src/app/product-management/incidents/_components/incidents.module.css'
import {
  IncidentWorkflowAction,
  ApplyIncidentWorkflowActionRequest,
} from '@/src/services/wayd-api'
import { isApiError } from '@/src/utils'

const IncidentDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const messageApi = useMessage()
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

  useDocumentTitle(incident ? `Incident ${incident.key}` : 'Incident')

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load incident.')
    }
  }, [error, messageApi])

  if (!isLoading && !incident && error) {
    notFound()
  }

  if (!incident) {
    return null
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
      rootCause: incident.rootCause,
      severityLevelId: incident.severityLevel.id,
      noticedAt: dayjs(incident.noticedAt),
      startedAt: incident.startedAt ? dayjs(incident.startedAt) : undefined,
      recoveredAt: incident.recoveredAt
        ? dayjs(incident.recoveredAt)
        : undefined,
      productIds: incident.products.map((p) => p.id),
      releaseIds: incident.releases.map((release) => release.id),
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
      }).unwrap()
      messageApi.success('Incident updated.')
      setEditing(false)
      refetch()
    } catch {
      messageApi.error('Failed to update incident.')
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
        isApiError(err)
          ? (err.detail ?? 'Could not update incident status.')
          : 'Could not update incident status.',
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
    Modal.confirm({
      title,
      content,
      okText: title,
      onOk: () => applyLifecycleAction(action, { action }, successMessage),
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
      <RecordShell
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
      >
        {editing ? (
          <Form form={form} layout="vertical">
            <Form.Item name="title" label="Title" rules={[{ required: true }]}>
              <TextArea rows={2} />
            </Form.Item>
            <Form.Item name="symptoms" label="Symptoms">
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
              <Select mode="multiple" options={productOptions} allowClear />
            </Form.Item>
            <Form.Item name="releaseIds" label="Releases">
              <Select mode="multiple" options={releaseOptions} allowClear />
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
          </Form>
        ) : (
          <article className={styles.detail}>
            <div className={styles.detailMain}>
              {incident.symptoms ? (
                <>
                  <h2 className={styles.fieldHeading}>Symptoms</h2>
                  <p className={styles.desc}>{incident.symptoms}</p>
                </>
              ) : null}
              {incident.rootCause ? (
                <>
                  <h2 className={styles.fieldHeading}>Root cause</h2>
                  <p className={styles.desc}>{incident.rootCause}</p>
                </>
              ) : null}
              <div className={styles.discussionSection}>
                <IncidentDiscussion incident={incident} />
              </div>
            </div>
            <aside className={styles.detailAside}>
              <h2 className={styles.asideHeading}>Commander</h2>
              <ul className={styles.asideList}>
                <li>{incident.commander?.name ?? '—'}</li>
              </ul>
              <h2 className={styles.asideHeading}>Products</h2>
              <ul className={styles.asideList}>
                {incident.products.length > 0 ? (
                  incident.products.map((product) => (
                    <li key={product.id}>
                      <Link
                        href={`/product-management/products/${product.key}`}
                      >
                        {product.name}
                      </Link>
                    </li>
                  ))
                ) : (
                  <li>—</li>
                )}
              </ul>
              <h2 className={styles.asideHeading}>Releases</h2>
              <ul className={styles.asideList}>
                {incident.releases.length > 0 ? (
                  incident.releases.map((release) => (
                    <li key={release.id}>
                      <Link
                        href={`/product-management/releases/${release.key}`}
                      >
                        {release.name}
                      </Link>
                    </li>
                  ))
                ) : (
                  <li>—</li>
                )}
              </ul>
              <h2 className={styles.asideHeading}>Timeline</h2>
              <ul className={styles.asideList}>
                <li>
                  Noticed {formatInstantInZone(incident.noticedAt, viewerZone)}
                </li>
                <li>
                  Response started{' '}
                  {incident.startedAt
                    ? formatInstantInZone(incident.startedAt, viewerZone)
                    : '—'}
                </li>
                <li>
                  Recovered{' '}
                  {incident.recoveredAt
                    ? formatInstantInZone(incident.recoveredAt, viewerZone)
                    : '—'}
                </li>
              </ul>
              <IncidentLinks
                incidentId={incident.id}
                idOrKey={key}
                canManageIncident={canUpdate}
              />
            </aside>
          </article>
        )}
      </RecordShell>
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
