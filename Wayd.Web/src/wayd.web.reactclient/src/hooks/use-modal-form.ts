'use client'

import { useEffect, useRef, useState } from 'react'
import { Form } from 'antd'
import type { FormInstance } from 'antd'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'

export interface UseModalFormOptions<TValues> {
  /**
   * Called when the user clicks OK and client-side form validation passes.
   * Perform the API mutation here. Return `true` to close the modal on
   * success, or `false` to keep it open (e.g. after applying server-side
   * validation errors to the form).
   *
   * The form instance is passed as the second argument so `onSubmit` can
   * call `form.setFields()` for server-side validation errors without
   * needing to reference the `form` variable before it is declared.
   */
  onSubmit: (values: TValues, form: FormInstance<TValues>) => Promise<boolean>

  /** Called after a successful submit to notify the parent. */
  onComplete: () => void

  /** Called when the user cancels the modal. */
  onCancel: () => void

  /** Fallback error message shown when an unhandled error occurs in onSubmit. */
  errorMessage?: string

  /**
   * Optional permission claim string (e.g. `'Permissions.Roadmaps.Create'`).
   * When provided, the hook checks the claim via `useAuth().hasPermissionClaim()`.
   * If denied, `onCancel` is called immediately and an error toast is shown —
   * the modal never becomes visible.
   */
  permission?: string
}

export interface UseModalFormReturn<TValues> {
  form: FormInstance<TValues>
  /** Whether the modal should be open. Use on `<Modal open={isOpen}>`. */
  isOpen: boolean
  isValid: boolean
  isSaving: boolean
  handleOk: () => Promise<void>
  handleCancel: () => void
}

/**
 * Manages boilerplate for Ant Design modal forms: form instance, permission
 * gating, validation state, saving state, and OK/Cancel handlers.
 *
 * Replaces the common pattern of `useState(isOpen)` + `useState(isSaving)` +
 * `useState(isValid)` + `Form.useWatch` + validation `useEffect` + permission
 * check `useEffect` that is repeated across 57+ form components.
 *
 * When `permission` is provided (a claim string like
 * `'Permissions.Roadmaps.Create'`), the hook checks the claim via
 * `useAuth().hasPermissionClaim()`. If denied, `onCancel` is called
 * immediately and an error toast is shown — the modal never becomes visible.
 */
function useModalForm<TValues = Record<string, unknown>>({
  onSubmit,
  onComplete,
  onCancel,
  errorMessage = 'An unexpected error occurred. Please try again.',
  permission,
}: UseModalFormOptions<TValues>): UseModalFormReturn<TValues> {
  const [form] = Form.useForm<TValues>()
  const [isValid, setIsValid] = useState(false)
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

  // Validate on every field value change using Form.useWatch.
  // This is the proven Ant Design pattern — useWatch triggers a re-render
  // when any field value changes, and the effect runs validateFields without
  // causing the infinite loop that onFieldsChange + validateFields creates.
  const formValues = Form.useWatch([], form)

  useEffect(() => {
    form
      .validateFields({ validateOnly: true })
      .then(() => setIsValid(true))
      .catch(() => setIsValid(false))
  }, [form, formValues])

  const handleOk = async () => {
    setIsSaving(true)
    try {
      const values = await form.validateFields()
      if (await onSubmit(values, form)) {
        form.resetFields()
        onComplete()
      }
    } catch (errorInfo: unknown) {
      // Ant Design's validateFields rejects with { errorFields: [...] } for
      // normal client-side validation failures — no need to toast for those.
      if (
        errorInfo &&
        typeof errorInfo === 'object' &&
        'errorFields' in errorInfo
      ) {
        return
      }
      console.error('handleOk error', errorInfo)
      messageApi.error(errorMessage)
    } finally {
      setIsSaving(false)
    }
  }

  const handleCancel = () => {
    form.resetFields()
    onCancel()
  }

  return {
    form,
    isOpen,
    isValid,
    isSaving,
    handleOk,
    handleCancel,
  }
}

export default useModalForm
