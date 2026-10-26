'use client'

import { ArrowLeftOutlined, EyeFilled, MutedOutlined } from '@ant-design/icons'
import {
  Alert,
  Button,
  Flex,
  Progress,
  Skeleton,
  Tag,
  Tooltip,
  Typography,
} from 'antd'
import dayjs from 'dayjs'
import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { useEffect, useRef } from 'react'
import StageTimeline from '@/src/app/ppm/_components/stage-timeline'
import { ActivityLogTimeline } from '@/src/components/common/activities'
import { LifecycleStatusTag, PersonAvatar } from '@/src/components/common'
import { useMessage } from '@/src/components/contexts/messaging'
import { FollowedRecordDetailDto, RecordPerson } from '@/src/services/wayd-api'
import {
  FollowOwnerType,
  FollowOwnerTypes,
  useGetFollowedRecordQuery,
  useUnfollowMutation,
} from '@/src/store/features/common/follows-api'
import { useGetProjectQuery } from '@/src/store/features/ppm/projects-api'
import { formatCalendarDate, toCalendarDate } from '@/src/utils/calendar-date'
import { toApiError } from '@/src/utils/problem-details'
import styles from '../following.module.css'
import { recordHref } from '@/src/components/common/record/record-href'
import FollowedRecordHealth from './followed-record-health'
import { formatTimeAgo } from '@/src/utils/time-ago'

const { Title, Text } = Typography

export interface FollowedRecordDetailProps {
  ownerType: string
  ownerId: string
  /** What to call the record's kind, such as "Project". */
  typeName: string
  /** Shown full-screen with a back control and a pinned action bar. */
  asView?: boolean
  onBack?: () => void
  /** Moves keyboard focus to the record's name once it loads. */
  autoFocus?: boolean
  /** Called once the record is unfollowed, so the page can move on. */
  onUnfollowed: () => void
}

interface FactProps {
  label: string
  children: React.ReactNode
}

const Fact = ({ label, children }: FactProps) => (
  <div>
    <span className={styles.factLabel}>{label}</span>
    {children}
  </div>
)

/** A project's stages, loaded from its own module only once one is selected. */
const ProjectStages = ({ projectKey }: { projectKey: string }) => {
  const { data, isLoading } = useGetProjectQuery(projectKey)

  return isLoading ? (
    <Skeleton active paragraph={{ rows: 1 }} title={false} />
  ) : data?.stages?.length ? (
    <StageTimeline stages={data.stages} displayMode="small" />
  ) : (
    <Text type="secondary">No stages planned.</Text>
  )
}

/** The people grouped by role, in the order the API ranks them: owners, then managers. */
const roleGroups = (people: RecordPerson[]): [string, RecordPerson[]][] => {
  const groups = new Map<string, RecordPerson[]>()
  for (const person of people) {
    groups.set(person.role, [...(groups.get(person.role) ?? []), person])
  }
  return [...groups.entries()]
}

const dateRange = (detail: FollowedRecordDetailDto) =>
  detail.start || detail.end
    ? `${formatCalendarDate(detail.start) || '…'} – ${formatCalendarDate(detail.end) || '…'}`
    : 'Not set'

/**
 * One followed record: where it stands, who owns it, and what has happened to
 * it since you followed it. Open and Unfollow live here, once, rather than on
 * every row of the list.
 */
