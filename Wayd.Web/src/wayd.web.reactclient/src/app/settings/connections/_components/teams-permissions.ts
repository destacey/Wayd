/**
 * Microsoft Graph application permissions for the Teams communication connector.
 * Keep docs/reference/integrations/teams.mdx aligned when these change.
 */
export const TEAMS_GRAPH_APPLICATION_PERMISSIONS = [
  {
    key: 'User.Read.All',
    permission: 'User.Read.All',
    reason:
      'Look up tenant users by email so Wayd can address Teams messages and calendar events to the right people.',
  },
  {
    key: 'Chat.Create',
    permission: 'Chat.Create',
    reason: 'Open Teams chats when Wayd sends communication through this connection.',
  },
  {
    key: 'ChatMessage.Send',
    permission: 'ChatMessage.Send',
    reason: 'Send messages in Teams chats on behalf of this connection.',
  },
  {
    key: 'Calendars.ReadWrite',
    permission: 'Calendars.ReadWrite',
    reason:
      'Create and update Outlook calendar events when Wayd sends scheduling-related communication.',
  },
] as const
