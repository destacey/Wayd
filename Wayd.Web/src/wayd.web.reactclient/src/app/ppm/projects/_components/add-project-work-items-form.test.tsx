import React from 'react'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { WorkTypeTier } from '@/src/services/wayd-api'
import AddProjectWorkItemsForm from './add-project-work-items-form'
import type { UseModalFormOptions } from '@/src/hooks'

const mockError = vi.fn()
vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockError, success: vi.fn() }),
}))

// Under jsdom, Form.useWatch does not refire after typing, so the real hook's validity never updates; as the
// other form suites do, keep the form instance real and submit through validateFields.
vi.mock(import('@/src/hooks'), async () => {
  const { Form } = await vi.importActual<typeof import('antd')>('antd')
  return {
    // The search waits for typing to settle; the tests search at once.
    useDebounce: <T,>(value: T) => value,
    useModalForm: <TValues,>({
      onSubmit,
      onComplete,
    }: UseModalFormOptions<TValues>) => {
      const [form] = Form.useForm<TValues>()
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
        handleCancel: vi.fn(),
      }
    },
  }
})

const mockAdd = vi.fn()
vi.mock('@/src/store/features/ppm/projects-api', () => ({
  useAddProjectWorkItemsMutation: () => [mockAdd],
}))

const mockSearchWorkItems = vi.fn()
vi.mock('@/src/store/features/work-management/workspace-api', () => ({
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

const renderForm = (onFormComplete = vi.fn()) =>
  act(async () =>
    render(
      <AddProjectWorkItemsForm
        projectId="p-1"
        existingWorkItemIds={new Set(['w-1'])}
        onFormComplete={onFormComplete}
        onFormCancel={vi.fn()}
      />,
    ),
  )

const searchWorkItems = async (term: string) => {
  const select = screen
    .getByText('Work Items')
    .closest('.ant-form-item')!
    .querySelector('input')!
  await userEvent.click(select)
  await userEvent.clear(select)
  await userEvent.type(select, term)
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
    const onFormComplete = vi.fn()
    await renderForm(onFormComplete)
    await searchWorkItems('app')
    await userEvent.click(screen.getByTitle('APP-2 · Checkout'))
    await userEvent.click(screen.getByTitle('APP-3 · Payments'))

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))

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
    await userEvent.click(screen.getByTitle('APP-3 · Payments'))

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
    await userEvent.click(screen.getByTitle('APP-2 · Checkout'))

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
    await userEvent.click(screen.getByTitle('APP-3 · Payments'))

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
    const onFormComplete = vi.fn()
    await renderForm(onFormComplete)
    await searchWorkItems('app')
    await userEvent.click(screen.getByTitle('APP-2 · Checkout'))

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Add' }))

    // Assert
    expect(mockError).toHaveBeenCalledWith('Project not found.')
    expect(onFormComplete).not.toHaveBeenCalled()
  })
})
