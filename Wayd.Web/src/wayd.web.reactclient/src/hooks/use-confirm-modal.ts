'use client'

import { useEffect, useRef, useState } from 'react'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'

export interface UseConfirmModalOptions {
  /**
   * Called when the user clicks OK. Perform the mutation here.
   * Return `true` to close the modal on success, or `false` to keep it open.
   */
  onSubmit: () => Promise<boolean>

  /** Called after a successful submit to notify the parent. */
  onComplete: () => void

  /** Called when the user cancels the modal. */
  onCancel: () => void

  /** Fallback error message shown when an unhandled error occurs in onSubmit. */
  errorMessage?: string

  /**
   * Optional permission claim string (e.g. `'Permissions.Roadmaps.Delete'`).
   * When provided, the hook checks the claim via `useAuth().hasPermissionClaim()`.
   * If denied, `onCancel` is called immediately and an error toast is shown —
   * the modal never becomes visible.
   */
  permission?: string
}

export interface UseConfirmModalReturn {
  /** Whether the modal should be open. Use on `<Modal open={isOpen}>`. */
  isOpen: boolean
  isSaving: boolean
  handleOk: () => Promise<void>
  handleCancel: () => void
}

/**
 * Manages boilerplate for confirmation/delete modals that have no form fields:
 * permission gating, saving state, and OK/Cancel handlers.
 *
 * For modals with Ant Design Form fields, use `useModalForm` instead.
 */
function useConfirmModal({
  onSubmit,
  onComplete,
  onCancel,
  errorMessage = 'An unexpected error occurred. Please try again.',
  permission,
}: UseConfirmModalOptions): UseConfirmModalReturn {
  const [isSaving, setIsSaving] = useState(false)
  const messageApi = useMessage()

  // Permission gate
  const { hasPermissionClaim } = useAuth()
  const permissionAllowed = permission
    ? hasPermissionClaim(permission)
    : true
  const permissionDeniedRef = useRef(false)
  const isOpen = permissionAllowed

  useEffect(() => {
    if (!permission || permissionAllowed || permissionDeniedRef.current)
      return
    permissionDeniedRef.current = true
    messageApi.error(
      'You do not have permission to perform this action.',
    )
    onCancel()
  }, [permission, permissionAllowed, messageApi, onCancel])

  const handleOk = async () => {
    setIsSaving(true)
    try {
      if (await onSubmit()) {
        onComplete()
      }
    } catch (error) {
      console.error('handleOk error', error)
      messageApi.error(errorMessage)
    } finally {
      setIsSaving(false)
    }
  }

  const handleCancel = () => {
    onCancel()
  }

  return {
    isOpen,
    isSaving,
    handleOk,
    handleCancel,
  }
}

export default useConfirmModal
