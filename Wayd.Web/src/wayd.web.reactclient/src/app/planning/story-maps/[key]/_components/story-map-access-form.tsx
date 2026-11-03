'use client'

import { Form, Modal } from 'antd'
import { useEffect, useRef } from 'react'
import {
  StoryMapDetailsDto,
  StoryMapEditAccess,
  Visibility,
} from '@/src/services/wayd-api'
import { toApiError, toFormErrors } from '@/src/utils'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  useSetStoryMapEditAccessMutation,
  useSetStoryMapEditorsMutation,
  useSetStoryMapOwnersMutation,
  useSetStoryMapVisibilityMutation,
} from '@/src/store/features/planning/story-maps-api'
import { useModalForm } from '@/src/hooks'
import StoryMapAccessFields, {
  type StoryMapAccessValues,
} from '@/src/app/planning/story-maps/_components/story-map-access-fields'

export interface StoryMapAccessFormProps {
  map: StoryMapDetailsDto
  onFormUpdate: () => void
  onFormCancel: () => void
}

const sameSet = (a: string[], b: string[]) =>
  a.length === b.length && a.every((id) => b.includes(id))

/**
 * Owners, editors, visibility and who can edit a map. Each is saved through its
 * own endpoint, only when it changed.
 */
const StoryMapAccessForm = ({
  map,
  onFormUpdate,
  onFormCancel,
}: StoryMapAccessFormProps) => {
  const { user } = useAuth()
  const messageApi = useMessage()

  const [setOwners] = useSetStoryMapOwnersMutation()
  const [setEditors] = useSetStoryMapEditorsMutation()
  const [setVisibility] = useSetStoryMapVisibilityMutation()
  const [setEditAccess] = useSetStoryMapEditAccessMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<StoryMapAccessValues>({
      onSubmit: async (values: StoryMapAccessValues, form) => {
        // Each call validates its own request, so its errors name its own properties.
        let fieldFor: ((key: string) => string | undefined) | undefined
        try {
          const editorIds = values.editorIds ?? []
          const editorTeamIds = values.editorTeamIds ?? []
          if (
            !sameSet(
              editorIds,
              map.editors.map((e) => e.id),
            ) ||
            !sameSet(
              editorTeamIds,
              map.editorTeams.map((t) => t.id),
            )
          ) {
            fieldFor = (key) =>
              key.startsWith('employeeIds')
                ? 'editorIds'
                : key.startsWith('teamIds')
                  ? 'editorTeamIds'
                  : undefined
            const response = await setEditors({
              id: map.id,
              employeeIds: editorIds,
              teamIds: editorTeamIds,
            })
            if (response.error) throw response.error
          }

          // A private map keeps its edit access, which applies again if it turns public.
          if (
            values.visibility === Visibility.Public &&
            values.editAccess &&
            values.editAccess !== map.editAccess
          ) {
            fieldFor = undefined
            const response = await setEditAccess({
              id: map.id,
              editAccess: values.editAccess,
            })
            if (response.error) throw response.error
          }

          // After the editors, so the people added above are let in when it turns private.
          if (values.visibility !== map.visibility) {
            fieldFor = undefined
            const response = await setVisibility({
              id: map.id,
              visibility: values.visibility,
            })
            if (response.error) throw response.error
          }

          // Last: an owner who removes themselves can change nothing after it.
          const ownerIds = values.ownerIds ?? []
          if (
            !sameSet(
              ownerIds,
              map.owners.map((o) => o.id),
            )
          ) {
            fieldFor = (key) =>
              key.startsWith('employeeIds') ? 'ownerIds' : undefined
            const response = await setOwners({
              id: map.id,
              employeeIds: ownerIds,
            })
            if (response.error) throw response.error
          }

          messageApi.success('Successfully updated story map access.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors, { fieldFor }))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the story map access. Please try again.',
            )
            console.error(error)
          }
          return false
        }
      },
      onComplete: onFormUpdate,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the story map access. Please try again.',
      // The map decides who may change its access; the page offers it only to its owners.
      permission: 'Permissions.StoryMaps.View',
    })

  // Seeded once per opening: the map refetches on every collaborator's edit, and reseeding would
  // undo what the user is changing.
  const seeded = useRef(false)
  useEffect(() => {
    if (!isOpen) {
      seeded.current = false
      return
    }
    if (!seeded.current) {
      seeded.current = true
      form.setFieldsValue({
        ownerIds: map.owners.map((o) => o.id),
        visibility: map.visibility,
        editAccess: map.editAccess ?? StoryMapEditAccess.AnyoneWhoCanView,
        editorIds: map.editors.map((e) => e.id),
        editorTeamIds: map.editorTeams.map((t) => t.id),
      })
    }
  }, [isOpen, map, form])

  const selfEmployeeId = user?.employeeId ?? undefined

  return (
    <Modal
      title="Story Map Access"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="story-map-access-form"
      >
        <StoryMapAccessFields
          selfEmployeeId={
            map.owners.some((o) => o.id === selfEmployeeId)
              ? selfEmployeeId
              : undefined
          }
        />
      </Form>
    </Modal>
  )
}

export default StoryMapAccessForm
