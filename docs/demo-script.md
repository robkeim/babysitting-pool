# Demo script

A run-through for pitching the co-op to people: which scenarios to show and in what order. It lists steps, not talking points. Rules behind each step live in [`specs/credit-engine.md`](specs/credit-engine.md) and [`specs/sign-in.md`](specs/sign-in.md); update this script when a flow changes.

## Setup

1. Start fresh: delete `babysitting.db` next to the API, then run `scripts/dev.sh` ([`development.md`](development.md)).
2. Open two browser profiles (or one normal and one private window) so two Members stay signed in side by side: **Ryan** (Requester) and **Sarah** (Sitter). **Oscar** (25.5 credits) is the Member the Admin offboards in Scenario 9; before then, have Oscar post one request so there is something to cancel. Use a third window or profile for the Admin.
3. Set the SMTP secrets once, before the first start ([`development.md`](development.md)), so invitations and sign-in links arrive in a real inbox. Keep a terminal visible too: links also appear in the API console and at `GET /api/dev/outbox?to=<email>`.
4. Seeded data, as plus-addresses of the base address you set (`<local>+admin@<domain>`, `+ryan`, `+sarah`, `+oscar`): the Admin, then Ryan, Sarah and Oscar holding 10.0, 0.0 and 25.5 credits. None has a profile address yet. Without the secrets the seed falls back to `admin@`, `ryan@`, `sarah@` and `oscar@example.test`, which can't receive mail.
5. Sign in as the Admin with the link logged at startup.

> Sarah starts with 0.0 credits, which suits the story: she can't post until she earns by sitting.

## Not demoable yet

Present these as roadmap, not live:

- In-app notifications: the backend and live stream exist behind the `notifications` feature flag, but there is no screen yet. Members find changes on **My Requests** and **My Claims**.
- Editing a Request (cancel and re-post instead).
- Running Settle from the UI. Payout and expiry need `POST /api/admin/settle` or the clock to pass.
- Demurrage in action: it only bites after 21 days of inactivity, so explain it verbally and point at the rules page and the Pool overview.
- Several addresses per Member, child profiles, disputes and no-show handling.

## Scenario 1: The Admin invites a new person

*Shows: no public sign-up, passwordless onboarding, starting balance.*

1. In the Admin window, open **Invitations**.
2. Enter a new email (a new plus-address of the base address, for example `+dana`) and select **Send invitation**. The list shows it as Pending.
3. Open `/sign-in` in the new person's window and point out there is no sign-up, only sign-in.
4. Copy the Invitation link from the console or outbox and open it. The page shows "Join the co-op" with the invited email.
5. Enter a display name and select **Create my account**.
6. Land on **Credits**, signed in, with 10.0 available and a Grant entry in the Ledger.
7. Reopen the same link to show it is single-use ("This invitation can't be used").
8. In the Admin window, refresh **Invitations**: the invitation is Accepted.
9. Optional: invite another email and use **Re-send** to show a fresh link replacing the old one.

## Scenario 2: A returning member signs in

*Shows: passwordless sign-in, no account enumeration.*

1. Sign out from the header.
2. Open `/sign-in`, enter Ryan's address (`+ryan`), select **Email me a sign-in link**.
3. Show the "Check your email" confirmation.
4. Repeat with an address that isn't a Member and show the identical confirmation.
5. Copy Ryan's link from the console or outbox and open it.
6. Land signed in on **Credits**.
7. Open the same link again to show it is single-use, with a link back to `/sign-in`.
8. While signed out, open `/requests` to show the redirect to `/sign-in`.

## Scenario 3: Ryan learns the rules and sets up a profile

*Shows: transparent rules, profile used for requests at home.*

1. As Ryan, open **How it works** from the account menu. Skim the four cards: credits, pricing, requests, staying active.
2. Open the profile from the initial in the header.
3. Enter an area and an exact address, then **Save**.

## Scenario 4: Ryan posts a babysitting request

*Shows: the cost formula, cost breakdown, escrow.*

1. As Ryan, open **My Requests** (empty) and select **Post a request**.
2. Enter start time and a duration. Try 50 minutes to show the 15-minute rule, then fix it.
3. Change the number of children, meals and transport and watch the **Cost breakdown** update, including "After posting: … credits left".
4. Select **How is this calculated?** to show the pricing rules open in a new tab.
5. Choose the Requesting family's house. Transport is disabled with an explanation, and the form says the request will use the area and address from his profile.
6. Post the request.
7. On **My Requests**, the request is Posted and shows his exact address.
8. Open **Credits**: available dropped by the cost, the cost shows as In escrow, and the Ledger has an Escrow entry.

