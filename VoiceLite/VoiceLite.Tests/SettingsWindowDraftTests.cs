using System;
using System.Collections.Generic;
using System.Windows.Input;
using AwesomeAssertions;
using VoiceLite.Models;
using Xunit;

namespace VoiceLite.Tests
{
    /// <summary>
    /// Regression tests for the v2.4.1 settings-draft fix. SettingsWindowNew edits a
    /// CLONE of the live settings (CloneForDraft) and copies it back only on Save/Apply
    /// (CommitDraft). These statics ARE the production draft mechanism, so the contracts
    /// under test are exactly the window's: Cancel (= never committing) leaves live
    /// settings and the registered hotkey untouched; Save updates them; immediate-mode
    /// actions (model install via the headless installer, dictation history) write the
    /// live instance directly and must survive a later commit unclobbered.
    /// </summary>
    public class SettingsWindowDraftTests
    {
        private static Settings MakeLiveSettings() => new Settings
        {
            RecordHotkey = Key.F1,
            HotkeyModifiers = ModifierKeys.Control,
            Mode = RecordMode.PushToTalk,
            TranscriptionPreset = TranscriptionPreset.Balanced,
            EnableVAD = true,
            AutoPaste = true,
            MinimizeToTray = true,
            CheckForUpdates = true,
            SelectedMicrophoneIndex = -1,
            TranslationSourceLanguage = "es",
            CustomShortcuts = new List<CustomShortcut>
            {
                new CustomShortcut { Trigger = "my email", Replacement = "misha@example.com" },
            },
        };

        // ---- Cancel contract: an uncommitted draft never leaks into live settings ----

        [Fact]
        public void EditingTheDraftHotkey_DoesNotTouchLiveSettings()
        {
            var live = MakeLiveSettings();
            var draft = SettingsWindowNew.CloneForDraft(live);

            // The window writes hotkey edits to the draft on focus loss. Cancel simply
            // discards the draft — this is the "Cancel didn't cancel" regression.
            draft.RecordHotkey = Key.F9;
            draft.HotkeyModifiers = ModifierKeys.Alt;

            live.RecordHotkey.Should().Be(Key.F1,
                "an uncommitted hotkey edit must never reach the live settings (or disagree " +
                "with the registered hotkey, which is re-registered from live settings only)");
            live.HotkeyModifiers.Should().Be(ModifierKeys.Control);
        }

        [Fact]
        public void EditingDraftCollections_DoesNotTouchLiveSettings()
        {
            var live = MakeLiveSettings();
            var draft = SettingsWindowNew.CloneForDraft(live);

            draft.CustomShortcuts.Add(new CustomShortcut { Trigger = "brb", Replacement = "be right back" });
            draft.CustomDictionary.Add(new CustomDictionaryEntry { Spoken = "github", Written = "GitHub" });

            live.CustomShortcuts.Should().HaveCount(1,
                "the draft must own independent collection instances");
            live.CustomDictionary.Should().BeEmpty();
        }

        [Fact]
        public void CloneForDraft_CopiesTheCurrentValues_AndHasItsOwnSyncRoot()
        {
            var live = MakeLiveSettings();

            var draft = SettingsWindowNew.CloneForDraft(live);

            draft.RecordHotkey.Should().Be(Key.F1);
            draft.HotkeyModifiers.Should().Be(ModifierKeys.Control);
            draft.Mode.Should().Be(RecordMode.PushToTalk);
            draft.CustomShortcuts.Should().ContainSingle(s => s.Trigger == "my email");
            draft.SyncRoot.Should().NotBeSameAs(live.SyncRoot,
                "locking the draft must never contend with services locking the live instance");
        }

        // ---- Save contract: committing copies the edited fields to live ----

