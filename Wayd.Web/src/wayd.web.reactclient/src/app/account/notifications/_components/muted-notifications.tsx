'use client'

import { Alert, Button, Spin, Switch, Tag, Typography } from 'antd'
import Link from 'next/link'
import { WaydEmpty } from '@/src/components/common'
import { recordHref } from '@/src/components/common/record/record-href'
import {
  recordId,
  typeLabel,
} from '@/src/components/common/record/record-identity'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  MutedRecordDto,
  NotificationReason,
  ReasonMuteDto,
} from '@/src/services/wayd-api'
import {
  useGetMutedRecordsInfiniteQuery,
  useGetReasonMutesQuery,
  useMuteReasonMutation,
  useUnmuteReasonMutation,
  useUnmuteRecordMutation,
} from '@/src/store/features/common/notification-mutes-api'
import { toApiError } from '@/src/utils/problem-details'
import { formatTimeAgo } from '@/src/utils/time-ago'
import styles from './muted-notifications.module.css'

/** How each reason that can be muted reads. A reason with no words is not offered. */
const reasonWording: Partial<
  Record<NotificationReason, { title: string; description: string }>
> = {
  [NotificationReason.Following]: {
    title: 'Records you follow',
    description:
      'Changes to anything you follow. They stay in your Following list.',
  },
  [NotificationReason.Requester]: {
    title: 'Requests you made',
    description:
      'Changes to intake requests you asked for, or that were submitted for you.',
  },
  [NotificationReason.Assigned]: {
    title: 'Requests assigned to you',
    description:
      'Changes to intake requests you are assigned, including being given or losing one. You stay assigned.',
  },
  [NotificationReason.Participant]: {
    title: 'Requests you take part in',
    description:
      'Changes to intake requests you were given access to, or submitted for someone else. You keep your access.',
  },
  [NotificationReason.Role]: {
    title: 'Records you work on',
    description:
      'Changes to projects and programs you hold a role on, and objectives of your team. You keep your roles.',
  },
}

/** What the muted-records list says when there are none. */
export const NoMutedRecordsMessage =
  'You have not muted any records. Mute one with the muted-speaker button beside it in your notifications.'

const mutedAgo = (mutedOn?: Date) => {
  const date = mutedOn ? new Date(mutedOn) : null
  return date && !Number.isNaN(date.getTime())
    ? `Muted ${formatTimeAgo(date)}`
    : 'Muted'
}

const ReasonMuteRow = ({ mute }: { mute: ReasonMuteDto }) => {
  const wording = reasonWording[mute.reason]
  const messageApi = useMessage()
  const [muteReason, muting] = useMuteReasonMutation()
  const [unmuteReason, unmuting] = useUnmuteReasonMutation()

  if (!wording) return null

  const onChange = async (muted: boolean) => {
    const response = muted
      ? await muteReason(mute.reason)
      : await unmuteReason(mute.reason)
    if ('error' in response) {
      messageApi.error(
        toApiError(response.error).detail ??
          `Could not ${muted ? 'mute' : 'unmute'} ${wording.title.toLowerCase()}.`,
      )
    }
  }

  const id = `mute-reason-${mute.reason}`

  return (
    <li className={styles.row}>
      <div className={styles.text}>
        <label htmlFor={id} className={styles.title}>
          {wording.title}
        </label>
        <span className={styles.secondary}>{wording.description}</span>
        {mute.isMuted ? (
          <span className={styles.tertiary}>{mutedAgo(mute.mutedOn)}</span>
        ) : null}
      </div>
      <Switch
        id={id}
        checked={mute.isMuted}
        loading={muting.isLoading || unmuting.isLoading}
        checkedChildren="Muted"
        unCheckedChildren="On"
        onChange={onChange}
      />
    </li>
  )
}

