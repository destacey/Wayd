'use client'

import { LabeledContent } from '@/src/components/common/content'
import PageActions from '@/src/components/common/page-actions'
import {
  createActionsColumn,
  WaydGrid,
} from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import {
  KpiMeasurementDto,
  KpiStatus,
  KpiTargetDirection,
} from '@/src/services/api/wayd.schemas'
import {
  KpiSubject,
  useCloseKpiMutation,
  useGetKpiQuery,
  useRemoveKpiMeasurementMutation,
  useRemoveKpiMutation,
  useReopenKpiMutation,
} from '@/src/store/features/common/kpis-api'
import { formatCalendarDate } from '@/src/utils/calendar-date'
import { getDrawerWidthPixels } from '@/src/utils'
import { Alert, Button, Drawer, Flex, Skeleton, Tag, Typography } from 'antd'
import type { ItemType } from 'antd/es/menu/interface'
import { useState } from 'react'
import { KpiHealthTag, KpiTrendTag } from '../kpi-status-tags'
import KpiConfirmForm from './kpi-confirm-form'
import KpiForm from './kpi-form'
import {
  atRiskLabel,
  describeAtRisk,
  describeChange,
  describeMeasurementTime,
  describeTarget,
  describeWindow,
  formatKpiValue,
  formatPercent,
  kpiHeadline,
} from './kpi-format'
import KpiHistoryChart from './kpi-history-chart'
import KpiMeasurementForm from './kpi-measurement-form'

const { Text, Paragraph } = Typography

/** Room for a handful of rows, so one or two measurements still read as a list. */
const MEASUREMENTS_MIN_HEIGHT = 240

type DialogState =
  | { dialog: 'edit' }
  | { dialog: 'record' }
  | { dialog: 'correct'; measurement: KpiMeasurementDto }
  | { dialog: 'remove-measurement'; measurement: KpiMeasurementDto }
  | { dialog: 'close' }
  | { dialog: 'reopen' }
  | { dialog: 'remove' }

export interface KpiDetailsDrawerProps {
  subject: KpiSubject
  kpiId: string
  onClose: () => void
}

/**
 * One KPI: what it is measured against, its history charted, and every
 * measurement, with the changes the viewer may make.
 */
