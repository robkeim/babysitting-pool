# Functional Specification: Co-op Credit & Ledger Engine

## 1. Overview
The Babysitting Co-op operates on a time-credit economy rather than fiat currency. 1 hour of standard babysitting = 1.0 credit. Members earn credits by hosting/babysitting for others and spend credits when requesting babysitting.

---

## 2. Initial Account State & Limits
- **Starting Balance:** New members receive **10.0 credits** once, when they create their account from an Invitation (see §2A).
- **Minimum Balance Floor:** 
  - The available balance can never drop below **0.0 credits**.
  - Members with a 0.0 credit balance cannot submit new babysitting requests until they earn credits by babysitting.
- **Maximum Balance:** Uncapped.

### 2A. Membership & Invitations
- There is no public sign-up. The site offers only sign-in for existing Members.
- Sign-in is passwordless: a Member requests a single-use magic link by email, which expires after a short time. An Invitation link is also single-use but valid longer (default 7 days), and an admin can re-send it.
- An admin sends a prospective Member an **Invitation** (a magic link). Following it lets them create their account, which grants the Starting Balance.

- **Offboarding:** When an admin offboards a Member, the system cancels their Posted Requests (refunding Escrow), withdraws their claims as Sitter (the Request returns to Posted, or expires if past the Claim Cutoff), cancels Requests they posted that are Claimed (refunding Escrow), and burns any remaining Available Balance via a Ledger entry. The account is closed, Ledger history is kept, and the person can only return via a new Invitation.

- **Admin powers (v1, kept deliberately small):** An Admin can create Invitations, offboard Members, view each Member's situation (balances, Requests, Ledger) read-only, and view an aggregated overview of the whole pool. Admins cannot edit balances or Requests; there are no manual Adjustments in v1.
- **Notifications (v1):** Members are notified by email and see events in an in-app feed. Which events trigger an email is a later policy decision.
- **Times:** Instants are stored in UTC; one co-op-wide time zone setting drives display and the weekly Demurrage run.

---

## 3. Cost Calculation Formula
The total cost of a request is calculated deterministically using the following rules:

### A. Base Duration
- Calculated in minutes, converted to fractional hours (e.g., 75 minutes = 1.25 hours).
- Duration must be a positive multiple of **15 minutes** (15, 30, 45, 60, 75, ...). Any other value (e.g., 82 minutes) is rejected. This keeps every cost an exact decimal, so no rounding rule is needed.
- Rate: 1.0 credit / hour for the 1st child.

### B. Additional Children Scaling (Per Hour)
- A request must include **at least 1 child**.
- Each increment is **cumulative**, added on top of the previous children's rate:
  - **1st Child:** +1.0x base rate (1.0 / hr).
  - **2nd Child:** +0.50 / hr.
  - **3rd+ Child:** +0.25 / hr per additional child.
- Resulting total hourly rate:

| Children | Hourly rate |
| -------- | ----------- |
| 1        | 1.00        |
| 2        | 1.50        |
| 3        | 1.75        |
| 4        | 2.00        |
| *n* (≥ 2) | 1.50 + 0.25 × (*n* − 2) |

### C. Location Premium
- **At Sitter's House:** 0.0 additions.
- **At Requesting Family's House:** Flat **+0.50 credits** location premium (compensates travel/inconvenience).

### D. Vehicle Transport (Pick-up / Drop-off)
- **One-way transport:** +0.50 credits.
- **Round-trip transport:** +0.75 credits.
- *Constraint:* Transport fees cannot be combined with the "At Requesting Family's House" location premium (mutually exclusive to prevent double-counting).

### E. Meals & Snacks (Per Child Flat Fee)
- **Lunch / Dinner:** +0.50 credits per child.
- **Snack:** +0.25 credits per child.

---

## 4. Transaction & Escrow Lifecycle

[New Request] 
      │
      ▼ (Check Available Balance >= Cost)
[Posted / Announced] ────► Credits ESCROWED (Available Balance decreases)
      │
      ├───► [Cancelled / Unclaimed] ────► Credits REFUNDED (Escrow released back to Available)
      │
      ▼
[Claimed]
      │
      ▼ (Scheduled Babysitting Start Time Reached)