const MutedRecordRow = ({ record }: { record: MutedRecordDto }) => {
  const messageApi = useMessage()
  const [unmuteRecord, { isLoading }] = useUnmuteRecordMutation()
  const href = recordHref(record)

  const onUnmute = async () => {
    const response = await unmuteRecord({
      ownerType: record.ownerType,
      ownerId: record.id,
    })
    if ('error' in response) {
      messageApi.error(
        toApiError(response.error).detail ?? `Could not unmute ${record.name}.`,
      )
    }
  }

  return (
    <li className={styles.row}>
      <div className={styles.text}>
        {href ? (
          <Link href={href} className={styles.title}>
            {record.name}
          </Link>
        ) : (
          <span className={styles.title}>{record.name}</span>
        )}
        <span className={styles.secondary}>
          {typeLabel(record.ownerType)} {record.key}
          {record.parentName ? ` · ${record.parentName}` : ''}
          {' · '}
          {mutedAgo(record.mutedOn)}
          {record.isFollowing ? ' · You still follow this' : ''}
        </span>
      </div>
      <Tag className={styles.status}>{record.statusName}</Tag>
      <Button
        loading={isLoading}
        aria-label={`Unmute ${record.name}`}
        onClick={onUnmute}
      >
        Unmute
      </Button>
    </li>
  )
}

/**
 * Everything the reader has muted, in one place: the reasons they are no
 * longer told for, and the records they hear nothing about.
 */
const MutedNotifications = () => {
  const reasons = useGetReasonMutesQuery(undefined, {
    refetchOnMountOrArgChange: true,
  })
  const records = useGetMutedRecordsInfiniteQuery(undefined, {
    refetchOnMountOrArgChange: true,
  })

  const mutedRecords = records.data?.pages.flatMap((page) => page.items) ?? []

  return (
    <div className={styles.sections}>
      <section className={styles.section} aria-labelledby="muted-reasons">
        <h2 id="muted-reasons" className={styles.heading}>
          Reasons
        </h2>
        <Typography.Text type="secondary">
          Mute a reason to stop being told about every record for it. You are
          still told when another reason applies.
        </Typography.Text>
        <div className={styles.pane}>
          {reasons.isError ? (
            <div className={styles.state}>
              <Alert type="error" title="Could not load your muted reasons." />
            </div>
          ) : reasons.isLoading ? (
            <div className={styles.state}>
              <Spin />
            </div>
          ) : (
            <ul className={styles.list} role="list" aria-label="Reasons">
              {(reasons.data ?? []).map((mute) => (
                <ReasonMuteRow key={mute.reason} mute={mute} />
              ))}
            </ul>
          )}
        </div>
      </section>

      <section className={styles.section} aria-labelledby="muted-records">
        <h2 id="muted-records" className={styles.heading}>
          Muted records
        </h2>
        <Typography.Text type="secondary">
          You hear nothing about these, whatever the reason. Muting changes
          nothing else: you keep your access, roles and follows.
        </Typography.Text>
        <div className={styles.pane}>
          {records.isError ? (
            <div className={styles.state}>
              <Alert type="error" title="Could not load your muted records." />
            </div>
          ) : records.isLoading ? (
            <div className={styles.state}>
              <Spin />
            </div>
          ) : mutedRecords.length === 0 ? (
            <div className={styles.state}>
              <WaydEmpty message={NoMutedRecordsMessage} />
            </div>
          ) : (
            <>
              <ul
                className={styles.list}
                role="list"
                aria-label="Muted records"
              >
                {mutedRecords.map((record) => (
                  <MutedRecordRow key={recordId(record)} record={record} />
                ))}
              </ul>
              {records.hasNextPage ? (
                <div className={styles.more}>
                  <Button
                    loading={records.isFetchingNextPage}
                    onClick={() => records.fetchNextPage()}
                  >
                    Load more
                  </Button>
                </div>
              ) : null}
            </>
          )}
        </div>
      </section>
    </div>
  )
}

export default MutedNotifications
