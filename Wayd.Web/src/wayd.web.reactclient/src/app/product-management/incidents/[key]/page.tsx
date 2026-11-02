'use client'

import { WorkflowStatusTag } from '@/src/components/common/status-workflows'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetIncidentQuery,
  useUpdateIncidentDetailsMutation,
} from '@/src/store/features/product-management/incidents-api'
import { useGetProductsQuery } from '@/src/store/features/product-management/products-api'
import { useGetIncidentSeveritySetsQuery } from '@/src/store/features/product-management/incident-severity-sets-api'
import { formatInstantInZone } from '@/src/utils'
import { Button, DatePicker, Form, Select } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import dayjs, { Dayjs } from 'dayjs'
import { notFound } from 'next/navigation'
import { use, useEffect, useMemo, useState } from 'react'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { MarkdownEditor } from '@/src/components/common/markdown'
import { IncidentSeverityRank } from '@/src/app/product-management/incidents/_components/incident-severity-rank'
import IncidentLinks from '@/src/app/product-management/incidents/_components/incident-links'
import IncidentDiscussion from '@/src/app/product-management/incidents/_components/incident-discussion'
import styles from '@/src/app/product-management/incidents/_components/incidents.module.css'

const IncidentDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const messageApi = useMessage()
  const [editing, setEditing] = useState(false)
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
  const { data: severitySets } = useGetIncidentSeveritySetsQuery(false)
  const { data: products } = useGetProductsQuery(undefined)

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

  const productMeta =
    incident.products.length > 0
      ? incident.products.map((p) => p.name).join(', ')
      : null

  const startEditing = () => {
    form.setFieldsValue({
      title: incident.title,
      description: incident.description,
      severityLevelId: incident.severityLevel.id,
      noticedAt: dayjs(incident.noticedAt),
      startedAt: incident.startedAt ? dayjs(incident.startedAt) : undefined,
      recoveredAt: incident.recoveredAt
        ? dayjs(incident.recoveredAt)
        : undefined,
      productIds: incident.products.map((p) => p.id),
    })
    setEditing(true)
  }

  const save = async () => {
    const values = await form.validateFields()
    try {
      await updateDetails({
        id: incident.id,
        title: values.title,
        description: values.description,
        severityLevelId: values.severityLevelId,
        noticedAt: (values.noticedAt as Dayjs).toDate(),
        startedAt: values.startedAt
          ? (values.startedAt as Dayjs).toDate()
          : undefined,
        recoveredAt: values.recoveredAt
          ? (values.recoveredAt as Dayjs).toDate()
          : undefined,
        productIds: values.productIds ?? [],
      }).unwrap()
      messageApi.success('Incident updated.')
      setEditing(false)
      refetch()
    } catch {
      messageApi.error('Failed to update incident.')
    }
  }

  const actions =
    canUpdate && !editing ? (
      <Button onClick={startEditing}>Edit</Button>
    ) : editing ? (
      <>
        <Button onClick={() => setEditing(false)}>Cancel</Button>
        <Button type="primary" loading={saving} onClick={() => void save()}>
          Save
        </Button>
      </>
    ) : null

  return (
    <div className="page-gutters">
      {actions ? <div className={styles.detailToolbar}>{actions}</div> : null}
      {editing ? (
        <Form form={form} layout="vertical">
          <Form.Item name="title" label="Title" rules={[{ required: true }]}>
            <TextArea rows={2} />
          </Form.Item>
          <Form.Item name="description" label="Description">
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
          <Form.Item name="noticedAt" label="Noticed at" rules={[{ required: true }]}>
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
        <>
          <article className={styles.detail}>
            <section className={styles.detailMain}>
              <p className={styles.meta}>
                INC-{incident.key}
                {productMeta ? ` · ${productMeta}` : null}
              </p>
              <h1 className={styles.titleRow}>
                <IncidentSeverityRank
                  name={incident.severityLevel.name}
                  color={incident.severityLevel.color}
                />
                <span>{incident.title}</span>
              </h1>
              {incident.description ? (
                <p className={styles.desc}>{incident.description}</p>
              ) : null}
            </section>
            <aside className={styles.detailAside}>
              <h2 className={styles.asideHeading}>Status</h2>
              <ul className={styles.asideList}>
                <li>
                  {incident.status ? (
                    <WorkflowStatusTag
                      name={incident.status.name}
                      category={incident.status.category}
                    />
                  ) : (
                    '—'
                  )}
                </li>
              </ul>
              <h2 className={styles.asideHeading}>Commander</h2>
              <ul className={styles.asideList}>
                <li>{incident.commander?.name ?? '—'}</li>
              </ul>
              <h2 className={styles.asideHeading}>Products</h2>
              <ul className={styles.asideList}>
                <li>
                  {incident.products.length > 0
                    ? incident.products.map((p) => p.name).join(', ')
                    : '—'}
                </li>
              </ul>
              <h2 className={styles.asideHeading}>Timeline</h2>
              <ul className={styles.asideList}>
                <li>
                  Noticed{' '}
                  {formatInstantInZone(incident.noticedAt, viewerZone)}
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
          <div className={styles.discussionSection}>
            <IncidentDiscussion incident={incident} />
          </div>
        </>
      )}
    </div>
  )
}

const IncidentDetailsPageWithAuthorization = requireFeatureFlag(
  authorizePage(IncidentDetailsPage, 'Permission', 'Permissions.Incidents.View'),
  'product-management',
)

export default requireFeatureFlag(
  IncidentDetailsPageWithAuthorization,
  'incident-management',
)
