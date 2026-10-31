import { renderHook, act } from '@testing-library/react'
import useModalForm from './use-modal-form'

// Mock dependencies
const mockMessageSuccess = jest.fn()
const mockMessageError = jest.fn()
jest.mock('../components/contexts/messaging', () => ({
  useMessage: () => ({
    success: mockMessageSuccess,
    error: mockMessageError,
  }),
}))

const mockHasPermissionClaim = jest.fn()
jest.mock('../components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    hasPermissionClaim: mockHasPermissionClaim,
  }),
}))

// Mock Ant Design Form.useForm and Form.useWatch
const mockValidateFields = jest.fn()
const mockResetFields = jest.fn()
const mockSetFields = jest.fn()

jest.mock('antd', () => ({
  Form: {
    useForm: () => [
      {
        validateFields: mockValidateFields,
        resetFields: mockResetFields,
        setFields: mockSetFields,
      },
    ],
    useWatch: () => undefined,
  },
}))

interface TestFormValues {
  name: string
  value: number
}

const createDefaultOptions = (overrides = {}) => ({
  onSubmit: jest.fn().mockResolvedValue(true),
  onComplete: jest.fn(),
  onCancel: jest.fn(),
  ...overrides,
})

describe('useModalForm', () => {
  beforeEach(() => {
    mockHasPermissionClaim.mockReturnValue(true)
    // Default: validation fails (empty form)
    mockValidateFields.mockRejectedValue(new Error('Required'))
  })

  describe('initial state', () => {
    it('returns a form instance', () => {
      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions()),
      )

      expect(result.current.form).toBeDefined()
      expect(result.current.form.validateFields).toBeDefined()
      expect(result.current.form.resetFields).toBeDefined()
    })

    it('starts with isValid false', () => {
      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions()),
      )

      expect(result.current.isValid).toBe(false)
    })

    it('starts with isSaving false', () => {
      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions()),
      )

      expect(result.current.isSaving).toBe(false)
    })

    it('starts with isOpen true when no permission is provided', () => {
      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions()),
      )

      expect(result.current.isOpen).toBe(true)
    })

    it('returns handler functions', () => {
      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions()),
      )

      expect(typeof result.current.handleOk).toBe('function')
      expect(typeof result.current.handleCancel).toBe('function')
    })
  })

  describe('permission gate', () => {
    it('sets isOpen true when permission claim is granted', () => {
      mockHasPermissionClaim.mockReturnValue(true)

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(
          createDefaultOptions({ permission: 'Permissions.Test.Create' }),
        ),
      )

      expect(result.current.isOpen).toBe(true)
      expect(mockHasPermissionClaim).toHaveBeenCalledWith(
        'Permissions.Test.Create',
      )
    })

    it('sets isOpen false when permission claim is denied', () => {
      mockHasPermissionClaim.mockReturnValue(false)

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(
          createDefaultOptions({ permission: 'Permissions.Test.Create' }),
        ),
      )

      expect(result.current.isOpen).toBe(false)
    })

    it('calls onCancel when permission is denied', () => {
      mockHasPermissionClaim.mockReturnValue(false)
      const onCancel = jest.fn()

      renderHook(() =>
        useModalForm<TestFormValues>(
          createDefaultOptions({
            permission: 'Permissions.Test.Create',
            onCancel,
          }),
        ),
      )

      expect(onCancel).toHaveBeenCalled()
    })

    it('shows error toast when permission is denied', () => {
      mockHasPermissionClaim.mockReturnValue(false)

      renderHook(() =>
        useModalForm<TestFormValues>(
          createDefaultOptions({ permission: 'Permissions.Test.Create' }),
        ),
      )

      expect(mockMessageError).toHaveBeenCalledWith(
        'You do not have permission to perform this action.',
      )
    })

    it('only shows permission error once on re-renders', () => {
      mockHasPermissionClaim.mockReturnValue(false)
      const onCancel = jest.fn()

      const { rerender } = renderHook(() =>
        useModalForm<TestFormValues>(
          createDefaultOptions({
            permission: 'Permissions.Test.Create',
            onCancel,
          }),
        ),
      )

      rerender()
      rerender()

      expect(mockMessageError).toHaveBeenCalledTimes(1)
      expect(onCancel).toHaveBeenCalledTimes(1)
    })

    it('does not check permission when no permission string is provided', () => {
      const onCancel = jest.fn()

      renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions({ onCancel })),
      )

      expect(onCancel).not.toHaveBeenCalled()
      expect(mockMessageError).not.toHaveBeenCalled()
    })
  })

  describe('validation via useWatch', () => {
    it('sets isValid true when validation passes', async () => {
      mockValidateFields.mockResolvedValue({})

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions()),
      )

      // useEffect runs after render — wait for it
      await act(async () => {})

      expect(result.current.isValid).toBe(true)
    })

    it('sets isValid false when validation fails', async () => {
      mockValidateFields.mockRejectedValue(
        new Error('Validation failed'),
      )

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions()),
      )

      await act(async () => {})

      expect(result.current.isValid).toBe(false)
    })

    it('validates again when a run is rejected as out of date, rather than counting it as invalid', async () => {
      // Arrange — Ant Design rejects a run with outOfDate when another
      // validation started while it was in flight. Every run the effects start
      // is rejected that way, so only a retry can find the form valid.
      let effectsHaveRun = false
      mockValidateFields.mockImplementation(() =>
        effectsHaveRun
          ? Promise.resolve({})
          : Promise.reject({ errorFields: [], outOfDate: true }),
      )

      // Act
      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions()),
      )
      effectsHaveRun = true
      await act(async () => {})

      // Assert
      expect(result.current.isValid).toBe(true)
    })

    it('calls validateFields with validateOnly option', async () => {
      mockValidateFields.mockResolvedValue({})

      renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions()),
      )

      await act(async () => {})

      expect(mockValidateFields).toHaveBeenCalledWith({
        validateOnly: true,
      })
    })
  })

  describe('handleOk', () => {
    it('validates the form before submitting', async () => {
      const values = { name: 'test', value: 42 }
      mockValidateFields.mockResolvedValue(values)
      const onSubmit = jest.fn().mockResolvedValue(true)

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions({ onSubmit })),
      )

      await act(async () => {
        await result.current.handleOk()
      })

      expect(mockValidateFields).toHaveBeenCalled()
    })

    it('calls onSubmit with validated values and form instance', async () => {
      const values = { name: 'test', value: 42 }
      mockValidateFields.mockResolvedValue(values)
      const onSubmit = jest.fn().mockResolvedValue(true)

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions({ onSubmit })),
      )

      await act(async () => {
        await result.current.handleOk()
      })

      expect(onSubmit).toHaveBeenCalledWith(values, result.current.form)
    })

    it('resets form and calls onComplete when onSubmit returns true', async () => {
      mockValidateFields.mockResolvedValue({ name: 'test', value: 1 })
      const onSubmit = jest.fn().mockResolvedValue(true)
      const onComplete = jest.fn()

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(
          createDefaultOptions({ onSubmit, onComplete }),
        ),
      )

      await act(async () => {
        await result.current.handleOk()
      })

      expect(mockResetFields).toHaveBeenCalled()
      expect(onComplete).toHaveBeenCalled()
    })

    it('does not reset form or call onComplete when onSubmit returns false', async () => {
      mockValidateFields.mockResolvedValue({ name: 'test', value: 1 })
      const onSubmit = jest.fn().mockResolvedValue(false)
      const onComplete = jest.fn()

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(
          createDefaultOptions({ onSubmit, onComplete }),
        ),
      )

      await act(async () => {
        await result.current.handleOk()
      })

      expect(mockResetFields).not.toHaveBeenCalled()
      expect(onComplete).not.toHaveBeenCalled()
    })

    it('sets isSaving to true while submitting', async () => {
      let resolveFn: (value: boolean) => void
      const submitPromise = new Promise<boolean>((resolve) => {
        resolveFn = resolve
      })
      mockValidateFields.mockResolvedValue({ name: 'test', value: 1 })
      const onSubmit = jest.fn().mockReturnValue(submitPromise)

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions({ onSubmit })),
      )

      // Start the submit
      let okPromise: Promise<void>
      act(() => {
        okPromise = result.current.handleOk()
      })

      // isSaving should be true while the promise is pending
      expect(result.current.isSaving).toBe(true)

      // Resolve the submit
      await act(async () => {
        resolveFn!(true)
        await okPromise
      })

      expect(result.current.isSaving).toBe(false)
    })

    it('sets isSaving back to false after submit failure', async () => {
      jest.spyOn(console, 'error').mockImplementation()
      mockValidateFields.mockResolvedValue({ name: 'test', value: 1 })
      const onSubmit = jest.fn().mockRejectedValue(new Error('API error'))

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions({ onSubmit })),
      )

      await act(async () => {
        await result.current.handleOk()
      })

      expect(result.current.isSaving).toBe(false)
    })

    it('shows default error message when onSubmit throws', async () => {
      jest.spyOn(console, 'error').mockImplementation()
      mockValidateFields.mockResolvedValue({ name: 'test', value: 1 })
      const onSubmit = jest.fn().mockRejectedValue(new Error('API error'))

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions({ onSubmit })),
      )

      await act(async () => {
        await result.current.handleOk()
      })

      expect(mockMessageError).toHaveBeenCalledWith(
        'An unexpected error occurred. Please try again.',
      )
    })

    it('shows custom error message when provided and onSubmit throws', async () => {
      jest.spyOn(console, 'error').mockImplementation()
      mockValidateFields.mockResolvedValue({ name: 'test', value: 1 })
      const onSubmit = jest.fn().mockRejectedValue(new Error('API error'))

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(
          createDefaultOptions({
            onSubmit,
            errorMessage: 'Failed to save the item.',
          }),
        ),
      )

      await act(async () => {
        await result.current.handleOk()
      })

      expect(mockMessageError).toHaveBeenCalledWith(
        'Failed to save the item.',
      )
    })

    it('does not show error toast for client-side validation failures', async () => {
      mockValidateFields.mockRejectedValue({
        errorFields: [{ name: ['name'], errors: ['Required'] }],
      })

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions()),
      )

      await act(async () => {
        await result.current.handleOk()
      })

      expect(mockMessageError).not.toHaveBeenCalled()
    })

    it('sets isSaving back to false after client-side validation failure', async () => {
      mockValidateFields.mockRejectedValue({
        errorFields: [{ name: ['name'], errors: ['Required'] }],
      })

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions()),
      )

      await act(async () => {
        await result.current.handleOk()
      })

      expect(result.current.isSaving).toBe(false)
    })
  })

  describe('handleCancel', () => {
    it('resets the form', () => {
      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions()),
      )

      act(() => {
        result.current.handleCancel()
      })

      expect(mockResetFields).toHaveBeenCalled()
    })

    it('calls onCancel callback', () => {
      const onCancel = jest.fn()

      const { result } = renderHook(() =>
        useModalForm<TestFormValues>(createDefaultOptions({ onCancel })),
      )

      act(() => {
        result.current.handleCancel()
      })

      expect(onCancel).toHaveBeenCalled()
    })
  })
})
