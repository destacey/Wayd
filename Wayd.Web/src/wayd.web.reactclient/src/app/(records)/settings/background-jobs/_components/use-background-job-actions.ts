'use client'

import { App } from 'antd'
import {
  useDeleteJobMutation,
  useRemoveRecurringJobMutation,
  useRequeueJobMutation,
} from '@/src/store/features/admin/background-jobs-api'
import { useMessage } from '@/src/components/contexts/messaging'

interface JobInfo {
  id: string
  action?: string | null
}

const useBackgroundJobActions = () => {
  const messageApi = useMessage()
  const { modal } = App.useApp()
  const [requeueJob] = useRequeueJobMutation()
  const [deleteJob] = useDeleteJobMutation()
  const [removeRecurringJob] = useRemoveRecurringJobMutation()

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

  return { handleRequeue, handleDelete, handleRemoveRecurring }
}

export default useBackgroundJobActions
