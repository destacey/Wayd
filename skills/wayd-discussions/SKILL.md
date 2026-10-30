---
name: wayd-discussions
description: Read before calling the Wayd MCP tools named Discussions_*. Guides agents holding the conversation on a Wayd record via the Wayd MCP server — an intake request or a project — reading its threads and the record's changes between them, posting a message with a subject, replying in a thread, writing an intake request's internal notes, and mentioning people. Use when the user asks what has been said on a request or project, wants to ask, answer or raise a topic on one, or wants someone told through its conversation.
---

# Wayd Discussions

The discussion tools are offered by the hosted MCP endpoint (`/mcp`) only, in its `discussions` toolset. They work on any record that has a conversation, named by two parameters:

| Record | `channelType` | `ownerId` |
| --- | --- | --- |
| Intake request | `demand.intake-request` | The request's UUID (see the `wayd-intake` skill) |
| Project | `ppm.project` | The project's UUID (see the `wayd-ppm` skill) |

A `channelType` that cannot be discussed returns 400. A record that does not exist, or that the caller cannot read, returns 404 — never 403 — so treat 404 as "not visible to you" and say so rather than retrying.

## Before any change

Posting and replying are changes: everyone who can read the record reads the message. Draft the text, show it to the user with where it will go — the record, the thread, and whether it is a message or an internal note — and wait for their go-ahead. Reads need no confirmation. When the user approves, post exactly what they approved.

## How a conversation is shaped

Each record has one conversation. A **top-level message** may carry a `subject` (up to 200 characters) and starts a **thread**; replies go one level deep. A message is `Public` or an `InternalNote`, and a reply takes its thread's kind, so a thread is wholly one or the other.

- **Read it** with `Discussions_GetEntries`: a page of top-level messages, oldest first, with the record's changes between them, as its Activity section shows them to the caller (`messagesOnly: true` leaves the changes out). A message posted with a change carries that change as `recordChange`. A thread shows only as `replyCount` on its message: read the replies with `Discussions_GetReplies`.
- **Check what the caller may do** with `Discussions_Get`: `canPost`, `canPostInternal`, `canModerate`, whether the conversation `isLocked`, and how many threads and messages the caller can read. Never offer an internal note when `canPostInternal` is false, and never post in a locked conversation.
- **Start a topic** with `Discussions_Post` (`kind` `Public`, an optional `subject`, a markdown `body`). Give a busy record's new topic a subject, so parallel conversations stay apart.
- **Answer in a thread** with `Discussions_Reply` and the top-level message's `id`. Reply in the thread a question was asked in rather than starting a new one.
- **Mention someone** by writing `@[Name](employee:{employeeId})` in the body. A mention notifies the person. What else it does depends on the record (below): say so before posting.

## Intake requests

- **Replies** are read by everyone who can read the request's detail; a caller who sees only its summary gets 404. **Internal notes** (`kind` `InternalNote`) are read only by the desk's agents and the Demand Management Admin. Write a note for anything the requester should not read — what the desk is weighing, doubts about the ask, who else should look — and never quote or paraphrase one in a reply.
- On a desk that is not private, a reply that mentions someone the author could add as a participant makes them one, so they can read the request. A participant cannot add anyone by mentioning them, and a mention in an internal note adds nobody and can name only the desk's members.
- Take employee ids from `IntakeEmployeeOptions_GetList`.
- To ask the requester a question and move the request to Waiting on Requester in one save, use `IntakeRequests_ChangeStatus` with a `reply` (see the `wayd-intake` skill) rather than posting and changing the status separately.

## Projects

- Everyone who can read the project reads and posts in its conversation, whatever the project's status. There are **no internal notes**: `canPostInternal` is always false, so write nothing meant for leaders only.
- **Moderating** — locking the conversation, deleting anyone's message, both done in the app — is for the project's delivery leaders: Owner or Manager on the project, its program or its portfolio, and the PPM administrator. `canModerate` says whether the caller is one.
- A mention only notifies. It never gives anyone access to the project: someone who cannot read it is never shown the notification, so mentioning them tells them nothing. Say so if the user mentions someone who may not have access.
- A message tells the project's role holders and followers, the people it mentions, and for a reply everyone who started or replied in its thread.
- Take employee ids from the project's people (`Projects_GetProject`, `Projects_GetProjectTeam`) or `Employees_GetList`.