const FollowedRecordDetail = ({
  ownerType,
  ownerId,
  typeName,
  asView = false,
  autoFocus = false,
  onBack,
  onUnfollowed,
}: FollowedRecordDetailProps) => {
  const messageApi = useMessage()
  const target = { ownerType: ownerType as FollowOwnerType, ownerId }
  const router = useRouter()
  const {
    data: detail,
    isLoading,
    isError,
    error,
    refetch,
  } = useGetFollowedRecordQuery(target)
  const [unfollow, { isLoading: isUnfollowing }] = useUnfollowMutation()
  const titleLinkRef = useRef<HTMLAnchorElement>(null)
  const hasDetail = !!detail

  // After an unfollow moves the pane to the next record, carry keyboard focus
  // there rather than dropping it on the page.
  useEffect(() => {
    if (autoFocus && hasDetail) titleLinkRef.current?.focus()
  }, [autoFocus, hasDetail])

  const onUnfollow = async () => {
    const response = await unfollow(target)
    if ('error' in response) {
      messageApi.error(
        toApiError(response.error).detail ?? 'Could not stop following.',
      )
      return
    }
    messageApi.success(`You no longer follow ${detail?.record.name ?? 'this'}.`)
    onUnfollowed()
  }

  const back = asView ? (
    <div className={styles.backRow}>
      <Button icon={<ArrowLeftOutlined />} type="text" onClick={onBack}>
        Back to the list
      </Button>
    </div>
  ) : null

  if (isLoading) {
    return (
      <div className={styles.detailSection}>
        {back}
        <Skeleton active />
      </div>
    )
  }

  // The API answers 404 for a record that is no longer followed or readable;
  // anything else is a failure to load, not a record that has gone.
  const isGone =
    !isError || (error !== undefined && toApiError(error).status === 404)
  if (isError || !detail) {
    return (
      <div className={styles.detailSection}>
        {back}
        {isGone ? (
          <Alert
            type="info"
            title="This record is no longer in your list."
            description="You may have stopped following it, or you can no longer see it."
          />
        ) : (
          <Alert
            type="error"
            title="Could not load this record."
            action={
              <Button size="small" onClick={() => refetch()}>
                Try again
              </Button>
            }
          />
        )}
      </div>
    )
  }

  const record = detail.record
  const href = recordHref(record)

  return (
    <div className={asView ? styles.mobileDetail : undefined}>
      <div className={styles.detailSection}>
        {back}
        <div className={styles.detailHeader}>
          <Flex gap="small" align="center" wrap>
            <Tag className={styles.typeTag}>{typeName}</Tag>
            <Text type="secondary">
              {record.key}
              {record.parentName ? ` · ${record.parentName}` : ''}
            </Text>
          </Flex>
          {/* Beside the list, unfollowing is the same eye as on the record's
              own page. The phone view pins a full-width bar instead. */}
          {asView ? null : (
            <Tooltip title="Stop following" placement="topRight">
              <Button
                icon={<EyeFilled />}
                aria-label="Unfollow"
                loading={isUnfollowing}
                onClick={onUnfollow}
              />
            </Tooltip>
          )}
        </div>
        <Title level={4} style={{ margin: '4px 0 8px' }}>
          {href ? (
            <Link href={href} ref={titleLinkRef} className={styles.titleLink}>
              {record.name}
            </Link>
          ) : (
            record.name
          )}
        </Title>
        <Flex gap="small" align="center" wrap>
          <LifecycleStatusTag
            status={{
              id: 0,
              name: record.statusName,
              lifecycleCategory: record.lifecycle,
            }}
          />
          <FollowedRecordHealth record={record} variant="tag" />
          {record.isMuted ? (
            <Tooltip title="You still follow this, but are told nothing about it. Unmute it on its page, or under Notifications → Muted.">
              <Tag icon={<MutedOutlined />}>Muted</Tag>
            </Tooltip>
          ) : null}
        </Flex>
      </div>

      {ownerType === FollowOwnerTypes.Project ? (
        <div className={styles.detailSection}>
          <span className={styles.factLabel}>Stages</span>
          <ProjectStages projectKey={record.key} />
        </div>
      ) : typeof detail.progress === 'number' ? (
        <div className={styles.detailSection}>
          <span className={styles.factLabel}>Progress</span>
          <Progress percent={Math.round(detail.progress)} />
        </div>
      ) : null}

      <div className={`${styles.detailSection} ${styles.detailFacts}`}>
        {detail.people.length > 0 ? (
          roleGroups(detail.people).map(([role, people]) => (
            <Fact key={role} label={role}>
              <Flex vertical gap="small">
                {people.map((person) => (
                  <Flex
                    key={`${person.employeeId ?? ''}${person.name}`}
                    gap="small"
                    align="center"
                  >
                    <PersonAvatar name={person.name} size="small" />
                    <span>{person.name}</span>
                  </Flex>
                ))}
              </Flex>
            </Fact>
          ))
        ) : (
          <Fact label="Led by">
            <Text type="secondary">No one assigned</Text>
          </Fact>
        )}
        <Fact label="Dates">{dateRange(detail)}</Fact>
        <Fact label="Following since">
          {formatCalendarDate(toCalendarDate(dayjs(record.followedOn)))}
        </Fact>
        <Fact label="Last change">
          {record.lastChangedOn
            ? formatTimeAgo(record.lastChangedOn)
            : 'None recorded'}
        </Fact>
      </div>

      <div className={styles.detailSection}>
        <span className={styles.factLabel}>
          What has happened since you followed it
        </span>
        {detail.changesSinceFollowed.length > 0 ? (
          <ActivityLogTimeline
            activities={detail.changesSinceFollowed}
            isLoading={false}
            hasMore={false}
          />
        ) : (
          <Text type="secondary">Nothing yet.</Text>
        )}
        {detail.hasMoreChanges && href ? (
          <Link href={href}>See all activity</Link>
        ) : null}
      </div>

      {asView ? (
        <div className={styles.stickyActions}>
          {href ? (
            // A real link, so it opens in a new tab like any other; a plain
            // click still navigates within the app.
            <Button
              type="primary"
              block
              href={href}
              onClick={(e) => {
                if (e.metaKey || e.ctrlKey || e.shiftKey || e.button !== 0)
                  return
                e.preventDefault()
                router.push(href)
              }}
            >
              Open {typeName}
            </Button>
          ) : null}
          <Button danger block loading={isUnfollowing} onClick={onUnfollow}>
            Unfollow
          </Button>
        </div>
      ) : null}
    </div>
  )
}

export default FollowedRecordDetail
