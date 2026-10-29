import { act, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  IntakeRequestDetailsDto,
  IntakeRequestTagCategoryDto,
} from '@/src/services/wayd-api'
import ManageIntakeRequestTagsForm from './manage-intake-request-tags-form'

const tag = (id: string, name: string) => ({
  id,
  name,
  isActive: true,
  requestCount: 0,
})

const axis = (
  id: string,
  name: string,
  allowsMany: boolean,
  tags: ReturnType<typeof tag>[],
): IntakeRequestTagCategoryDto => ({
  id,
  name,
  allowsMany,
  order: 1,
  isActive: true,
  isSystem: false,
  tags,
})

const AREA = axis('a-area', 'Area', true, [
  tag('t-payments', 'Payments'),
  tag('t-onboarding', 'Onboarding'),
])
const SEGMENT = axis('a-segment', 'Segment', false, [
  tag('t-enterprise', 'Enterprise'),
  tag('t-smb', 'SMB'),
])
// Not offered by the request's desk; the request brought Legacy with it from another desk.
const OLD_AREA = axis('a-old', 'Old Area', true, [
  tag('t-legacy', 'Legacy'),
  tag('t-other', 'Other'),
])
// Not offered, and carried by nothing.
const UNRELATED = axis('a-unrelated', 'Unrelated', true, [
  tag('t-unrelated', 'Whatever'),
])

const request = {
  summary: {
    id: 'r-1',
    key: 1,
    title: 'A request',
    desk: { id: 'd-1', key: 1, name: 'IT' },
    tags: [
      {
        id: 't-enterprise',
        name: 'Enterprise',
        categoryId: 'a-segment',
        categoryName: 'Segment',
      },
      {
        id: 't-legacy',
        name: 'Legacy',
        categoryId: 'a-old',
        categoryName: 'Old Area',
      },
    ],
  },
} as unknown as IntakeRequestDetailsDto

const mockTag = jest.fn()
const mockUntag = jest.fn()
const mockTagByName = jest.fn()
let mockAllowsFreeTags = false
const mockError = jest.fn()
const mockComplete = jest.fn()

// The form instance is real so antd's Form binds to it; handleOk runs the real submit so the diff is
// exercised end to end.
jest.mock('@/src/hooks', () => {
  const { Form: AntForm } = jest.requireActual('antd')
  return {
    useModalForm: ({
      onSubmit,
      onComplete,
    }: {
      onSubmit: (values: unknown, form: unknown) => Promise<boolean>
      onComplete: () => void
    }) => {
      const [form] = AntForm.useForm()
      return {
        form,
        isOpen: true,
        isValid: true,
        isSaving: false,
        handleOk: async () => {
          if (await onSubmit(form.getFieldsValue(), form)) onComplete()
        },
        handleCancel: jest.fn(),
      }
    },
  }
})

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: mockError, success: jest.fn() }),
}))

jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeRequestTagCategoriesQuery: (args?: { deskId?: string }) => ({
      // With the desk, what it offers; without, every axis.
      data: args?.deskId
        ? [AREA, SEGMENT]
        : [AREA, SEGMENT, OLD_AREA, UNRELATED],
      isLoading: false,
    }),
    useTagIntakeRequestMutation: () => [mockTag],
    useUntagIntakeRequestMutation: () => [mockUntag],
    useTagIntakeRequestByNameMutation: () => [mockTagByName],
    useGetIntakeDeskQuery: () => ({
      data: { id: 'd-1', allowsFreeTags: mockAllowsFreeTags },
      isLoading: false,
    }),
    useSearchIntakeRequestTagsQuery: () => ({
      data: [
        {
          id: 't-followup',
          name: 'follow up',
          isFree: true,
          isActive: true,
          requestCount: 2,
        },
      ],
      isLoading: false,
    }),
  }),
)

const renderForm = async () =>
  await act(async () =>
    render(
      <ManageIntakeRequestTagsForm
        request={request}
        onFormComplete={mockComplete}
        onFormCancel={jest.fn()}
      />,
    ),
  )

const fieldFor = (label: string) =>
  screen.getByText(label).closest('.ant-form-item') as HTMLElement