## Scenario 5: Sarah finds and claims the request

*Shows: privacy of the exact address, first claim wins.*

1. As Sarah (other window), open **Open Requests**.
2. Ryan's request is listed with area, time, size and credits, but no address.
3. Select **Claim**.
4. Open **My Claims** and show the request as Claimed.
5. As Ryan, refresh **My Requests** and show it is now Claimed.

## Scenario 6: Plans change (withdraw and cancel)

*Shows: no-penalty cancellation and full refunds.*

1. As Sarah, **My Claims**, select **Withdraw**. The request returns to Posted for others.
2. As Sarah, claim it again.
3. As Ryan, select **Cancel** on the request.
4. As Ryan, open **Credits**: escrow is released, a Refund entry is in the Ledger, and available is back to 10.0.

## Scenario 7: Credits move when the babysitting happens

*Shows: payout to the sitter, the credit economy closing the loop.*

1. As Ryan, post a new request (Scenario 4), then as Sarah claim it (Scenario 5).
2. Make the start time pass and run Settle: as the Admin, open **Admin**, then **Pool**, select **Run settle** and confirm. The result line counts Requests paid out, Requests expired and Members charged Demurrage. The minimum lead time is 6 hours, so for a live demo run the API in the `E2E` environment and advance its clock, or run this step ahead of time.
3. As Sarah, open **Credits**: a Payout entry, with a balance above 0.0.
4. As Ryan, open **My Requests**: the request is Completed and escrow is gone.
5. As Sarah, who now has credits, post a request of her own to close the loop.

## Scenario 8: The rules that protect the pool

*Shows: invariants enforced by the system.*

1. As Sarah with 0.0 credits (before Scenario 7), try to post a request. The form shows "Not enough credits" and posting is blocked.
2. As Ryan, try a start time less than 6 hours from now. Posting is refused.
3. As Ryan, try to claim his own request in **Open Requests**. The claim is refused, since a Requester and Sitter must differ.
4. As Ryan, post two requests whose times overlap (for example 3-5 PM and 4-6 PM). As Sarah, claim the first, then try the second. The second claim is refused as an overlap, while back-to-back times (3-5 PM and 5-7 PM) are allowed.
5. As Ryan, try a request at his house after clearing his profile address. The form sends him to the profile first.

Run this scenario right after Scenario 5 if the story flows better that way.

## Scenario 9: The Admin runs the co-op

*Shows: oversight with limited powers, privacy.*

1. As the Admin, open **Members**. Show open and closed Members, with the Admin's own row offering no actions.
2. Open a Member to see balances, Ledger and their Requests (area only, never the exact address).
3. Open **Pool**: credits in circulation, escrowed, burned by Demurrage and at Offboarding, open Members, Requests by state, and the dormant list.
4. Back in **Members**, select **Offboard** on a Member who has a Posted request (Oscar). Read the confirmation, confirm, and show the result: requests canceled, claims withdrawn, escrow refunded.
5. Select **Erase personal data** on the closed Member and show the name and email replaced by "Former member".
6. As the offboarded Member, try to sign in: the same "Check your email" answer, but no link arrives.
7. Open **Feature flags**, toggle one and show it takes effect without a restart.
8. As a non-Admin, open `/admin/members` to show it is refused.

## Scenario 10: The Admin flips the whole app to vacation mode

*Shows: runtime feature flags, no deploy, no restart.*

1. In the Admin window, open **Admin** then **Feature flags** and find `vacationMode`: an operator toggle, off.
2. Keep Ryan's or Sarah's window visible next to it, on any screen.
3. Tick **Enabled** and **Save**. The Admin's own window turns coral at once; the other windows follow within about a minute, or as soon as you click back into them.
4. To target a few people first, untick the rollout, set a Member's id in the always-on list and save: only that Member's window changes.
5. Untick **Enabled** and save to put everything back.

## Optional extras

- **Dark mode:** follows the system appearance setting ([`design.md`](design.md)); flip it in OS settings to show it.
- **Language:** switch from the account menu to show the app is translated.
- **Inactivity:** explain Demurrage (-2.0 credits per week after 21 days without activity) with the **How it works** page and the dormant list on **Pool**.

## Suggested order for a 10-minute pitch

Scenario 1 (invite and join) → 3 (rules and profile) → 4 (post) → 5 (claim) → 7 (payout) → 6 (cancel) → 8 (guardrails) → 9 (Admin). Keep Scenario 2 for Q&A or a technical audience.