const KpiDetailsDrawer = ({
  subject,
  kpiId,
  onClose,
}: KpiDetailsDrawerProps) => {
  const target = { ...subject, kpiId }
  const { data, isLoading, error, refetch } = useGetKpiQuery(target)
  const [dialog, setDialog] = useState<DialogState | null>(null)
  const closeDialog = () => setDialog(null)
  // Wider than most drawers, so the chart and the measurement grid have room.
  const [width] = useState(() =>
    Math.min(
      window.innerWidth,
      Math.max(640, Math.floor(getDrawerWidthPixels() * 1.4)),
    ),
  )

  const [closeKpi] = useCloseKpiMutation()
  const [reopenKpi] = useReopenKpiMutation()
  const [removeKpi] = useRemoveKpiMutation()
  const [removeMeasurement] = useRemoveKpiMeasurementMutation()

  const kpi = data?.kpi
  const isOpen = kpi?.status === KpiStatus.Active
  const canChange = !!data?.canManage
  const canChangeMeasurements = canChange && isOpen

  const columns: ColumnDef<KpiMeasurementDto, any>[] = [
    createActionsColumn<KpiMeasurementDto>({
      unavailable: !canChangeMeasurements,
      ariaLabel: 'Measurement actions',
      getItems: (m) => [
        {
          key: 'correct',
          label: 'Correct',
          onClick: () => setDialog({ dialog: 'correct', measurement: m }),
        },
        {
          key: 'remove',
          label: 'Remove',
          danger: true,
          onClick: () =>
            setDialog({ dialog: 'remove-measurement', measurement: m }),
        },
      ],
    }),
    {
      id: 'period',
      header: 'When',
      size: 210,
      accessorFn: (m) => m.measuredAt ?? m.periodEnd,
      cell: ({ row }) =>
        kpi ? describeMeasurementTime(row.original, kpi.window) : '',
    },
    {
      id: 'value',
      header: 'Value',
      size: 120,
      accessorKey: 'value',
      cell: ({ row }) =>
        formatKpiValue(row.original.value, kpi?.prefix, kpi?.suffix),
    },
    { id: 'sampleSize', header: 'Sample', size: 90, accessorKey: 'sampleSize' },
    {
      id: 'measuredBy',
      header: 'Measured By',
      size: 170,
      accessorFn: (m) => m.measuredBy?.name ?? '',
    },
    { id: 'note', header: 'Note', size: 240, accessorKey: 'note' },
  ]

  const actionItems: ItemType[] = !canChange
    ? []
    : [
        {
          key: 'edit',
          label: 'Edit',
          onClick: () => setDialog({ dialog: 'edit' }),
        },
        isOpen
          ? {
              key: 'close',
              label: 'Close',
              onClick: () => setDialog({ dialog: 'close' }),
            }
          : {
              key: 'reopen',
              label: 'Reopen',
              onClick: () => setDialog({ dialog: 'reopen' }),
            },
        { type: 'divider' },
        {
          key: 'remove',
          label: 'Remove',
          danger: true,
          onClick: () => setDialog({ dialog: 'remove' }),
        },
      ]

  const headline = kpi ? kpiHeadline(kpi) : undefined
  const atRisk = kpi ? describeAtRisk(kpi.goal, kpi.prefix, kpi.suffix) : null

  return (
    <Drawer
      title={kpi ? `${kpi.key} · ${kpi.name}` : 'KPI'}
      open
      onClose={onClose}
      size={width}
      destroyOnHidden
      extra={
        canChange && kpi ? (
          <Flex gap={8}>
            {canChangeMeasurements && (
              <Button
                type="primary"
                onClick={() => setDialog({ dialog: 'record' })}
              >
                Record Measurement
              </Button>
            )}
            <PageActions actionItems={actionItems} />
          </Flex>
        ) : undefined
      }
    >
      {isLoading ? (
        <Skeleton active />
      ) : error || !kpi || !data ? (
        <Alert
          type="error"
          showIcon
          title="The KPI could not be loaded. It may have been removed."
          action={
            <Button size="small" onClick={refetch}>
              Retry
            </Button>
          }
        />
      ) : (
        <Flex vertical gap={16}>
          <Flex gap={8} wrap align="center">
            {kpi.status === KpiStatus.Closed && <Tag>Closed</Tag>}
            {kpi.category && <Tag>{kpi.category.name}</Tag>}
            <KpiHealthTag health={kpi.reading.health} />
            <KpiTrendTag trend={kpi.reading.trend} />
          </Flex>
          {kpi.description && (
            <Paragraph style={{ marginBottom: 0 }}>{kpi.description}</Paragraph>
          )}

          <Flex gap={24} wrap>
            <LabeledContent label={headline!.label}>
              <Text strong style={{ fontSize: 18 }}>
                {headline!.text}
              </Text>
            </LabeledContent>
            {kpi.reading.averagedCount != null && (
              <LabeledContent label="Latest">
                {formatKpiValue(
                  kpi.reading.latestValue,
                  kpi.prefix,
                  kpi.suffix,
                )}
              </LabeledContent>
            )}
            <LabeledContent label="Each value covers">
              {describeWindow(kpi.window)}
              {kpi.rollingAverageOf != null && (
                <Text type="secondary">
                  {' '}
                  (read as the average of the last {kpi.rollingAverageOf})
                </Text>
              )}
            </LabeledContent>
            <LabeledContent label="Direction">
              {kpi.goal.direction === KpiTargetDirection.Increase
                ? 'Higher is better'
                : 'Lower is better'}
            </LabeledContent>
            <LabeledContent label="Baseline">
              {formatKpiValue(kpi.goal.baselineValue, kpi.prefix, kpi.suffix)}
              {kpi.goal.baselineWindowStart && kpi.goal.baselineWindowEnd && (
                <Text type="secondary">
                  {' '}
                  ({formatCalendarDate(kpi.goal.baselineWindowStart)} –{' '}
                  {formatCalendarDate(kpi.goal.baselineWindowEnd)})
                </Text>
              )}
            </LabeledContent>
            <LabeledContent label="Target">
              {describeTarget(kpi.goal, kpi.prefix, kpi.suffix)}
            </LabeledContent>
            {atRisk && (
              <LabeledContent label={atRiskLabel(kpi.goal.direction)}>
                {atRisk}
              </LabeledContent>
            )}
            <LabeledContent label="Change from baseline">
              {kpi.reading.changeFromBaseline == null
                ? '—'
                : describeChange(
                    kpi.reading.changeFromBaseline,
                    kpi.reading.changeFromBaselinePercent,
                    kpi.prefix,
                    kpi.suffix,
                  )}
            </LabeledContent>
            <LabeledContent label="Progress to target">
              {kpi.reading.progressToTarget == null
                ? '—'
                : formatPercent(kpi.reading.progressToTarget * 100)}
            </LabeledContent>
          </Flex>

          <KpiHistoryChart
            values={data.measurements.map((m) => ({
              periodStart: m.periodStart,
              periodEnd: m.periodEnd,
              value: m.value,
              measuredAt: m.measuredAt,
            }))}
            goal={kpi.goal}
            prefix={kpi.prefix ?? undefined}
            suffix={kpi.suffix ?? undefined}
            isLoading={false}
          />

          <div>
            <Text strong style={{ fontSize: 14 }}>
              Measurements
            </Text>
            <WaydGrid
              variant="simple"
              columns={columns}
              data={data.measurements}
              getRowId={(m) => m.id}
              emptyMessage="No measurements yet"
              minHeight={MEASUREMENTS_MIN_HEIGHT}
            />
          </div>
        </Flex>
      )}

      {kpi && dialog?.dialog === 'edit' && (
        <KpiForm
          subject={subject}
          kpi={kpi}
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        />
      )}
      {kpi && dialog?.dialog === 'record' && (
        <KpiMeasurementForm
          target={target}
          measurementWindow={kpi.window}
          prefix={kpi.prefix ?? undefined}
          suffix={kpi.suffix ?? undefined}
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        />
      )}
      {kpi && dialog?.dialog === 'correct' && (
        <KpiMeasurementForm
          target={target}
          measurementWindow={kpi.window}
          prefix={kpi.prefix ?? undefined}
          suffix={kpi.suffix ?? undefined}
          measurement={dialog.measurement}
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        />
      )}
      {kpi && dialog?.dialog === 'remove-measurement' && (
        <KpiConfirmForm
          title="Remove this measurement?"
          okText="Remove"
          danger
          onConfirm={() =>
            removeMeasurement({
              ...target,
              measurementId: dialog.measurement.id,
            }).unwrap()
          }
          successMessage="Measurement removed."
          failureMessage="The measurement could not be removed."
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        >
          {formatKpiValue(dialog.measurement.value, kpi.prefix, kpi.suffix)},{' '}
          {describeMeasurementTime(dialog.measurement, kpi.window)}
        </KpiConfirmForm>
      )}
      {kpi && dialog?.dialog === 'close' && (
        <KpiConfirmForm
          title="Close this KPI?"
          okText="Close KPI"
          onConfirm={() => closeKpi(target).unwrap()}
          successMessage="KPI closed."
          failureMessage="The KPI could not be closed."
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        >
          It leaves the scorecard and keeps its measurements as history. Reopen
          it to measure it again.
        </KpiConfirmForm>
      )}
      {kpi && dialog?.dialog === 'reopen' && (
        <KpiConfirmForm
          title="Reopen this KPI?"
          okText="Reopen"
          onConfirm={() => reopenKpi(target).unwrap()}
          successMessage="KPI reopened."
          failureMessage="The KPI could not be reopened."
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        >
          It returns to the scorecard and takes measurements again.
        </KpiConfirmForm>
      )}
      {kpi && dialog?.dialog === 'remove' && (
        <KpiConfirmForm
          title="Remove this KPI for good?"
          okText="Remove"
          danger
          onConfirm={async () => {
            await removeKpi(target).unwrap()
            onClose()
          }}
          successMessage="KPI removed."
          failureMessage="The KPI could not be removed."
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        >
          {kpi.name} and its {data?.measurements.length ?? 0} measurement(s) are
          deleted. Close it instead to keep its history.
        </KpiConfirmForm>
      )}
    </Drawer>
  )
}

export default KpiDetailsDrawer
