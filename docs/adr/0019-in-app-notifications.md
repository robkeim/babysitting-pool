# In-app Notifications are persisted until dismissed, delivered over SSE and hidden behind a flag

Members learn of a Posted Request or a Claim inside the app, without reloading (#137). This records the decisions; the first slice (#152) builds persistence and the list API, with no live stream or UI yet.

**Transport.** Server-Sent Events, built in the next slice. The flow is one-way, server to browser, over the existing single origin and cookie session (ADR 0006), and `EventSource` reconnects by itself. WebSockets were rejected as two-way machinery for a one-way need, and polling as the fallback only if SSE proves unworkable behind Fly's proxy (ADR 0018).

**Persistence.** A Notification is a row, so a Member sees what they missed while away; a live-only stream would lose it. The row holds ids, the kind, a timestamp and a small payload: the Area and start time for `request_posted`, the Sitter's id for `request_claimed`. No address, free text or name (ADR 0014). Rows are written in the same SQLite transaction as `AddPostedAsync` and `SaveClaimedAsync` (ADR 0004): a rolled-back Post or lost claim race leaves none. The Posted fan-out is added with the Post in that same `SaveChanges`, which EF sends as one batch; hand-written SQL is not allowed outside migrations (ADR 0004).

**Audience.** `request_posted` goes to every open Member except the Requester, Dormant Members included, because a Notification is how a lapsed Member finds out there is work. Closed Members are skipped: they cannot sign in, and Erasure would delete their rows anyway. `request_claimed` goes to the Requester only. No other event notifies; in particular Settle writes none, so ADR 0007 stands unchanged.

**Dismissal.** Dismissing hard-deletes the row. There is no read state and no `dismissed_at`: a Notification stays until the Member dismisses it. `DELETE /api/notifications/{id}` is idempotent (204 also for an id already gone) and answers 404 `notification_not_found` for another Member's id; `DELETE /api/notifications` dismisses all of the caller's own.

**Backstop.** Settle deletes Notifications older than 90 days (`CoopSettings.NotificationRetention`), so ones nobody dismisses do not accumulate. Erasure (ADR 0014) deletes all of the Member's Notifications.

**Flag.** Everything is behind the `notifications` flag (ADR 0016), removed by #160. The endpoints answer 404 `feature_off` while it is off. Rows are written regardless of the flag, so switching it on shows what happened before; the domain never reads a flag. If a rollout needs a quiet start, the 90-day sweep bounds the stored backlog.
