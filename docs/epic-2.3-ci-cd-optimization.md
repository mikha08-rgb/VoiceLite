# Epic 2.3: CI/CD Optimization

**Status**: 🚀 IN PROGRESS
**Goal**: Leverage Epic 2.2 test categorization for faster CI/CD feedback (5x speedup on PR builds)
**Priority**: HIGH (developer productivity, faster PR cycle)
**Created**: 2025-10-11

## Problem Statement

Current GitHub Actions PR workflow runs all 540 tests (including slow hardware/fileIO tests), taking 2-3 minutes per build. With Epic 2.2's test categorization, we can run only Unit tests (~399 tests, ~27s) on every PR, and run the full suite nightly or on-demand.

**Impact**:
- PR builds take 2-3 minutes (slow feedback)
- Developers wait for full test suite on every commit
- No separation between fast/slow tests in CI
- Wasted CI minutes on redundant full suite runs

## Goals

1. **Fast PR Builds**: Run Unit tests only (~27s, 5-6x faster)
2. **Nightly Full Suite**: Run Hardware + FileIO tests on schedule
3. **Timing Metrics**: Show test runtime in CI output
4. **Documentation**: Update PR template with test guidance

## Success Criteria

- ✅ PR builds complete in <2 minutes (down from 2-3 minutes)
- ✅ Unit tests (399) run on every PR commit
- ✅ Full suite (540) runs nightly at 2 AM UTC
- ✅ CI output shows timing breakdown by category
- ✅ PR template guides contributors on test expectations

## Current State

**PR Workflow** (`.github/workflows/pr-tests.yml`):
- Runs all 540 tests on every PR
- Takes 2-3 minutes (including setup)
- No filtering by category
- No timing metrics

**Test Categories** (from Epic 2.2):
- Unit: 399 tests (~27s)
- Hardware: 57 tests (~5-8s)
- FileIO: 33 tests (~3-5s)
- Integration: 17 tests (skipped)
- UI: 13 tests (skipped)

## Stories

### Story 2.3.1: Update PR Workflow for Fast Tests
**Objective**: Modify PR workflow to run Unit tests only
**Tasks**:
- [ ] Read `.github/workflows/pr-tests.yml`
- [ ] Update test command to `--filter "Category=Unit"`
- [ ] Add timing output with `--logger "console;verbosity=normal"`
- [ ] Test on feature branch before merging

**Acceptance Criteria**:
- PR builds run 399 Unit tests only
- Build time <2 minutes total
- Console shows test timing

### Story 2.3.2: Create Nightly Full Suite Workflow
**Objective**: Run complete test suite nightly
**Tasks**:
- [ ] Create `.github/workflows/nightly-tests.yml`
- [ ] Schedule for 2 AM UTC daily
- [ ] Run all categories (Unit + Hardware + FileIO)
- [ ] Add failure notifications

**Acceptance Criteria**:
- Nightly job runs all 489 categorized tests
- Scheduled for 2 AM UTC
- Sends notification on failure

### Story 2.3.3: Add Manual Full Suite Trigger
**Objective**: Allow on-demand full suite runs
**Tasks**:
- [ ] Add `workflow_dispatch` trigger to PR workflow
- [ ] Add option to select test category (Unit, All, Hardware, FileIO)
- [ ] Update documentation

**Acceptance Criteria**:
- Developers can manually trigger full suite from GitHub UI
- Workflow accepts category parameter

### Story 2.3.4: Add Timing Metrics to CI Output
**Objective**: Show test runtime breakdown
**Tasks**:
- [ ] Add timing summary to workflow output
- [ ] Group test results by category
- [ ] Show total runtime and per-category runtime

**Acceptance Criteria**:
- CI output shows timing per category
- Total runtime displayed prominently

### Story 2.3.5: Update Documentation
**Objective**: Document new CI workflow
**Tasks**:
- [ ] Update PR template with test expectations
- [ ] Update CLAUDE.md with CI workflow info
- [ ] Create Epic 2.3 completion report
- [ ] Document workflow trigger options

**Acceptance Criteria**:
- All documentation updated
- PR template guides contributors
- Epic 2.3 completion report created

## Timeline

- **Story 2.3.1**: 30 minutes (update PR workflow)
- **Story 2.3.2**: 30 minutes (create nightly workflow)
- **Story 2.3.3**: 20 minutes (add manual trigger)
- **Story 2.3.4**: 20 minutes (timing metrics)
- **Story 2.3.5**: 20 minutes (documentation)

**Total Estimated Effort**: 2 hours

## Risks and Mitigations

**Risk**: Breaking existing PR checks
**Mitigation**: Test workflow changes on feature branch first, verify all tests pass

**Risk**: Missing critical bugs by skipping Hardware/FileIO on PR
**Mitigation**: Nightly full suite catches issues, manual trigger available

**Risk**: Developers confused by new workflow
**Mitigation**: Clear documentation in PR template and CLAUDE.md

## Dependencies

- Epic 2.2 (Integration Test Optimization) - ✅ Complete
- GitHub Actions access - ✅ Available
- Test categorization - ✅ Complete (489/540 tests)

## Related Documents

- [Epic 2.2 Completion Report](epic-2.2-completion-report.md)
- [.github/workflows/pr-tests.yml](../.github/workflows/pr-tests.yml)
- [CLAUDE.md](../CLAUDE.md)

## Expected Impact

### Before Epic 2.3
- PR build time: 2-3 minutes
- CI minutes/month: ~300-400 (100 PRs × 3 minutes)
- Developer wait time: 2-3 minutes per commit

### After Epic 2.3
- PR build time: <2 minutes (Unit tests + setup)
- CI minutes/month: ~200 PRs + 30 nightly = ~250 minutes
- Developer wait time: <2 minutes per commit
- **Savings**: ~40% CI minutes, ~30% faster feedback

## Notes

- This epic leverages Epic 2.2's test categories without adding new tests
- Test count remains 540 (unchanged)
- Pass rate should remain 99.8% (539/540 passing)
- Focus is on workflow optimization, not code changes
