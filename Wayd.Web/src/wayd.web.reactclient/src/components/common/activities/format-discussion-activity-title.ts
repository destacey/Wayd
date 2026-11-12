import {
  ActivityLogDto,
  IncidentTimelineLinkAction,
  IncidentTimelineLinkKind,
} from '@/src/services/wayd-api'
import { formatEventTitle } from './activity-log-timeline'

type LinkRecordedPayload = {
  kind?: IncidentTimelineLinkKind | string
  action?: IncidentTimelineLinkAction | string
  label?: string
  fromLabel?: string
  toLabel?: string
}

const linkTargetLabel = (kind: LinkRecordedPayload['kind']) =>
  kind === IncidentTimelineLinkKind.WorkItem ||
  kind === 'workItem' ||
  kind === 'WorkItem'
    ? 'work item'
    : 'link'

/** Activity line for discussion feeds; enriches incident link events from payload. */
export const formatDiscussionActivityTitle = (
  activity: ActivityLogDto,
): string => {
  if (activity.eventType !== 'IncidentTimelineLinkRecordedEvent') {
    return formatEventTitle(activity.eventType, activity.summary)
  }

  try {
    const payload = JSON.parse(activity.payload) as LinkRecordedPayload
    const target = linkTargetLabel(payload.kind)
    const action = payload.action

    if (
      action === IncidentTimelineLinkAction.Added ||
      action === 'added' ||
      action === 'Added'
    ) {
      return `Linked ${target}: ${payload.label ?? ''}`.trim()
    }

    if (
      action === IncidentTimelineLinkAction.Removed ||
      action === 'removed' ||
      action === 'Removed'
    ) {
      return `Removed ${target}: ${payload.label ?? ''}`.trim()
    }

    if (
      action === IncidentTimelineLinkAction.Updated ||
      action === 'updated' ||
      action === 'Updated'
    ) {
      if (payload.fromLabel && payload.toLabel) {
        return `Updated ${target}: ${payload.fromLabel} → ${payload.toLabel}`
      }

      return `Updated ${target}: ${payload.label ?? ''}`.trim()
    }
  } catch {
    // Fall back to summary when payload is missing or malformed.
  }

  return formatEventTitle(activity.eventType, activity.summary)
}
