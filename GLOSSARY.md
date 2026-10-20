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

**Offboarding**:
Closing a Member's account, which cancels or releases their open Requests and burns their remaining balance. Ledger history is kept.
_Avoid_: Deleting, banning

**Invitation**:
A magic link an admin sends to a prospective Member, which is the only way to create an account.
_Avoid_: Sign-up, registration

### Requests

**Request**:
A Member's posted ask for childcare, which is born Posted and ends Completed, Cancelled or Expired.
_Avoid_: Booking, job, session

**Claimed**:
The state of a Request once a Sitter has taken it. The first claim wins and needs no approval.
_Avoid_: Approved, accepted

**Minimum Lead Time**:
The least notice a Request must give between posting and its start time. Defaults to 6 hours and is configurable.
_Avoid_: Notice period

**Claim Cutoff**:
The moment before a Request's start time after which it can no longer be claimed, and an unclaimed Request expires. Defaults to 3 hours before the start time and is configurable.
_Avoid_: Deadline, expiry time

**Session**:
The period of care itself, from the Request's start time onward.
_Avoid_: Booking, hosting session

**Location**:
Where a Session takes place: *Sitter's House* or *Requesting Family's House*.
_Avoid_: Venue, host's house

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
The weekly Credit penalty on a Member who has not completed a Request as Sitter within 21 days. Demurrage Credits are burned: they leave circulation with no recipient.
_Avoid_: Inactivity fee, decay
