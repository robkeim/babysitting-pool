# Functional Specification: Co-op Credit & Ledger Engine

## 1. Overview
The Babysitting Co-op operates on a time-credit economy rather than fiat currency. 1 hour of standard babysitting = 1.0 credit. Members earn credits by hosting/babysitting for others and spend credits when requesting babysitting.

---

## 2. Initial Account State & Limits
- **Starting Balance:** New members receive **10.0 credits** upon account activation.
- **Minimum Balance Floor:** 
  - The available balance can never drop below **0.0 credits**.
  - Members with a 0.0 credit balance cannot submit new babysitting requests until they earn credits by babysitting.
- **Maximum Balance:** Uncapped.

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

[Draft Request] 
      │
      ▼ (Check Available Balance >= Cost)
[Posted / Announced] ────► Credits ESCROWED (Available Balance decreases)
      │
      ├───► [Cancelled / Unclaimed] ────► Credits REFUNDED (Escrow released back to Available)
      │
      ▼
[Claimed & Approved]
      │
      ▼ (Scheduled Babysitting Start Time Reached)
[In Progress / Completed] ──────► Credits TRANSFERRED (Escrow released to Sitter's Available Balance)

1. **Request Creation & Escrow:** 
   - Upon posting a request, the system calculates the required cost and moves that amount from the Requester’s **Available Balance** into **Escrow**.
   - If `Available Balance < Request Cost`, posting is blocked.
2. **Cancellation / Expiration:**
   - If a request is cancelled by the owner or expires unclaimed, the escrowed credits are immediately returned to the owner’s Available Balance.
3. **Payout Trigger:**
   - When the scheduled babysitting start time arrives, the escrowed credits are released and transferred directly to the Sitter's Available Balance in an atomic transaction. There is no separate mutual-completion confirmation.
   - The requester and the sitter must be different members.
4. **Cancellation Policy Note (Future Backlog):**
   - For v1, last-minute cancellations return escrowed credits without penalty. Late-cancellation penalties are deferred to a future milestone.

---

## 5. Inactivity Policy (Demurrage)
- **Definition of Activity:** Hosting or babysitting at least once within a rolling 21-day window.
- **Penalty Trigger:** If a member has no completed hosting sessions for > 21 days:
  - Apply a **-2.0 credits / week** penalty.
  - **Hard Floor Protection:** Penalties stop strictly at **0.0 credits** (never drives an account into negative).
  - *Admin Flag:* Accounts sitting at 0.0 credits due to inactivity are flagged for review/offboarding.