/** Opens an axis's dropdown, returning it. antd portals each one out of the form. */
const openDropdown = async (label: string) => {
  const user = userEvent.setup()
  const input = fieldFor(label).querySelector(
    'input[role="combobox"]',
  ) as HTMLElement
  await act(async () => {
    await user.click(input)
  })
  const listboxId = input.getAttribute('aria-controls')!
  return await waitFor(() => {
    const el = document
      .getElementById(listboxId)
      ?.closest('.ant-select-dropdown') as HTMLElement | null
    if (!el) throw new Error(`no dropdown for ${label}`)
    return el
  })
}

const selectOption = async (label: string, option: string) => {
  const dropdown = await openDropdown(label)
  await act(async () => {
    await userEvent.setup().click(await within(dropdown).findByTitle(option))
  })
}

const save = async () => {
  await act(async () => {
    await userEvent.setup().click(screen.getByRole('button', { name: 'Save' }))
  })
}

describe('ManageIntakeRequestTagsForm', () => {
  beforeEach(() => {
    mockTag.mockReset().mockResolvedValue({ data: undefined })
    mockUntag.mockReset().mockResolvedValue({ data: undefined })
    mockTagByName.mockReset().mockResolvedValue({ data: 't-new' })
    mockAllowsFreeTags = false
    mockError.mockReset()
    mockComplete.mockReset()
  })

  it('offers the desk’s axes and, for an axis it does not use, only the tag the request carries', async () => {
    // Arrange / Act
    await renderForm()

    // Assert
    expect(screen.getByText('Area')).toBeInTheDocument()
    expect(screen.getByText('Segment')).toBeInTheDocument()
    expect(screen.getByText('Old Area')).toBeInTheDocument()
    expect(screen.queryByText('Unrelated')).not.toBeInTheDocument()
    const oldArea = await openDropdown('Old Area')
    expect(within(oldArea).queryByTitle('Other')).not.toBeInTheDocument()
  })

  it('removes the tag a single-value axis held before applying its replacement', async () => {
    // Arrange — tagging first would have the server replace Enterprise, and the untag would then strip SMB
    await renderForm()

    // Act
    await selectOption('Segment', 'SMB')
    await save()

    // Assert
    expect(mockUntag).toHaveBeenCalledWith({
      requestId: 'r-1',
      tagId: 't-enterprise',
    })
    expect(mockTag).toHaveBeenCalledWith({ requestId: 'r-1', tagId: 't-smb' })
    expect(mockUntag.mock.invocationCallOrder[0]).toBeLessThan(
      mockTag.mock.invocationCallOrder[0],
    )
  })

  it('offers no free tags on a desk that does not allow them', async () => {
    // Arrange / Act
    await renderForm()

    // Assert
    expect(screen.queryByText('Free tags')).not.toBeInTheDocument()
  })

  it('applies an existing free tag by id and a typed name by name, on a desk allowing them', async () => {
    // Arrange
    mockAllowsFreeTags = true
    await renderForm()
    const input = fieldFor('Free tags').querySelector(
      'input[role="combobox"]',
    ) as HTMLInputElement

    // Act — committed with the field's comma separator, which jsdom drives reliably
    await act(async () => {
      await userEvent.setup().type(input, 'Follow Up,escalated,')
    })
    await save()

    // Assert
    expect(mockTag).toHaveBeenCalledWith({
      requestId: 'r-1',
      tagId: 't-followup',
    })
    expect(mockTagByName).toHaveBeenCalledWith({
      requestId: 'r-1',
      name: 'escalated',
    })
    expect(mockUntag).not.toHaveBeenCalled()
  })

  it('says how many changes were saved and closes when one fails part-way', async () => {
    // Arrange
    mockTag.mockResolvedValue({
      error: { detail: 'This desk does not use that tag axis.' },
    })
    await renderForm()

    // Act
    await selectOption('Segment', 'SMB')
    await save()

    // Assert
    expect(mockError).toHaveBeenCalledWith(
      expect.stringContaining('1 of 2 changes were saved'),
    )
    expect(mockComplete).toHaveBeenCalled()
  })
})