[In Progress / Completed] ──────► Credits TRANSFERRED (Escrow released to Sitter's Available Balance)

1. **Request Creation & Escrow:** A Request is created only when it is posted; "draft" is a UI-only half-filled form with no domain state.
   - Upon posting a request, the system calculates the required cost and moves that amount from the Requester’s **Available Balance** into **Escrow**.
   - If `Available Balance < Request Cost`, posting is blocked.
   - A Request's start time must be at least the **Minimum Lead Time** (default 6 hours, configurable) after the time of posting; otherwise posting is blocked.
2. **Cancellation / Expiration:**
   - If a request is cancelled by the owner or expires unclaimed, the escrowed credits are immediately returned to the owner’s Available Balance.
   - **Claim Cutoff:** A Posted Request can no longer be claimed once the **Claim Cutoff** passes (default 3 hours before the start time, configurable). An unclaimed Request expires at the Claim Cutoff, so a Sitter is never claiming too late to arrange care.
   - With the default settings, a Request has at least 3 hours (Minimum Lead Time minus Claim Cutoff) to be claimed.
3. **Payout Trigger:**
   - When the scheduled babysitting start time arrives, the escrowed credits are released and transferred directly to the Sitter's Available Balance in an atomic transaction. There is no separate mutual-completion confirmation.
   - The requester and the sitter must be different members.
4. **Claiming:** The first Sitter to claim a Request wins; there is no Requester approval step.
5. **Cancellation & Withdrawal:**
   - The Requester may cancel a Posted or Claimed Request any time before the start time, with a full refund.
   - The Sitter may withdraw a claim before the start time. The Request returns to Posted if time remains; otherwise (the Claim Cutoff has passed) it expires and is refunded.
   - For v1, last-minute cancellations and withdrawals carry no penalty. Late-cancellation penalties are deferred to a future milestone.
6. **Editing a Request:**
   - A Requester can edit a Posted or Claimed Request any time before the start time.
   - Cost drivers (start time, duration, child count, Location, transport, meals) re-price the Request. A higher cost needs enough Available Balance and escrows the difference; if the Requester can't cover it, the edit is rejected. A lower cost refunds the difference immediately.
   - A start-time edit must satisfy the same Minimum Lead Time rule as posting, measured against the current time.
   - The exact address is shown only to the counterparty once the Request is Claimed, and hidden again if the claim is dropped. Posted Requests show Members only a rough area.
   - Editing a Claimed Request drops the claim. Every field the Sitter relies on (all cost drivers, address and drop-off point) does this; only free-text notes are exempt, and the Sitter is still notified of them. The Request returns to Posted with the new terms and its Escrow adjusted to the new cost. The former Sitter is notified and may claim it again like any other Member, first claim wins. Future: first rights for the former Sitter to reconfirm (backlog: #5).
   - After the Claim Cutoff, a Claimed Request can only have its free-text notes edited. An edit that would drop the claim is blocked with an explanation, so the Request does not silently expire.
7. **Sitter overlap (v1):** A Sitter may hold overlapping Claimed Requests; Sitter capacity is not modelled (backlog: #3).
8. **Children (v1):** A Request records only the number of children (backlog for child profiles: #4).
9. **Known v1 trade-off (Future Backlog):** Payout happens at the start time, so a no-show Sitter is still paid. Disputes and no-show handling are deferred.

---

## 5. Inactivity Policy (Demurrage)
- **Definition of Activity:** Completing at least one Request as Sitter within a rolling 21-day window. Posting or paying as Requester does not count.
- **Penalty Trigger:** Demurrage is evaluated at a weekly boundary (default Monday 00:00 in the co-op time zone; weekday and time configurable). If a member's last Request completed as Sitter (or, if none, their account creation) is more than 21 days before the boundary:
  - Apply a **-2.0 credits** penalty for that boundary. Because charges fall only on boundaries, the effective grace period is 21 to 28 days.
  - Each charge is recorded with the boundary it belongs to, so a boundary can be charged at most once per Member. After downtime, every missed boundary is applied in order, each evaluated as of its own time. Boundaries before the account existed, and offboarded Members, are skipped.
  - Only Available Balance is charged; Escrow is never touched.
  - Demurrage Credits are burned (recorded as a Ledger entry with no recipient).
  - **Hard Floor Protection:** The charge is min(2.0, Available Balance); penalties stop strictly at **0.0 credits** (never drives an account into negative) and no zero-amount Ledger entry is written.
  - *Admin Flag:* Accounts sitting at 0.0 credits due to inactivity are flagged for review/offboarding.

---

## 6. Settle (Time-driven Processes)
Expiry, Payout and Demurrage are applied by **Settle** (see ADR 0007).
- **Order within a run:** Expire, then Payout, then Demurrage.
- **Expire:** every Posted Request past its Claim Cutoff becomes Expired and its Escrow is refunded.
- **Payout:** every Claimed Request past its start time becomes Completed and its Escrow is released to the Sitter.
- **Claim Cutoff snapshot:** the cutoff instant is stored on the Request when it is posted or edited. Later changes to the setting affect only new or edited Requests.
- **Between runs:** claims past the Claim Cutoff are rejected by the clock check, and read models show an overdue Posted Request as Expired.
- **Idempotency:** Settle selects only Requests still Posted or Claimed past their trigger, so a repeat run does nothing. Demurrage is idempotent per (Member, boundary).
