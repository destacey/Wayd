import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  StatusCategory,
  StatusCategoryLabelDto,
  WorkflowAliasDto,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'
import EditWorkflowStatusForm, {
  type WorkflowStatusEditScope,
} from './edit-workflow-status-form'
import type { UseModalFormOptions } from '@/src/hooks'

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

// Not spread from the real barrel: it re-exports store-bound hooks. The stand-in
// keeps the hook's contract — a form that is open and submits its values.
vi.mock(import('@/src/hooks'), async () => {
  const { Form } = await vi.importActual<typeof import('antd')>('antd')
  return {
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
          const values = await form.validateFields().catch(() => null)
          if (values && (await onSubmit(values, form))) onComplete()
        },
        handleCancel: vi.fn(),
      }
    },
  }
})

const mockRename = vi.fn()
const mockReclassify = vi.fn()
const mockDescribe = vi.fn()
vi.mock('@/src/store/features/common/status-workflows-api', () => ({
  useRenameWorkflowStatusMutation: () => [mockRename],
  useReclassifyWorkflowStatusMutation: () => [mockReclassify],
  useDescribeWorkflowStatusMutation: () => [mockDescribe],
}))

const ALIASES: WorkflowAliasDto[] = [
  {
    value: 2,
    name: 'Released',
    isRequired: true,
    category: StatusCategory.Done,
  },
  {
    value: 3,
    name: 'Ready',
    isRequired: true,
    category: StatusCategory.Active,
  },
]

// The server enum's numbers, which a status's category navigation carries as its id.
const CATEGORY_IDS: Record<StatusCategory, number> = {
  [StatusCategory.Proposed]: 0,
  [StatusCategory.Active]: 1,
  [StatusCategory.Done]: 2,
  [StatusCategory.Removed]: 3,
}

const statusIn = (
  category: StatusCategory,
  alias: number,
  categoryName: string = category,
): WorkflowStatusDto => ({
  id: 's-1',
  name: 'Released',
  category: { id: CATEGORY_IDS[category], name: categoryName },
  alias,
  order: 1,
  isRetired: false,
})

// An owner type that names two categories its own way.
const LABELS: StatusCategoryLabelDto[] = [
  { category: StatusCategory.Proposed, label: 'Proposed' },
  { category: StatusCategory.Active, label: 'Underway' },
  { category: StatusCategory.Done, label: 'Concluded' },
  { category: StatusCategory.Removed, label: 'Removed' },
]

const renderForm = async (
  status: WorkflowStatusDto,
  categoryLabels?: StatusCategoryLabelDto[],
  scope?: WorkflowStatusEditScope,
  onFormComplete: () => void = vi.fn(),
) =>
  await act(async () =>
    render(
      <EditWorkflowStatusForm
        workflowId="w-1"
        status={status}
        statuses={[status]}
        aliases={ALIASES}
        categoryLabels={categoryLabels}
        scope={scope}
        onFormComplete={onFormComplete}
        onFormCancel={vi.fn()}
      />,
    ),
  )

const chooseCategory = async (category: string) => {
  const field = screen.getByLabelText('Category')
  await userEvent.click(field)
  const options = await screen.findAllByTitle(category)
  await userEvent.click(options[options.length - 1])
}

const categoryShown = () =>
  within(
    screen.getByLabelText('Category').closest('.ant-select') as HTMLElement,
  )
    .getByTitle(/./)
    .getAttribute('title')

const meaningShown = () =>
  within(screen.getByLabelText('Meaning').closest('.ant-select') as HTMLElement)
    .getByTitle(/./)
    .getAttribute('title')

