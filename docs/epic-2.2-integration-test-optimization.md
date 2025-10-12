# Epic 2.2: Integration Test Optimization

**Status**: 🚀 IN PROGRESS
**Goal**: Reduce integration test runtime from 2-3 minutes to <30 seconds for fast feedback loop
**Priority**: HIGH (improves developer productivity)
**Created**: 2025-10-11

## Problem Statement

Current integration tests (~40 tests) take 2-3 minutes to run, slowing down the TDD feedback loop. Many tests require real hardware (microphone, Whisper.exe) and cannot run in CI, but all tests run sequentially regardless of category.

**Impact**:
- Developers wait 2-3 minutes for test feedback
- CI runs skip integration tests entirely (no coverage)
- No fast/slow test separation
- Poor developer experience

## Goals

1. **Fast Feedback Loop**: Unit tests complete in <5 seconds
2. **Integration Test Categories**: Split into Fast (30s) and Slow (2-3min)
3. **CI Optimization**: Run fast tests on every commit, slow tests nightly
4. **Documentation**: Clear guidance on test categories

## Success Criteria

- ✅ Fast tests (unit + fast integration) run in <30 seconds
- ✅ Slow tests (hardware-dependent) run in <5 minutes
- ✅ CI runs fast tests on every PR
- ✅ Nightly CI runs full test suite
- ✅ Test categorization documented

## Current State

**Test Count**: 540 total tests
**Pass Rate**: 99.8% (539/540 passing)
**Runtime**: ~2-3 minutes for full suite
**Integration Tests**: ~40 tests (marked with `[Skip]` or `[Category=Integration]`)

**Integration Test Distribution**:
- AudioRecorderTests: 15 integration tests (microphone required)
- WhisperServiceTests: 4 integration tests (Whisper.exe + audio required)
- SystemTrayManagerTests: 13 integration tests (WPF UI thread required)
- MemoryLeakTest: 3 integration tests (MainWindow instantiation required)
- AudioPreprocessorTests: 5 integration tests (audio file processing)

## Stories

### Story 2.2.1: Analyze Current Test Structure ✅ IN PROGRESS
**Objective**: Understand current test runtime distribution
**Tasks**:
- [x] Run full test suite with timing analysis
- [ ] Identify slowest 10 tests
- [ ] Categorize tests by runtime (fast <100ms, medium <1s, slow >1s)
- [ ] Document findings

**Acceptance Criteria**:
- Test runtime distribution documented
- Slow tests identified and categorized

### Story 2.2.2: Create Test Category System
**Objective**: Define test categories for filtering
**Tasks**:
- [ ] Define test categories (Unit, FastIntegration, SlowIntegration, Hardware)
- [ ] Add `[Trait]` attributes to all tests
- [ ] Update test documentation

**Acceptance Criteria**:
- All 540 tests categorized
- Filter commands documented

### Story 2.2.3: Split Integration Tests
**Objective**: Separate fast integration tests from slow
**Tasks**:
- [ ] Move hardware-dependent tests to `[Category=Hardware]`
- [ ] Move WPF UI tests to `[Category=UI]`
- [ ] Keep fast integration tests in `[Category=FastIntegration]`
- [ ] Verify all tests still pass

**Acceptance Criteria**:
- Tests can be filtered by category
- Fast tests run in <30 seconds
- Slow tests run separately

### Story 2.2.4: Optimize CI Workflow
**Objective**: Update GitHub Actions for fast feedback
**Tasks**:
- [ ] Create fast test job (Unit + FastIntegration)
- [ ] Create slow test job (Hardware + UI) - nightly only
- [ ] Add timing metrics to CI output
- [ ] Update PR test workflow

**Acceptance Criteria**:
- PR builds run fast tests (<2 minutes total)
- Nightly builds run full suite
- CI shows timing breakdown

### Story 2.2.5: Documentation and Cleanup
**Objective**: Document test optimization and new workflow
**Tasks**:
- [ ] Update CLAUDE.md with test commands
- [ ] Create developer guide for test categories
- [ ] Add timing benchmarks to Epic 2.2 report
- [ ] Update PR template with test guidance

**Acceptance Criteria**:
- All documentation updated
- Epic 2.2 completion report created

## Timeline

- **Story 2.2.1**: 2 hours (analysis)
- **Story 2.2.2**: 1 hour (categorization)
- **Story 2.2.3**: 2 hours (refactoring)
- **Story 2.2.4**: 2 hours (CI workflow)
- **Story 2.2.5**: 1 hour (documentation)

**Total Estimated Effort**: 8 hours

## Risks and Mitigations

**Risk**: Test categorization errors (wrong category)
**Mitigation**: Run full suite after categorization, verify all tests pass

**Risk**: CI workflow breaks existing PR checks
**Mitigation**: Test workflow changes on feature branch first

**Risk**: Timing targets not achieved
**Mitigation**: Profile slow tests, optimize or move to separate category

## Dependencies

- Epic 2.1 (Service Layer Test Coverage) - ✅ Complete
- xUnit test framework - ✅ Available
- GitHub Actions CI - ✅ Available

## Related Documents

- [Epic 2.1 Completion Report](epic-2.1-completion-report.md)
- [Epic 2.1 Progress Summary](epic-2.1-progress-summary.md)
- [.github/workflows/pr-tests.yml](../.github/workflows/pr-tests.yml)
- [CLAUDE.md](../CLAUDE.md)

## Notes

- This epic focuses on **workflow optimization**, not new test creation
- Test count should remain ~540 (no new tests added)
- Pass rate should remain 99.8% (539/540 passing)
- Epic 2.1's coverage gains are preserved (~80-85% Services/)