        [Fact]
        public void CommitDraft_CopiesEditedFieldsToLive()
        {
            var live = MakeLiveSettings();
            var draft = SettingsWindowNew.CloneForDraft(live);
            draft.RecordHotkey = Key.F9;
            draft.HotkeyModifiers = ModifierKeys.Alt;
            draft.Mode = RecordMode.Toggle;
            draft.TranscriptionPreset = TranscriptionPreset.Speed;
            draft.EnableVAD = false;
            draft.AutoPaste = false;
            draft.SelectedMicrophoneIndex = 2;
            draft.SelectedMicrophoneName = "USB Mic";
            draft.CustomShortcuts.Add(new CustomShortcut { Trigger = "brb", Replacement = "be right back" });
            draft.CustomDictionary.Add(new CustomDictionaryEntry { Spoken = "github", Written = "GitHub" });

            SettingsWindowNew.CommitDraft(draft, live);

            live.RecordHotkey.Should().Be(Key.F9,
                "Save must hand MainWindow the new hotkey so it re-registers it");
            live.HotkeyModifiers.Should().Be(ModifierKeys.Alt);
            live.Mode.Should().Be(RecordMode.Toggle);
            live.TranscriptionPreset.Should().Be(TranscriptionPreset.Speed);
            live.EnableVAD.Should().BeFalse();
            live.AutoPaste.Should().BeFalse();
            live.SelectedMicrophoneIndex.Should().Be(2);
            live.SelectedMicrophoneName.Should().Be("USB Mic");
            live.CustomShortcuts.Should().HaveCount(2);
            live.CustomDictionary.Should().ContainSingle(e => e.Written == "GitHub");
        }

        [Fact]
        public void CommitDraft_CollectionsDoNotShareInstancesWithTheDraft()
        {
            var live = MakeLiveSettings();
            var draft = SettingsWindowNew.CloneForDraft(live);

            SettingsWindowNew.CommitDraft(draft, live);

            // After an Apply the dialog stays open and the draft stays editable —
            // post-Apply edits must not leak into live through shared lists.
            draft.CustomShortcuts.Add(new CustomShortcut { Trigger = "leak", Replacement = "leak" });
            live.CustomShortcuts.Should().HaveCount(1,
                "committed collections must be deep copies, not shared references");
        }

        // ---- Immediate-mode actions bypass the draft and must survive a commit ----

        [Fact]
        public void ModelInstalledWhileDraftOpen_SurvivesCancel_AndSurvivesCommit()
        {
            var live = MakeLiveSettings();
            var draft = SettingsWindowNew.CloneForDraft(live);

            // The model installer (ModelDownloadControl → headless ModelInstaller) is
            // handed the LIVE instance and writes it immediately.
            live.TranscriptionModel = "freshly-installed-model";

            // Cancel = no commit: trivially unaffected.
            live.TranscriptionModel.Should().Be("freshly-installed-model");

            // Save = commit of a draft whose snapshot predates the install: the whitelist
            // must not clobber the installed model with the stale draft value.
            SettingsWindowNew.CommitDraft(draft, live);
            live.TranscriptionModel.Should().Be("freshly-installed-model",
                "a model install performed while the settings draft is open is immediate " +
                "and must not be reverted by Save or Cancel");
        }

        [Fact]
        public void HistoryAndLicenseChangedWhileDraftOpen_SurviveCommit()
        {
            var live = MakeLiveSettings();
            var draft = SettingsWindowNew.CloneForDraft(live);

            // The global hotkey still works over the modal dialog: a dictation adds to
            // live history. License activation writes the live instance immediately.
            live.TranscriptionHistory.Add(new TranscriptionHistoryItem
            {
                Text = "dictated over the settings dialog",
                Timestamp = DateTime.Now,
            });
            live.IsProLicense = true;

            SettingsWindowNew.CommitDraft(draft, live);

            live.TranscriptionHistory.Should().ContainSingle(
                h => h.Text == "dictated over the settings dialog",
                "runtime-mutated state must never be clobbered by the stale draft snapshot");
            live.IsProLicense.Should().BeTrue(
                "license activation is immediate-mode and outside the draft whitelist");
        }
    }
}
