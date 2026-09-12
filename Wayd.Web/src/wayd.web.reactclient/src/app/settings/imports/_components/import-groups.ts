import {
  ImportAtomicity,
  ImportProcessDto,
  ImportProcessStatus,
} from '@/src/services/wayd-api'
import { isRunning } from './import-status-tag'

/**
 * A row of the imports list: a run, or the rollup of the runs submitted together under one group.
 *
 * A rollup borrows the run's shape so the grid's columns read it the same way — the counts are sums,
 * the status is the batch's, the timing spans the batch. `children` are the runs it stands for, and
 * `id` is the group id, which no run shares.
 */
export interface ImportListRow extends ImportProcessDto {
  children?: ImportListRow[]
  isGroup?: boolean
}

/** The one line a rollup shows in the Import column. */
export const groupLabel = (count: number) =>
  `${count} imports submitted together`

/**
 * The batch's status, from its runs'. Still going if any run is; otherwise good only if every run
 * was, partial if anything applied and anything did not, and failed if nothing at all landed —
 * the same reading `Complete` gives one run from its own counts.
 */
const rollupStatus = (runs: ImportProcessDto[]): ImportProcessStatus => {
  if (runs.some((r) => r.status === ImportProcessStatus.Cancelling))
    return ImportProcessStatus.Cancelling
  if (runs.some((r) => r.status === ImportProcessStatus.Processing))
    return ImportProcessStatus.Processing
  if (runs.some((r) => isRunning(r.status))) return ImportProcessStatus.Queued
  if (runs.every((r) => r.status === ImportProcessStatus.Succeeded))
    return ImportProcessStatus.Succeeded
  if (runs.every((r) => r.status === ImportProcessStatus.Cancelled))
    return ImportProcessStatus.Cancelled

  const applied = runs.reduce((n, r) => n + r.succeededRowCount, 0)
  return applied > 0
    ? ImportProcessStatus.PartiallySucceeded
    : ImportProcessStatus.Failed
}

// The DTO types these as Date, but the generated client hands the JSON through untouched, so at
// runtime they are ISO strings. Both parse the same way; the picked value is passed on as it came,
// which is what the grid's date column already formats.
const byTime = (a: Date, b: Date) =>
  new Date(a).getTime() - new Date(b).getTime()

const earliest = (values: (Date | undefined)[]) =>
  values.filter((v): v is Date => !!v).sort(byTime)[0]

const latest = (values: (Date | undefined)[]) =>
  values
    .filter((v): v is Date => !!v)
    .sort(byTime)
    .at(-1)

const rollup = (groupId: string, runs: ImportProcessDto[]): ImportListRow => {
  const status = rollupStatus(runs)
  const totalRowCount = runs.reduce((n, r) => n + r.totalRowCount, 0)
  const succeededRowCount = runs.reduce((n, r) => n + r.succeededRowCount, 0)
  const failedRowCount = runs.reduce((n, r) => n + r.failedRowCount, 0)
  const allFinished = runs.every((r) => r.isTerminal)

  return {
    // Namespaced: the group id is the caller's to choose, so nothing stops it from equalling a
    // run's id, and two rows with one key would corrupt expansion and selection.
    id: `group:${groupId}`,
    importType: 'group',
    displayName: groupLabel(runs.length),
    atomicity: ImportAtomicity.PerRow,
    status,
    submissionGroupId: groupId,
    // Files submitted together are submitted by one caller; the first run's is the batch's.
    submittedByUserId: runs[0].submittedByUserId,
    submittedByName: runs[0].submittedByName,
    // The newest file's, so the batch sorts where that file would in a newest-first list rather
    // than sinking behind runs submitted after its first file but before its last.
    submittedOn: latest(runs.map((r) => r.submittedOn))!,
    startedOn: earliest(runs.map((r) => r.startedOn)),
    // A batch has finished only once its last run has; until then it has no finish to show.
    completedOn: allFinished ? latest(runs.map((r) => r.completedOn)) : undefined,
    lastProgressOn: latest(runs.map((r) => r.lastProgressOn)),
    totalRowCount,
    succeededRowCount,
    failedRowCount,
    error: undefined,
    // Stop, resume and retry act on one run; the rollup offers nothing and the runs beneath it do.
    canManage: false,
    unappliedRowCount: totalRowCount - succeededRowCount - failedRowCount,
    isTerminal: allFinished,
    children: runs,
    isGroup: true,
  }
}

/**
 * Folds a page of runs into the list's rows: one rollup per submission group, holding its runs,
 * and every ungrouped run on its own.
 *
 * Rollups keep the position of their group's newest run, so the list still reads newest first
 * before any sort is applied. A group of one is left as a plain run — a rollup of one thing says
 * nothing the thing does not.
 */
export const buildImportRows = (
  processes: ImportProcessDto[] | undefined,
): ImportListRow[] => {
  if (!processes) return []

  const byGroup = new Map<string, ImportProcessDto[]>()
  for (const process of processes) {
    if (!process.submissionGroupId) continue
    const runs = byGroup.get(process.submissionGroupId) ?? []
    runs.push(process)
    byGroup.set(process.submissionGroupId, runs)
  }

  const placed = new Set<string>()
  const rows: ImportListRow[] = []

  for (const process of processes) {
    const groupId = process.submissionGroupId
    const runs = groupId ? byGroup.get(groupId) : undefined

    if (!groupId || !runs || runs.length < 2) {
      rows.push(process)
      continue
    }

    if (placed.has(groupId)) continue
    placed.add(groupId)
    rows.push(rollup(groupId, runs))
  }

  return rows
}
