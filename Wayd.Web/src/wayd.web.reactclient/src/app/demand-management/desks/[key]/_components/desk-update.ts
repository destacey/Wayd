import {
  IntakeDeskDto,
  IntakeDeskRole,
  UpdateIntakeDeskRequest,
} from '@/src/services/wayd-api'

/**
 * The desk as an update request, with the given changes. The update overwrites the whole desk, so a form
 * that edits part of it sends the rest unchanged — and the revision it was read at, so the server refuses
 * the save if someone changed the desk since rather than undoing their change.
 */
export const toUpdateRequest = (
  desk: IntakeDeskDto,
  changes: Partial<
    Omit<UpdateIntakeDeskRequest, 'id' | 'expectedRevision'>
  > = {},
): UpdateIntakeDeskRequest => ({
  id: desk.id,
  expectedRevision: desk.revision,
  name: desk.name,
  description: desk.description,
  visibility: desk.visibility,
  isRequesterSelectable: desk.isRequesterSelectable,
  isOpen: desk.isOpen,
  requesterMayAddParticipants: desk.requesterMayAddParticipants,
  offersAllChannels: desk.offersAllChannels,
  channelIds: desk.channelIds,
  offersAllTagCategories: desk.offersAllTagCategories,
  tagCategoryIds: desk.tagCategoryIds,
  allowsFreeTags: desk.allowsFreeTags,
  members: desk.members.map((m) => ({
    employeeId: m.employee.id,
    role: m.role,
  })),
  // Left out unless a form changes it, so the server keeps the triagers and drops any member removed here.
  triagerIds: undefined,
  ...changes,
})

/** The ids of the desk's members who triage its new requests. */
export const triagerIds = (desk: IntakeDeskDto) =>
  desk.members.filter((m) => m.isTriager).map((m) => m.employee.id)

/** The ids of the desk's members holding the role. */
export const memberIds = (desk: IntakeDeskDto, role: IntakeDeskRole) =>
  desk.members.filter((m) => m.role === role).map((m) => m.employee.id)
