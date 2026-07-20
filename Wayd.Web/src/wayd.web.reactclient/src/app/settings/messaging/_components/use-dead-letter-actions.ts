'use client'

import { App } from 'antd'
import {
  useDiscardDeadLettersMutation,
  useReplayDeadLettersMutation,
} from '@/src/store/features/admin/messaging-api'
import { useMessage } from '@/src/components/contexts/messaging'

interface DeadLetterInfo {
  id: string
  messageType: string
}

const useDeadLetterActions = () => {
  const messageApi = useMessage()
  const { modal } = App.useApp()
  const [replayDeadLetters] = useReplayDeadLettersMutation()
  const [discardDeadLetters] = useDiscardDeadLettersMutation()

  const handleReplay = async (
    deadLetter: DeadLetterInfo,
    onSuccess?: () => void,
  ) => {
    try {
      await replayDeadLetters([deadLetter.id]).unwrap()
      // Replay is asynchronous: the API marks the envelope replayable and the
      // durability agent moves it back to incoming on its next pass.
      messageApi.success('Message queued for replay.')
      onSuccess?.()
    } catch {
      messageApi.error('Failed to queue message for replay.')
    }
  }

  const handleDiscard = (deadLetter: DeadLetterInfo, onSuccess?: () => void) => {
    modal.confirm({
      title: 'Discard Dead Letter Message',
      content: `Are you sure you want to permanently delete this "${shortTypeName(deadLetter.messageType)}" message? This cannot be undone.`,
      okText: 'Discard',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await discardDeadLetters([deadLetter.id]).unwrap()
          messageApi.success('Message discarded.')
          onSuccess?.()
        } catch {
          messageApi.error('Failed to discard message.')
        }
      },
    })
  }

  return { handleReplay, handleDiscard }
}

/** Trims an assembly-qualified or namespaced .NET type name down to the class name. */
export const shortTypeName = (typeName?: string | null): string => {
  if (!typeName) return ''
  const withoutAssembly = typeName.split(',')[0].trim()
  const segments = withoutAssembly.split('.')
  return segments[segments.length - 1]
}

export default useDeadLetterActions
