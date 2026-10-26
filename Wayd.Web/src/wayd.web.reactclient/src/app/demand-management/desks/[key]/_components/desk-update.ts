import {
  IntakeDeskDto,
  IntakeDeskRole,
  UpdateIntakeDeskRequest,
} from '@/src/services/wayd-api'

/**
 * The desk as an update request, with the given changes. The update overwrites the whole desk, so a form
 * that edits part of it sends the rest unchanged.
 */
export const toUpdateRequest = (
  desk: IntakeDeskDto,
  changes: Partial<Omit<UpdateIntakeDeskRequest, 'id'>> = {},
): UpdateIntakeDeskRequest => ({
  id: desk.id,
  name: desk.name,
  description: desk.description,
  visibility: desk.visibility,
  isRequesterSelectable: desk.isRequesterSelectable,
  isOpen: desk.isOpen,
  requesterMayAddParticipants: desk.requesterMayAddParticipants,
  members: desk.members.map((m) => ({
    employeeId: m.employee.id,
    role: m.role,
  })),
  ...changes,
})

/** The ids of the desk's members holding the role. */
export const memberIds = (desk: IntakeDeskDto, role: IntakeDeskRole) =>
  desk.members.filter((m) => m.role === role).map((m) => m.employee.id)
