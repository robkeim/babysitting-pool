# Babysitting Pool

A community babysitting co-op where members exchange childcare using a time-credit economy instead of money.

## Language

### People

**Member**:
A person in the co-op who holds a Credit Account. The only kind of actor in the system.
_Avoid_: User, account holder, owner

**Requester**:
The Member who posts a Request and pays for it.
_Avoid_: Owner, parent, client

**Sitter**:
The Member who claims a Request and cares for the children.
_Avoid_: Host, babysitter

**Admin**:
A Member who runs the co-op: creates Invitations, offboards Members, and has read-only views of every Member and the whole pool. Admins cannot change balances.
_Avoid_: Moderator, superuser

**Dormant Member**:
A Member with no Request Completed, as Requester or as Sitter, for longer than the dormancy threshold (default 6 weeks), counted from account creation if they have none. Only Admins can see who is dormant, so that they can reach out and decide whether the Member should stay. Dormancy is the longer-period stage of the same inactivity that triggers Demurrage.
_Avoid_: Inactive Member, churned

**Offboarding**:
Closing a Member's account, which cancels or releases their open Requests and burns their remaining balance. Ledger history is kept.
_Avoid_: Deleting, banning

**Erasure**:
Removing a closed Member's personal details (name, email, notes, addresses) while keeping the Ledger entries as anonymous history. Separate from Offboarding, which keeps those details. Only an Admin can erase, and only after Offboarding.
_Avoid_: Deleting an account, GDPR delete

**Invitation**:
A magic link an admin sends to a prospective Member, which is the only way to create an account.
_Avoid_: Sign-up, registration

**Sign-in link**:
A single-use, one-day magic link emailed to an existing Member, which signs them in. Invitations create accounts; sign-in links only sign in.
_Avoid_: Login link, password reset

**Sign-in session**:
A signed-in browser, ended by signing out or after 30 days without use. Called `Session` in the Identity code; not the period of care, which is a Session under Requests.
_Avoid_: Login

### Requests

**Request**:
A Member's posted ask for childcare, which is born Posted and ends Completed, Cancelled or Expired.
_Avoid_: Booking, job, session

**Claimed**:
The state of a Request once a Sitter has taken it. The first claim wins and needs no approval.
_Avoid_: Approved, accepted

**Notification**:
An in-app message kept for one Member until they dismiss it or its Request starts: a Request was Posted (sent to every other open Member) or a Request was Claimed (sent to its Requester), a Claimed Request was cancelled (sent to its Sitter), or a Sitter withdrew from a Request (sent to its Requester). It carries ids and a few facts, never an address or a name. Not an email.
_Avoid_: Alert, message, publication, accepted

**Minimum Lead Time**:
The least notice a Request must give between posting and its start time. Defaults to 6 hours and is configurable.
_Avoid_: Notice period

**Claim Cutoff**:
The moment before a Request's start time after which it can no longer be claimed, and an unclaimed Request expires. Defaults to 3 hours before the start time and is configurable.
_Avoid_: Deadline, expiry time

**Expired**:
The end state of a Request that was never claimed by its Claim Cutoff; its Escrow is refunded.
_Avoid_: Lapsed, timed out

**Completed**:
The end state of a Claimed Request once its start time arrives and the Payout is made.
_Avoid_: Done, finished

**Session**:
The period of care itself, from the Request's start time onward.
_Avoid_: Booking, hosting session

**Location**:
Where a Session takes place: *Sitter's House* or *Requesting Family's House*. Only a Request at the Requesting Family's House carries an Area and address.
_Avoid_: Venue, host's house

**Area**:
The rough neighbourhood a Member lives in, kept with their exact address on their profile. Other Members see the Area of a Posted Request at the Requesting Family's House; the exact address goes only to the Requester and the Sitter who claimed it.
_Avoid_: Neighbourhood, region, zone

### Credits

**Credit**:
The co-op's unit of value. One hour of standard babysitting for one child at the Sitter's house is 1.0 Credit.
_Avoid_: Points, tokens, money

**Credit Account**:
A Member's Available balance and Escrowed balance.
_Avoid_: Wallet

**Available Balance**:
The Credits a Member can spend. It never drops below 0.0.
_Avoid_: Balance, funds

**Escrow**:
Credits set aside from a Requester's Available Balance when a Request is posted, until they are refunded or paid out.
_Avoid_: Hold, reserve

**Ledger**:
The append-only record of every Credit movement (grant, escrow, refund, payout, demurrage).
_Avoid_: Transaction log, history

**Payout**:
The release of escrowed Credits to the Sitter when the Request's start time arrives.
_Avoid_: Settlement, transfer

**Demurrage**:
The weekly Credit penalty on a Member who has had no Request Completed, as Requester or as Sitter, within 21 days. Posting or claiming without following through does not count. Demurrage Credits are burned: they leave circulation with no recipient.
_Avoid_: Inactivity fee, decay

### Time

**Settle**:
The recurring run that applies every time-driven change that has become due: Expiry, Payout and Demurrage. It is not the same as a Payout.
_Avoid_: Cron job, batch, sweep

### Language

**Language**:
The language a Member reads the co-op in, chosen by the Member and kept on their profile. English (en-US) is the first, followed by French (fr-FR); others are added over time.
_Avoid_: Locale, translation

**Ünïcödé Tëst**:
A Language that renders English as accented look-alike text padded to roughly the length other languages need, so layout problems show up before a real translation exists. It is never offered to Members in production.
_Avoid_: Pseudo-locale, fake language
