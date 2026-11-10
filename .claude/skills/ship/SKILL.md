---
name: ship
description: Open a PR, or merge one. Use for "open/create a PR" or finishing a ticket's branch (Open), and for "merge" once the human has reviewed (Merge).
---

A PR exists only for validated work, so it opens ready for review, never as a draft. Green means `scripts/check.sh all --full --stamped --quiet` passes on the exact tree being shipped (the stamp is per tree hash, so a changed tree re-runs it).

## Open

Opening ends the run: the human reviews the PR next, and Merge starts only when they ask for it.

1. Run the full gate. Done when it prints green.
2. Review in a fresh context. Spawn one sub-agent (`general-purpose`) with no knowledge of how the change was written. Give it only the diff command (`git diff main...HEAD`), the ticket (`gh issue view <n>`) and [`docs/review-checklist.md`](../../../docs/review-checklist.md), and have it run the `code-review` skill (Standards and Spec axes) plus every checklist item. It returns findings as `path:line`, description and severity. Fix each blocking finding and re-run step 1, or keep it and state why in the PR body. Done when every blocking finding is resolved or accepted.
3. Push branch `<issue>-<slug>` and run `gh pr create` with a Conventional Commit title and the body from the `pr` skill, ending in `Closes #n`. List accepted blocking findings in the body.
4. Post the findings as one PR comment in the table format from the checklist (`gh pr comment`), with status `resolved` or `accepted` for each.

Done when `gh pr view --json isDraft,url` shows `isDraft: false`.

## Merge

Runs on the human's explicit request, after their review.

1. `git fetch origin && git merge origin/main`. Resolve every conflict, keeping both sides' intent (read the spec section for domain code), then commit.
2. Run the full gate on the merged tree. When red, fix the cause and re-run; merge nothing until green.
3. `git push`, then `gh pr merge --squash`. The PR title becomes the squash commit, and the remote branch is deleted on merge.

Done when `gh pr view --json state` shows `MERGED`.
