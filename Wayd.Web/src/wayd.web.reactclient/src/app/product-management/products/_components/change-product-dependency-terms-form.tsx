'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  ChangeProductDependencyTermsRequest,
  DependencyStrength,
  InteractionStyle,
  ProductDependencyDto,
} from '@/src/services/wayd-api'
import { useChangeProductDependencyTermsMutation } from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Alert, DatePicker, Form, Modal } from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import { useState } from 'react'
import { DependencyStrengthRadio } from './dependency-strength'
import { InteractionStyleCheckboxes } from './interaction-style'

const { Item } = Form

export interface ChangeProductDependencyTermsFormProps {
  dependency: ProductDependencyDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ChangeProductDependencyTermsFormValues {
  strength?: DependencyStrength
  interactionStyles?: InteractionStyle[]
  changedOn?: Dayjs
}

/**
 * Changes the terms a dependency holds on — its strength, how the products talk, or both.
 *
 * Opens on the current terms rather than pre-flipping the strength, which is what the strength-only form
 * used to do: with two fields there is no single "other" value to offer, and pre-selecting one would put a
 * change in front of somebody who came to edit the other.
 *
 * A change ends the dependency and records a new one from the chosen day, so time before it is still judged
 * by the terms that held then. Writing down styles that were never recorded is not a change — see
 * {@link isOnlyRecordingStyles} below.
 */
const ChangeProductDependencyTermsForm = ({
  dependency,
  onFormComplete,
  onFormCancel,
}: ChangeProductDependencyTermsFormProps) => {
  const messageApi = useMessage()

  const [changeTerms] = useChangeProductDependencyTermsMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ChangeProductDependencyTermsFormValues>({
      onSubmit: async (
        values: ChangeProductDependencyTermsFormValues,
        form,
      ) => {
        try {
          const request = {
            strength: values.strength,
            interactionStyles: values.interactionStyles,
            changedOn: values.changedOn?.format('YYYY-MM-DD'),
          } as ChangeProductDependencyTermsRequest

          const response = await changeTerms({
            productId: dependency.product.id,
            dependencyId: dependency.id,
            dependsOnProductId: dependency.dependsOnProduct.id,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Dependency terms changed.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while changing the dependency terms. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while changing the dependency terms. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  // The alert and the date field depend on what is currently chosen, tracked here rather than with
  // Form.useWatch: the form instance comes from useModalForm, and useWatch binds only to a form created by
  // the same antd module instance, which nothing here guarantees.
  const [{ strength, interactionStyles: styles }, setTerms] = useState<{
    strength?: DependencyStrength
    interactionStyles?: InteractionStyle[]
  }>({
    strength: dependency.strength,
    interactionStyles: dependency.interactionStyles ?? [],
  })

  // The current dependency ends the day before the change, so the change can fall no earlier than the day
  // after it started.
  const earliest = dayjs(dependency.startsOn).add(1, 'day')
  const startedToday = earliest.isAfter(dayjs(), 'day')

  // Filling in styles nobody had recorded changed nothing about the dependency, so the API records it in
  // place and keeps one period. A date would be meaningless, and the day-after rule does not apply.
  const hadNoStyles = !dependency.interactionStyles?.length
  const isOnlyRecordingStyles =
    hadNoStyles && strength === dependency.strength && !!styles?.length

  // Which of the three outcomes the current selection leads to. On opening, nothing has been chosen yet,
  // so neither of the other two is true — saying one anyway states an outcome for an action nobody has
  // taken, and a reader who believes it goes looking for an ended dependency that was never created.
  const recorded = dependency.interactionStyles ?? []
  const stylesChanged =
    (styles?.length ?? 0) !== recorded.length ||
    !recorded.every((s) => styles?.includes(s))
  const nothingChanged = strength === dependency.strength && !stylesChanged
  const willStartANewDependency = !nothingChanged && !isOnlyRecordingStyles

  const blocked = willStartANewDependency && startedToday

  // The day only means something to the dependency about to be created, so the field is offered only when
  // one is. Otherwise it invites a choice that is silently discarded — and its being available is how a
  // reader decides whether a new dependency is coming.
  const canChooseFirstDay = willStartANewDependency && !startedToday

  return (
    <Modal
      // The title and the button say which of the two things will happen. Leaving them on "Change" while
      // the alert says no new dependency starts makes the dialog contradict itself, and a reader acts on
      // the title — they expect the dependency to have ended and go looking for it under Show ended.
      title={
        isOnlyRecordingStyles
          ? 'Record Interaction Styles'
          : 'Change Dependency Terms'
      }
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid || blocked || nothingChanged }}
      okText={isOnlyRecordingStyles ? 'Record' : 'Change'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      {nothingChanged ? (
        <Alert
          type="info"
          showIcon
          title="Nothing has changed yet"
          description="Change the strength or the interaction to see what will happen to this dependency."
          style={{ marginBottom: 16 }}
        />
      ) : isOnlyRecordingStyles ? (
        <Alert
          type="info"
          showIcon
          title="This records how the products already talk"
          description="Nothing about the dependency is changing, so it keeps one period and no new dependency starts."
          style={{ marginBottom: 16 }}
        />
      ) : blocked ? (
        <Alert
          type="warning"
          showIcon
          title="This dependency started today"
          description="Its terms can change from tomorrow. If it was recorded on the wrong terms, remove it and add it again."
          style={{ marginBottom: 16 }}
        />
      ) : (
        <Alert
          type="info"
          showIcon
          title="The current dependency ends and a new one starts"
          description="The current one ends the day before the change, so time before it is still judged by the terms that held then. It stays in the list under Show ended."
          style={{ marginBottom: 16 }}
        />
      )}
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="change-product-dependency-terms-form"
        onValuesChange={(_, values) => setTerms(values)}
        initialValues={{
          strength: dependency.strength,
          interactionStyles: dependency.interactionStyles ?? [],
        }}
      >
        <Item
          name="strength"
          label="Strength"
          rules={[{ required: true, message: 'Choose a strength' }]}
        >
          <DependencyStrengthRadio />
        </Item>
        <Item
          name="interactionStyles"
          label="Interaction"
          extra={
            hadNoStyles
              ? 'How these products talk. Nothing has been recorded yet, so choosing here records it without starting a new dependency.'
              : 'How these products talk. Changing this starts a new dependency, the same as changing the strength.'
          }
        >
          <InteractionStyleCheckboxes />
        </Item>
        <Item
          label="First Day"
          name="changedOn"
          extra={
            canChooseFirstDay
              ? 'The first day the new terms hold. Leave empty for today.'
              : 'Available once you choose a change that starts a new dependency.'
          }
        >
          <DatePicker
            style={{ width: '100%' }}
            disabled={!canChooseFirstDay}
            disabledDate={(current) =>
              current.isBefore(earliest, 'day') ||
              current.isAfter(dayjs(), 'day')
            }
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default ChangeProductDependencyTermsForm