describe('EditWorkflowStatusForm', () => {
  beforeEach(() => {
    mockRename.mockReset()
    mockReclassify.mockReset()
    mockDescribe.mockReset()
  })

  it('opens showing the meaning the status holds', async () => {
    // Arrange / Act
    await renderForm(statusIn(StatusCategory.Done, 2))

    // Assert
    expect(meaningShown()).toBe('Released (required)')
  })

  it('drops the meaning on moving to a category it does not belong to, and brings it back on returning', async () => {
    // Arrange
    await renderForm(statusIn(StatusCategory.Done, 2))

    // Act
    await chooseCategory(StatusCategory.Active)
    const afterMove = meaningShown()
    await chooseCategory(StatusCategory.Done)

    // Assert
    expect(afterMove).toBe('None')
    expect(meaningShown()).toBe('Released (required)')
  })

  it('names a meaning the owner type no longer has and says to clear it', async () => {
    // Arrange / Act — a status cloned from a workflow whose meaning was retired
    await renderForm(statusIn(StatusCategory.Proposed, 5))

    // Assert
    expect(meaningShown()).toBe('Unknown meaning (5)')
    expect(
      screen.getByText(
        'This meaning no longer exists. Choose None or another meaning.',
      ),
    ).toBeInTheDocument()
  })

  it('names a meaning filed under the wrong category and warns about it', async () => {
    // Arrange / Act — a status cloned from a workflow saved before aliases were
    // tied to categories
    await renderForm(statusIn(StatusCategory.Active, 2))

    // Assert
    expect(meaningShown()).toBe('Released (belongs under Done)')
    expect(
      screen.getByText(
        'This meaning belongs under another category. Move the status there, or choose another meaning.',
      ),
    ).toBeInTheDocument()
  })

  it("opens on the status's category read from its id, named as the owner type labels it", async () => {
    // Arrange / Act — the navigation's name is the label, which is not a category value
    await renderForm(statusIn(StatusCategory.Done, 2, 'Concluded'), LABELS)

    // Assert
    expect(categoryShown()).toBe('Concluded')
    expect(meaningShown()).toBe('Released (required)')
  })

  it('names where a misfiled meaning belongs by the owner type label', async () => {
    // Arrange / Act
    await renderForm(statusIn(StatusCategory.Active, 2, 'Underway'), LABELS)

    // Assert
    expect(meaningShown()).toBe('Released (belongs under Concluded)')
  })

  it('saves an unchanged labelled status without reclassifying it', async () => {
    // Arrange
    await renderForm(statusIn(StatusCategory.Done, 2, 'Concluded'), LABELS)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(mockReclassify).not.toHaveBeenCalled()
  })

  it('reclassifies into the category behind the chosen label', async () => {
    // Arrange
    mockReclassify.mockResolvedValue({})
    await renderForm(statusIn(StatusCategory.Done, 2, 'Concluded'), LABELS)

    // Act
    await chooseCategory('Underway')
    await userEvent.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(mockReclassify).toHaveBeenCalledWith(
      expect.objectContaining({
        request: { category: StatusCategory.Active, alias: 0 },
      }),
    )
  })

  describe('description only', () => {
    const save = () =>
      userEvent.click(screen.getByRole('button', { name: 'Save' }))

    it('shows the name, category and meaning read-only', async () => {
      // Arrange / Act
      await renderForm(
        statusIn(StatusCategory.Done, 2),
        undefined,
        'description',
      )

      // Assert
      expect(screen.getByLabelText('Name')).toBeDisabled()
      expect(screen.getByLabelText('Category')).toBeDisabled()
      expect(screen.getByLabelText('Meaning')).toBeDisabled()
      expect(screen.getByLabelText('Description')).toBeEnabled()
      expect(screen.getByLabelText('Description')).toHaveAccessibleDescription(
        'This workflow is kept by a sync. Only the description can be changed here.',
      )
    })

    it('sends only the description', async () => {
      // Arrange
      mockDescribe.mockResolvedValue({})
      await renderForm(
        statusIn(StatusCategory.Done, 2),
        undefined,
        'description',
      )

      // Act
      await userEvent.type(
        screen.getByLabelText('Description'),
        'Out the door.',
      )
      await save()

      // Assert
      expect(mockDescribe).toHaveBeenCalledWith({
        workflowId: 'w-1',
        statusId: 's-1',
        request: { description: 'Out the door.' },
      })
      expect(mockRename).not.toHaveBeenCalled()
      expect(mockReclassify).not.toHaveBeenCalled()
    })

    it('clears a description', async () => {
      // Arrange
      mockDescribe.mockResolvedValue({})
      await renderForm(
        { ...statusIn(StatusCategory.Done, 2), description: 'Out the door.' },
        undefined,
        'description',
      )

      // Act
      await userEvent.clear(screen.getByLabelText('Description'))
      await save()

      // Assert
      expect(mockDescribe).toHaveBeenCalledWith(
        expect.objectContaining({ request: { description: '' } }),
      )
    })

    it('sends nothing when the description is unchanged, and still closes', async () => {
      // Arrange
      const onFormComplete = vi.fn()
      await renderForm(
        statusIn(StatusCategory.Done, 2),
        undefined,
        'description',
        onFormComplete,
      )

      // Act
      await save()

      // Assert
      expect(mockDescribe).not.toHaveBeenCalled()
      expect(onFormComplete).toHaveBeenCalled()
    })

    it('keeps the dialog open when the description is refused', async () => {
      // Arrange
      const onFormComplete = vi.fn()
      mockDescribe.mockResolvedValue({
        error: { status: 400, detail: 'This workflow is archived.' },
      })
      await renderForm(
        statusIn(StatusCategory.Done, 2),
        undefined,
        'description',
        onFormComplete,
      )

      // Act
      await userEvent.type(
        screen.getByLabelText('Description'),
        'Out the door.',
      )
      await save()

      // Assert
      expect(mockDescribe).toHaveBeenCalled()
      expect(onFormComplete).not.toHaveBeenCalled()
    })

    it('does not warn about a misfiled meaning it cannot change', async () => {
      // Arrange / Act — Released belongs under Done
      await renderForm(
        statusIn(StatusCategory.Active, 2),
        undefined,
        'description',
      )

      // Assert
      expect(
        screen.queryByText(/belongs under another category/),
      ).not.toBeInTheDocument()
    })
  })

  describe('name and description', () => {
    const save = () =>
      userEvent.click(screen.getByRole('button', { name: 'Save' }))

    it('shows the category and meaning read-only', async () => {
      // Arrange / Act
      await renderForm(
        statusIn(StatusCategory.Done, 2),
        undefined,
        'nameAndDescription',
      )

      // Assert
      expect(screen.getByLabelText('Name')).toBeEnabled()
      expect(screen.getByLabelText('Description')).toBeEnabled()
      expect(screen.getByLabelText('Category')).toBeDisabled()
      expect(screen.getByLabelText('Meaning')).toBeDisabled()
      expect(screen.getByLabelText('Name')).toHaveAccessibleDescription(
        'This workflow is published, so its categories and meanings are fixed. Clone it to change them.',
      )
    })

    it('sends a description change through rename', async () => {
      // Arrange
      mockRename.mockResolvedValue({})
      const onFormComplete = vi.fn()
      await renderForm(
        statusIn(StatusCategory.Done, 2),
        undefined,
        'nameAndDescription',
        onFormComplete,
      )

      // Act
      await userEvent.type(
        screen.getByLabelText('Description'),
        'Out the door.',
      )
      await save()

      // Assert
      expect(mockRename).toHaveBeenCalledWith(
        expect.objectContaining({
          request: expect.objectContaining({ description: 'Out the door.' }),
        }),
      )
      expect(mockDescribe).not.toHaveBeenCalled()
      expect(onFormComplete).toHaveBeenCalled()
    })

    it('sends nothing when nothing changed, and still closes', async () => {
      // Arrange
      const onFormComplete = vi.fn()
      await renderForm(
        statusIn(StatusCategory.Done, 2),
        undefined,
        'nameAndDescription',
        onFormComplete,
      )

      // Act
      await save()

      // Assert
      expect(mockRename).not.toHaveBeenCalled()
      expect(mockReclassify).not.toHaveBeenCalled()
      expect(mockDescribe).not.toHaveBeenCalled()
      expect(onFormComplete).toHaveBeenCalled()
    })

    it('renames without reclassifying', async () => {
      // Arrange
      mockRename.mockResolvedValue({})
      await renderForm(
        statusIn(StatusCategory.Done, 2),
        undefined,
        'nameAndDescription',
      )

      // Act
      await userEvent.clear(screen.getByLabelText('Name'))
      await userEvent.type(screen.getByLabelText('Name'), 'Live')
      await save()

      // Assert
      expect(mockRename).toHaveBeenCalledWith(
        expect.objectContaining({
          request: expect.objectContaining({ name: 'Live' }),
        }),
      )
      expect(mockReclassify).not.toHaveBeenCalled()
      expect(mockDescribe).not.toHaveBeenCalled()
    })

    it('does not warn about a misfiled meaning it cannot change', async () => {
      // Arrange / Act — Released belongs under Done
      await renderForm(
        statusIn(StatusCategory.Active, 2),
        undefined,
        'nameAndDescription',
      )

      // Assert
      expect(
        screen.queryByText(/belongs under another category/),
      ).not.toBeInTheDocument()
    })
  })
})
