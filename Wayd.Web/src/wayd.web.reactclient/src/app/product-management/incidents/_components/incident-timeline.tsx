'use client'

import {
  coerceIncidentInstant,
  instantToEpochMs,
} from '@/src/app/product-management/incidents/_components/incident-format'
import { IncidentSeverityRank } from '@/src/app/product-management/incidents/_components/incident-severity-rank'
import { useMessage } from '@/src/components/contexts/messaging'
import { IncidentDto, IncidentTimelineNoteDto } from '@/src/services/wayd-api'
import {
  useAddIncidentTimelineNoteMutation,
  useGetIncidentTimelineNotesQuery,
} from '@/src/store/features/product-management/incidents-api'
import { formatInstantInZone, formatInstantTimeInZone } from '@/src/utils'
import { Button, Input } from 'antd'
import { useMemo, useState } from 'react'
import styles from '@/src/app/product-management/incidents/_components/incidents.module.css'

const { TextArea } = Input

type MilestoneKind = 'noticed' | 'started' | 'recovered'

type TimelineMilestoneNode = {
  kind: 'milestone'
  id: string
  milestone: MilestoneKind
  label: string
  at: Date
}

type TimelineNoteNode = {
  kind: 'note'
  id: string
  at: Date
  body: string
  authorName: string
}

export type TimelineNode = TimelineMilestoneNode | TimelineNoteNode

const buildTimelineNodes = (
  incident: IncidentDto,
  notes: IncidentTimelineNoteDto[],
): TimelineNode[] => {
  const milestones: TimelineMilestoneNode[] = [
    {
      kind: 'milestone',
      id: 'noticed',
      milestone: 'noticed',
      label: 'Noticed',
      at: coerceIncidentInstant(incident.noticedAt),
    },
  ]

  if (incident.startedAt) {
    milestones.push({
      kind: 'milestone',
      id: 'started',
      milestone: 'started',
      label: 'Response started',
      at: coerceIncidentInstant(incident.startedAt),
    })
  }

  if (incident.recoveredAt) {
    milestones.push({
      kind: 'milestone',
      id: 'recovered',
      milestone: 'recovered',
      label: 'Recovered',
      at: coerceIncidentInstant(incident.recoveredAt),
    })
  }

  const noteNodes: TimelineNoteNode[] = notes.map((note) => ({
    kind: 'note',
    id: note.id,
    at: coerceIncidentInstant(note.recordedAt),
    body: note.body,
    authorName: note.author.name,
  }))

  return [...milestones, ...noteNodes].sort(
    (a, b) => instantToEpochMs(a.at) - instantToEpochMs(b.at),
  )
}

export interface IncidentTimelineProps {
  incident: IncidentDto
  idOrKey: string
  productMeta: string | null
  viewerZone: string
  canAddNotes: boolean
}

const IncidentTimeline = ({
  incident,
  idOrKey,
  productMeta,
  viewerZone,
  canAddNotes,
}: IncidentTimelineProps) => {
  const messageApi = useMessage()
  const [draft, setDraft] = useState('')
  const { data: notes = [], refetch } = useGetIncidentTimelineNotesQuery({
    idOrKey,
    incidentId: incident.id,
  })
  const [addNote, { isLoading: posting }] = useAddIncidentTimelineNoteMutation()

  const nodes = useMemo(
    () => buildTimelineNodes(incident, notes),
    [incident, notes],
  )

  const postNote = async () => {
    const body = draft.trim()
    if (!body) {
      return
    }

    try {
      await addNote({ incidentId: incident.id, body }).unwrap()
      setDraft('')
      messageApi.success('Timeline updated.')
      refetch()
    } catch {
      messageApi.error('Failed to add timeline note.')
    }
  }

  return (
    <div className={styles.timelinePane}>
      <div className={styles.timelineHeader}>
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
      </div>
      {nodes.map((node) =>
        node.kind === 'milestone' ? (
          <div key={node.id} className={styles.timelineRow}>
            <div className={styles.timelineRailCell}>
              <time className={styles.timelineRailTime}>
                {formatInstantTimeInZone(node.at, viewerZone)}
              </time>
              <span className={styles.timelineDotMilestone} />
            </div>
            <div className={styles.timelineEvent}>
              <div className={styles.timelineEventHead}>
                <strong>{node.label}</strong>
                <time>{formatInstantInZone(node.at, viewerZone)}</time>
              </div>
            </div>
          </div>
        ) : (
          <div key={node.id} className={styles.timelineRow}>
            <div className={styles.timelineRailCell}>
              <time className={styles.timelineRailTime}>
                {formatInstantTimeInZone(node.at, viewerZone)}
              </time>
              <span className={styles.timelineDotNote} />
            </div>
            <div className={styles.timelineEvent}>
              <div className={styles.timelineEventHead}>
                <strong>{node.authorName}</strong>
                <time>{formatInstantInZone(node.at, viewerZone)}</time>
              </div>
              <div>{node.body}</div>
            </div>
          </div>
        ),
      )}
      {canAddNotes ? (
        <form
          className={styles.timelineComposer}
          onSubmit={(event) => {
            event.preventDefault()
            void postNote()
          }}
        >
          <TextArea
            rows={2}
            value={draft}
            onChange={(event) => setDraft(event.target.value)}
            placeholder="Add to the timeline"
            aria-label="Add to the timeline"
            maxLength={4096}
          />
          <div className={styles.timelineComposerActions}>
            <Button
              type="primary"
              htmlType="submit"
              loading={posting}
              disabled={!draft.trim()}
            >
              Post
            </Button>
          </div>
        </form>
      ) : null}
    </div>
  )
}

export default IncidentTimeline
