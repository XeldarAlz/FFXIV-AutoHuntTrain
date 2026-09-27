## What

<!-- One or two sentences on what this PR changes. -->

## Why

<!-- The motivating problem, linked issue, or user-visible behavior this fixes. -->

Closes #

## How to test

<!--
Minimum steps a reviewer can run to verify the change. Testing usually means setting a conductor on a live train (or injecting an announcement with /aht inject), pressing Start, and watching the plugin reach at least one flag and land a hit on the mark. Make sure the dependencies listed in /aht deps are installed first. For UI-only changes, describe what to click.
-->

## Checklist

- [ ] `dotnet build -c Release` passes
- [ ] Verified in-game on at least one train, or a hand-followed conductor, in the affected expansion
- [ ] If this changes user-visible behavior, README is updated
- [ ] If this touches the automation loop, relevant `[AutoHuntTrain]` log lines make the sequence auditable
