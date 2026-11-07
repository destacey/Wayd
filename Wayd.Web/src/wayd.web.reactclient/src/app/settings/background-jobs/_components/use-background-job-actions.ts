'use client'

import { App } from 'antd'
import {
  BulkJobOutcome,
  useDeleteJobMutation,
  useDeleteJobsMutation,
  useRemoveRecurringJobMutation,
  useRequeueJobMutation,
  useRequeueJobsMutation,
} from '@/src/store/features/admin/background-jobs-api'
import { useMessage } from '@/src/components/contexts/messaging'

interface JobInfo {
  id: string
  action?: string | null
}

const jobCount = (count: number) => `${count} job${count === 1 ? '' : 's'}`

/**
 * The one message a bulk action reports: everything done, or how many of the
 * batch could not be acted on and the likeliest reason.
 */
export const describeBulkOutcome = (
  verb: string,
  { total, failed }: BulkJobOutcome,
): { ok: boolean; text: string } =>
  failed === 0
    ? { ok: true, text: `${verb} ${jobCount(total)}.` }
    : {
        ok: false,
        text: `${verb} ${total - failed} of ${jobCount(total)}; ${failed} failed. They may have changed state — refresh and try again.`,
      }

const useBackgroundJobActions = () => {
  const messageApi = useMessage()
  const { modal } = App.useApp()
  const [requeueJob] = useRequeueJobMutation()
  const [deleteJob] = useDeleteJobMutation()
  const [removeRecurringJob] = useRemoveRecurringJobMutation()
  const [requeueJobs] = useRequeueJobsMutation()
  const [deleteJobs] = useDeleteJobsMutation()

  const reportBulk = (verb: string, outcome: BulkJobOutcome) => {
    const { ok, text } = describeBulkOutcome(verb, outcome)
    if (ok) messageApi.success(text)
    else messageApi.error(text)
  }

  const handleRequeue = async (job: JobInfo, onSuccess?: () => void) => {
    try {
      await requeueJob(job.id).unwrap()
      messageApi.success('Job queued for another attempt.')
      onSuccess?.()
    } catch {
      messageApi.error('Failed to requeue the job.')
    }
  }

  const handleDelete = (job: JobInfo, onSuccess?: () => void) => {
    modal.confirm({
      title: 'Delete Job',
      content: `Are you sure you want to delete this "${job.action ?? job.id}" job?`,
      okText: 'Delete',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await deleteJob(job.id).unwrap()
          messageApi.success('Job deleted.')
          onSuccess?.()
        } catch {
          messageApi.error('Failed to delete the job.')
        }
      },
    })
  }

  const handleRemoveRecurring = (
    recurringJobId: string,
    onSuccess?: () => void,
  ) => {
    modal.confirm({
      title: 'Remove Recurring Job',
      content: `Are you sure you want to remove the "${recurringJobId}" schedule? Jobs it already created are unaffected.`,
      okText: 'Remove',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await removeRecurringJob(recurringJobId).unwrap()
          messageApi.success('Recurring job removed.')
          onSuccess?.()
        } catch {
          messageApi.error('Failed to remove the recurring job.')
        }
      },
    })
  }

  /**
   * Requeues the jobs and reports the batch in one message. Not confirmed,
   * matching the single-job requeue. Resolves once the batch has finished.
   */
  const handleBulkRequeue = async (jobs: JobInfo[]) => {
    const outcome = await requeueJobs(jobs.map((job) => job.id)).unwrap()
    reportBulk('Requeued', outcome)
  }

  /** Resolves true once the jobs were deleted, false when the confirmation was dismissed. */
  const handleBulkDelete = (jobs: JobInfo[]) =>
    new Promise<boolean>((resolve) => {
      modal.confirm({
        title: 'Delete Jobs',
        content: `Are you sure you want to delete ${jobCount(jobs.length)}?`,
        okText: 'Delete',
        okButtonProps: { danger: true },
        onOk: async () => {
          const outcome = await deleteJobs(jobs.map((job) => job.id)).unwrap()
          reportBulk('Deleted', outcome)
          resolve(true)
        },
        onCancel: () => resolve(false),
      })
    })

  return {
    handleRequeue,
    handleDelete,
    handleRemoveRecurring,
    handleBulkRequeue,
    handleBulkDelete,
  }
}

export default useBackgroundJobActions
