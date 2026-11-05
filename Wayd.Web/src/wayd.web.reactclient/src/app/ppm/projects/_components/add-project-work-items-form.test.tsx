import React from 'react'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { WorkTypeTier } from '@/src/services/wayd-api'
import AddProjectWorkItemsForm from './add-project-work-items-form'

const mockError = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockError, success: jest.fn() }),
}))

// In Jest, Form.useWatch does not refire after typing, so the real hook's validity never updates; as the
// other form suites do, keep the form instance real and submit through validateFields.
jest.mock('@/src/hooks', () => {
  const { Form } = jest.requireActual('antd')
  return {
    // The search waits for typing to settle; the tests search at once.
    useDebounce: (value: unknown) => value,
    useModalForm: ({
      onSubmit,
      onComplete,
    }: {
      onSubmit: (
        values: Record<string, unknown>,
        form: unknown,
      ) => Promise<boolean>
      onComplete: () => void
    }) => {
      const [form] = Form.useForm()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => {
          try {
            if (await onSubmit(await form.validateFields(), form)) onComplete()
          } catch {
            // A validation failure leaves the form open, as the real hook does.
          }
        },
        handleCancel: jest.fn(),
      }
    },
  }
})

const mockAdd = jest.fn()
jest.mock('@/src/store/features/ppm/projects-api', () => ({
  useAddProjectWorkItemsMutation: () => [mockAdd],
}))

const mockSearchWorkItems = jest.fn()
jest.mock('@/src/store/features/work-management/workspace-api', () => ({
  useSearchWorkItemsQuery: (arg: unknown, options: { skip: boolean }) =>
    mockSearchWorkItems(arg, options),
}))

const epic = (id: string, key: string, title: string, project?: string) => ({
  id,
  key,
  title,
  type: { name: 'Epic' },
  project: project ? { id: project, name: `Project ${project}` } : undefined,
})

const renderForm = (onFormComplete = jest.fn()) =>
  act(async () =>
    render(
      <AddProjectWorkItemsForm
        projectId="p-1"
        existingWorkItemIds={new Set(['w-1'])}
        onFormComplete={onFormComplete}
        onFormCancel={jest.fn()}
      />,
    ),
  )

const searchWorkItems = async (term: string) => {
  const select = screen
    .getByText('Work Items')
    .closest('.ant-form-item')!
    .querySelector('input')!
  await act(() => userEvent.click(select))
  await act(() => userEvent.clear(select))
  await act(() => userEvent.type(select, term))
}

describe('AddProjectWorkItemsForm', () => {
  beforeEach(() => {
    mockAdd.mockReset()
    mockError.mockReset()
    mockSearchWorkItems.mockReset()
    mockSearchWorkItems.mockImplementation((arg: { query: string }) => ({
      data:
        arg.query === ''
          ? undefined
          : arg.query === 'zzz'
            ? [epic('w-4', 'APP-4', 'Unrelated')]
            : [
                epic('w-1', 'APP-1', 'Already here', 'p-1'),
                epic('w-2', 'APP-2', 'Checkout'),
                epic('w-3', 'APP-3', 'Payments', 'p-9'),
              ],
      isFetching: false,
      isError: false,
    }))
  })

  it('searches only portfolio work items, once something is typed, leaving out ones already on the project', async () => {
    // Arrange
    await renderForm()

    // Act
    await searchWorkItems('app')

    // Assert
    expect(mockSearchWorkItems).toHaveBeenCalledWith(
      { query: '', tier: WorkTypeTier.Portfolio },
      { skip: true },
    )
    expect(mockSearchWorkItems).toHaveBeenCalledWith(
      { query: 'app', tier: WorkTypeTier.Portfolio },
      { skip: false },
    )
    expect(screen.getByTitle('APP-2 · Checkout')).toBeInTheDocument()
    expect(screen.queryByTitle('APP-1 · Already here')).toBeNull()
  })

  it('adds every chosen work item to the project', async () => {
    // Arrange
    mockAdd.mockReturnValue({ unwrap: () => Promise.resolve(null) })
    const onFormComplete = jest.fn()
    await renderForm(onFormComplete)
    await searchWorkItems('app')
    await act(() => userEvent.click(screen.getByTitle('APP-2 · Checkout')))
    await act(() => userEvent.click(screen.getByTitle('APP-3 · Payments')))

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Add' })),
    )

    // Assert
    expect(mockAdd).toHaveBeenCalledWith({
      projectId: 'p-1',
      workItemIds: ['w-2', 'w-3'],
    })
    expect(onFormComplete).toHaveBeenCalled()
  })

  it('warns that a work item on another project will move', async () => {
    // Arrange
    await renderForm()
    await searchWorkItems('app')

    // Act
    await act(() => userEvent.click(screen.getByTitle('APP-3 · Payments')))

    // Assert
    expect(
      screen.getByText(
        'These work items will move from their current project:',
      ),
    ).toBeInTheDocument()
    expect(screen.getByText('APP-3 · Project p-9')).toBeInTheDocument()
  })

  it('does not warn for a work item with no project', async () => {
    // Arrange
    await renderForm()
    await searchWorkItems('app')

    // Act
    await act(() => userEvent.click(screen.getByTitle('APP-2 · Checkout')))

    // Assert
    expect(
      screen.queryByText(
        'These work items will move from their current project:',
      ),
    ).toBeNull()
  })

  it('still warns about a chosen work item after a later search drops it', async () => {
    // Arrange
    await renderForm()
    await searchWorkItems('app')
    await act(() => userEvent.click(screen.getByTitle('APP-3 · Payments')))

    // Act
    await searchWorkItems('zzz')

    // Assert
    expect(screen.getByTitle('APP-4 · Unrelated')).toBeInTheDocument()
    expect(screen.getByText('APP-3 · Project p-9')).toBeInTheDocument()
  })

  it('shows the API error and stays open when the add is refused', async () => {
    // Arrange
    mockAdd.mockReturnValue({
      unwrap: () =>
        Promise.reject({ status: 400, detail: 'Project not found.' }),
    })
    const onFormComplete = jest.fn()
    await renderForm(onFormComplete)
    await searchWorkItems('app')
    await act(() => userEvent.click(screen.getByTitle('APP-2 · Checkout')))

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Add' })),
    )

    // Assert
    expect(mockError).toHaveBeenCalledWith('Project not found.')
    expect(onFormComplete).not.toHaveBeenCalled()
  })
})
