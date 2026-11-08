# Functional Specification: Invitation, Account Creation and Sign-in

Extends credit-engine.md §2A (read it first). Decisions and rationale: [ADR 0012](../adr/0012-passwordless-auth.md).

## 1. Rules

- There is no public sign-up. A person gets in only through an **Invitation**; afterwards they sign in with a **sign-in link** sent to their email. There are no passwords.
- Both links carry a random secret that is single-use and stored only as a hash.
- **Invitation:** valid 7 days (`CoopSettings`). Following it shows the invited email and asks for a **display name** (1 to 60 characters after trimming). Submitting creates the Member, opens their Credit Account with the Starting Balance (a Grant Ledger entry), and signs them in. The Member's Language is not asked; it defaults to `en-US`. A signed-in Member changes it with `PUT /api/me/language` (`{ "language": "en-US" }`, 204); `GET /api/me` returns it. `en-XA` (Ünïcödé Tëst) is accepted only outside Production; any other tag is a 400 `invalid_request`. The Member's Color Scheme works the same way: it defaults to `system`, `PUT /api/me/color-scheme` (`{ "colorScheme": "light" | "dark" | "system" }`, 204) changes it, `GET /api/me` returns it as `colorScheme`, and any other value is a 400 `invalid_request`.
- **Sign-in link:** valid 1 day, single-use. Requesting one for an unknown, closed or mistyped email gives the same answer as for a real Member (no account enumeration), and sends nothing.
- **Session:** an HttpOnly cookie referencing a server-side session. It expires after 30 days without use (sliding) and can be revoked on sign-out or Offboarding. Sign-out revokes the session.
- Emails are normalized (trimmed, lower-cased) before use. At most one active Member per email; a closed Member's email may be invited again (creating a new Member).
- A Member whose account is closed cannot sign in and their sessions and links stop working.
- **The Admin:** exactly one. `Bootstrap:AdminEmail` makes the API issue an Admin Invitation at startup when no Admin Member exists and no live Admin Invitation is outstanding. An Admin Invitation is refused once an Admin exists. Production refuses to start when there is no Admin and no `Bootstrap:AdminEmail`.
- Only sign-in and Invitation emails are sent (credit-engine.md §2A). Their text is English for now.

## 2. Flows

1. **Invited person:** opens `/invitation?token=…` → sees "Join the co-op as <email>" and a display name field → submits → lands on their credits page with 10.0 credits, signed in. An invalid, used or expired link shows a message offering no way forward other than asking the Admin for a new Invitation.
2. **Returning Member:** opens `/sign-in` → enters their email → sees "If that address belongs to a Member, we've sent a link" (always) → opens `/sign-in/verify?token=…` from the email → is signed in and lands on their credits page. An invalid, used or expired link shows a message with a link back to `/sign-in`.
3. **Admin invites:** opens Invitations (Admin only) → sees every Invitation, newest first, as pending, accepted or expired → enters an email address to send a new Invitation, or re-sends a pending or expired one. A re-send gives the same Invitation a new link and a fresh 7 days, and the old link stops working. An accepted Invitation cannot be re-sent. An address that is already a Member, or that has an Invitation not yet accepted, cannot be invited again (re-send that one instead).
4. **Signed-out visitor** opening any page that needs a Member is sent to `/sign-in`. A signed-in Member can sign out from the header.

The link pages only submit the token when the page loads or the form is submitted (a POST), so email scanners that fetch URLs do not consume a link.

## 3. API surface

| Call | Result |
| --- | --- |
| `POST /api/auth/sign-in-links` `{ email }` | `202`, always (valid email); `400` if not an email |
| `POST /api/auth/sessions` `{ token }` | `204` + session cookie; `422 sign_in_link_invalid` |
| `DELETE /api/auth/sessions/current` | `204`, cookie cleared |
| `POST /api/invitations/preview` `{ token }` | `200 { email }`; `422 invitation_invalid` |
| `POST /api/invitations/accept` `{ token, displayName }` | `204` + session cookie; `400` bad display name; `422 invitation_invalid` |
| `GET /api/admin/invitations` (Admin) | `200 { items, nextCursor }`, each `{ id, email, isAdmin, status: Pending\|Accepted\|Expired, issuedAt, expiresAt, acceptedAt }`, newest first; `nextCursor` is always null |
| `POST /api/admin/invitations` `{ email }` (Admin) | `201` with the Invitation, link emailed; `400` bad email; `422 already_a_member`, `422 invitation_already_sent` |
| `POST /api/admin/invitations/{id}/resend` (Admin) | `200` with the Invitation; `404 invitation_not_found`; `422 invitation_already_accepted`, `422 already_a_member` |
| `GET /api/me` | `200 { memberId, email, displayName, isAdmin }`; `401 unauthenticated` |

State-changing calls need the `X-Requested-With` header (ADR 0006).

## 4. Development and test support

- Development and E2E: the fake outbox logs each message, including the link, and is readable at `GET /api/dev/outbox` (Development) or `GET /api/test/outbox` (E2E, needs the E2E token); the Admin's sign-in link is logged at startup. Development seeds one Admin and three Members (balances 10.0, 0.0, 25.5) through the application layer, idempotently.
- E2E only (needs the E2E token): `POST /api/test/sessions { memberId }` returns a session token for the cookie.
- Offboarding and Erasure are offered from the Admin Members list (#143, #96; the API is `POST /api/admin/members/{id}/offboard`, see credit-engine.md §2A).
