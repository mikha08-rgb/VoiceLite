# VoiceLite Critical Paths - Exact Code Flow

**Generated**: 2025-10-08
**Purpose**: Trace the exact code execution paths for critical operations with file:line references

---

## Path 1: Hotkey Press → Audio Recording

### Trigger: User Presses Left Alt

**Entry**: [HotkeyManager.cs:180](VoiceLite/VoiceLite/Services/HotkeyManager.cs#L180)

```
HotkeyManager.StartPollingForModifierKey(Key key)
  │
  ├─► Line 192: Task.Run(() => { ... })  // Background polling thread
  │
  ├─► Line 198: GetAsyncKeyState(vKey) & 0x8000  // Win32 API call every 15ms
  │
  ├─► Line 204: isKeyDown = true  // State change under lock
  │
  └─► Line 218: RunOnDispatcher(() => HotkeyPressed?.Invoke(this, EventArgs.Empty))
       │
       └─► Dispatches to UI thread
```

**Handler**: [MainWindow.xaml.cs:1093](VoiceLite/VoiceLite/MainWindow.xaml.cs#L1093)

```
MainWindow.OnHotkeyPressed(object? sender, EventArgs e)
  │
  ├─► Line 1102: Debounce check (50ms minimum)
  │    if ((now - lastHotkeyPressTime).TotalMilliseconds < 50)
  │        return;  // Ignore rapid presses
  │
  ├─► Line 1109: lock (recordingLock)  // CRITICAL: Thread safety
  │
  ├─► Line 1113: Check settings.Mode
  │    ├─ PushToTalk → HandlePushToTalkPressed()
  │    └─ Toggle → HandleToggleModePressed()
  │
  └─► Line 1259: StartRecording()  // MainWindow method
       │
       └─► [MainWindow.xaml.cs:976] MainWindow.StartRecording()
            │
            ├─► Line 983: lock (recordingLock)  // v1.0.57 CRITICAL FIX
            │
            ├─► Line 990: Validate audioRecorder != null
            │
            ├─► Line 994: if (recorder.IsRecording) return;  // Guard against race
            │
            ├─► Line 1001: if (IsRecording) return;  // Pre-check via coordinator
            │
            ├─► Line 1024: recordingCoordinator?.StartRecording()
            │    │
            │    └─► [RecordingCoordinator.cs:85]
            │         │
            │         ├─► Line 93: if (!stateMachine.TryTransition(RecordingState.Recording))
            │         │             // CRITICAL: State machine prevents invalid transitions
            │         │             // Idle → Recording (valid)
            │         │             // Recording → Recording (invalid, rejected)
            │         │
            │         ├─► Line 98: Force-reset recovery if stuck
            │         │    if (stateMachine.CurrentState != RecordingState.Idle) {
            │         │        stateMachine.Reset();  // Force back to Idle
            │         │    }
            │         │
            │         ├─► Line 123: audioRecorder.StartRecording()
            │         │    │
            │         │    └─► [AudioRecorder.cs:234]
            │         │         │
            │         │         ├─► Line 238: lock (lockObject)
            │         │         │
            │         │         ├─► Line 258: DisposeWaveInCompletely()
            │         │         │    // CRITICAL: Always dispose old device first
            │         │         │    // Prevents audio buffer cross-contamination
            │         │         │
            │         │         ├─► Line 268: waveInInstanceId++
            │         │         │    currentRecordingInstanceId = waveInInstanceId
            │         │         │    // Instance ID tracking prevents late callbacks
            │         │         │
            │         │         ├─► Line 272: waveIn = new WaveInEvent {
            │         │         │        WaveFormat = new WaveFormat(16000, 16, 1),  // Whisper format
            │         │         │        BufferMilliseconds = 30,  // Low latency
            │         │         │        NumberOfBuffers = 3
            │         │         │    }
            │         │         │
            │         │         ├─► Line 281: waveIn.DataAvailable += OnDataAvailable
            │         │         │    // Event fires every 30ms with audio chunks
            │         │         │
            │         │         ├─► Line 288: audioMemoryStream = new MemoryStream()
            │         │         │    waveFile = new WaveFileWriter(audioMemoryStream, waveIn.WaveFormat)
            │         │         │    // Memory buffer mode (no disk I/O)
            │         │         │
            │         │         └─► Line 292: waveIn.StartRecording()
            │         │              isRecording = true
            │         │
            │         └─► Line 126: StatusChanged?.Invoke(this, new RecordingStatusEventArgs {
            │                  Status = "Recording",
            │                  IsRecording = true
            │              })
            │
            └─► Line 1040: recordingElapsedTimer?.Start()
                 // UI timer for elapsed time display
```

---

## Path 2: Audio Capture Loop (30ms intervals)

**Trigger**: NAudio fires DataAvailable event every 30ms

**Entry**: [AudioRecorder.cs:311](VoiceLite/VoiceLite/Services/AudioRecorder.cs#L311)

```
AudioRecorder.OnDataAvailable(object? sender, WaveInEventArgs e)
  │
  ├─► Line 317: if (!isRecording) return;  // Pre-lock check
  │
  ├─► Line 324: lock (lockObject)  // CRITICAL: Protect state
  │
  ├─► Line 331: callbackInstanceId = currentRecordingInstanceId
  │    // Capture instance ID under lock
  │
  ├─► Line 338: if (senderWaveIn != waveIn) return;
  │    // Object reference check (prevents callbacks from disposed devices)
  │
  ├─► Line 347: if (callbackInstanceId != currentRecordingInstanceId) return;
  │    // TIER 1.1: Instance ID check (prevents stale callbacks from previous sessions)
  │    // CRITICAL: NAudio may fire late callbacks with buffered data after dispose
  │
  ├─► Line 368: WaveFileWriter? localWaveFile = waveFile
  │    if (localWaveFile == null || !isRecording || e.BytesRecorded <= 0) return;
  │
  ├─► Line 377: buffer = ArrayPool<byte>.Shared.Rent(e.BytesRecorded)
  │    // CRIT-004 FIX: Rent buffer inside try block
  │
  ├─► Line 379: Array.Copy(e.Buffer, buffer, e.BytesRecorded)
  │
  ├─► Line 384: Apply volume scaling (0.8x)
  │    for (int i = 0; i < pairCount * 2; i += 2) {
  │        short sample = (short)(buffer[i] | (buffer[i + 1] << 8));
  │        int scaled = (int)Math.Round(sample * 0.8f);
  │        scaled = Math.Clamp(scaled, short.MinValue, short.MaxValue);
  │        buffer[i] = (byte)(scaled & 0xFF);
  │        buffer[i + 1] = (byte)((scaled >> 8) & 0xFF);
  │    }
  │
  ├─► Line 392: localWaveFile.Write(buffer, 0, pairCount * 2)
  │    // Write to memory stream (no disk I/O)
  │
  └─► Line 406: ArrayPool<byte>.Shared.Return(buffer, clearArray: true)
       finally {  // ALWAYS returns buffer, even on exception
           // clearArray: true for security (prevents audio data leakage)
       }
```

**Loop continues every 30ms until StopRecording() is called.**

---

## Path 3: Hotkey Release → Transcription Start

### Trigger: User Releases Left Alt

**Entry**: [HotkeyManager.cs:410](VoiceLite/VoiceLite/Services/HotkeyManager.cs#L410)

```
HotkeyManager.StartReleaseMonitoring()
  │
  ├─► Line 423: Task.Run(() => { ... })  // Background thread
  │
  ├─► Line 429: Poll GetAsyncKeyState() every 10ms
  │
  ├─► Line 434: if (!keyPressed && isKeyDown) {
  │        isKeyDown = false;
  │        raiseRelease = true;
  │    }
  │
  └─► Line 443: RunOnDispatcher(() => HotkeyReleased?.Invoke(this, EventArgs.Empty))
       │
       └─► [MainWindow.xaml.cs:1146]
            │
            MainWindow.OnHotkeyReleased(object? sender, EventArgs e)
            │
            ├─► Line 1183: if (!IsRecording) return;
            │
            ├─► Line 1292: StopRecording(false)  // false = don't cancel
            │    │
            │    └─► [MainWindow.xaml.cs:1055]
            │         │
            │         MainWindow.StopRecording(bool cancel = false)
            │         │
            │         ├─► Line 1060: if (!IsRecording) return;
            │         │
            │         ├─► Line 1071: recordingCoordinator?.StopRecording(cancel)
            │         │    │
            │         │    └─► [RecordingCoordinator.cs:163]
            │         │         │
            │         │         RecordingCoordinator.StopRecording(bool cancel)
            │         │         │
            │         │         ├─► Line 172: State transition
            │         │         │    Recording → Stopping (normal)
            │         │         │    Recording → Cancelled (if cancel=true)
            │         │         │
            │         │         ├─► Line 185: audioRecorder.StopRecording()
            │         │         │    │
            │         │         │    └─► [AudioRecorder.cs:452]
            │         │         │         │
            │         │         │         AudioRecorder.StopRecording()
            │         │         │         │
            │         │         │         ├─► Line 454: lock (lockObject)
            │         │         │         │
            │         │         │         ├─► Line 461: isRecording = false  // IMMEDIATELY
            │         │         │         │    // Rejects all incoming OnDataAvailable callbacks
            │         │         │         │
            │         │         │         ├─► Line 470: waveFile.Flush()
            │         │         │         │    waveFile.Dispose()
            │         │         │         │    // Finalize WAV headers
            │         │         │         │
            │         │         │         ├─► Line 481: var audioData = audioMemoryStream.ToArray()
            │         │         │         │    // Get complete WAV file from memory
            │         │         │         │
            │         │         │         ├─► Line 486: audioMemoryStream.Dispose()
            │         │         │         │    audioMemoryStream = null
            │         │         │         │
            │         │         │         ├─► Line 499: AudioDataReady?.Invoke(this, audioData)
            │         │         │         │    // CRITICAL: Fires event with audio bytes
            │         │         │         │    // Triggers RecordingCoordinator.OnAudioFileReady()
            │         │         │         │
            │         │         │         └─► Line 532: waveIn.Dispose()
            │         │         │              waveIn = null  // No more audio capture
            │         │         │
            │         │         └─► Line 198: StartStoppingTimeoutTimer()
            │         │              // 10-second safety timer
            │         │              // Recovers if AudioFileReady never fires
            │         │
            │         └─► Line 1073: isRecording = false  // UI state
            │
            └─► UI updates (status text, button states)
```

---

## Path 4: Transcription Pipeline

**Trigger**: `AudioRecorder.AudioDataReady` event fires

**Entry**: [RecordingCoordinator.cs:246](VoiceLite/VoiceLite/Services/RecordingCoordinator.cs#L246)

```
RecordingCoordinator.OnAudioFileReady(object? sender, string audioFilePath)
  │
  ├─► Line 252: StopStoppingTimeoutTimer()
  │    // Safety timer no longer needed (event fired successfully)
  │
  ├─► Line 256: if (isDisposed) return;
  │    // Disposal safety check
  │
  ├─► Line 264: var currentState = stateMachine.CurrentState
  │
  ├─► Line 267: if (currentState == RecordingState.Cancelled) {
  │        await CleanupAudioFileAsync(audioFilePath);
  │        return;  // Skip transcription
  │    }
  │
  └─► Line 279: await ProcessAudioFileAsync(audioFilePath)
       │
       └─► [RecordingCoordinator.cs:314]
            │
            RecordingCoordinator.ProcessAudioFileAsync(string audioFilePath)
            │
            ├─► Line 320: Pre-flight health checks
            │    ├─ File exists? (Line 321)
            │    ├─ File size > 100 bytes? (Line 328)
            │    └─ Whisper service available? (Line 335)
            │
            ├─► Line 359: State transition Stopping → Transcribing
            │    if (!stateMachine.TryTransition(RecordingState.Transcribing)) return;
            │
            ├─► Line 367: StatusChanged?.Invoke(this, "Transcribing")
            │
            ├─► Line 374: StartTranscriptionWatchdog()
            │    // 120-second timeout with 10-second interval checks
            │    // Atomic flag prevents double-fire on completion
            │
            ├─► Line 377: transcriptionComplete.Reset()
            │    // WEEK1-DAY2: ManualResetEventSlim for disposal wait
            │
            ├─► Line 390: RETRY LOOP (max 3 attempts)
            │    for (int attempt = 1; attempt <= 3; attempt++) {
            │        try {
            │            transcription = await Task.Run(async () =>
            │                await whisperService.TranscribeAsync(audioFilePath)
            │            );
            │            break;  // Success
            │        }
            │        catch (Exception retryEx) when (attempt < maxRetries) {
            │            // Smart retry logic based on error type
            │            if (retryEx is FileNotFoundException) throw;  // Don't retry
            │            if (retryEx is TimeoutException) throw;
            │            if (retryEx is IOException) await Task.Delay(0);  // Retry immediately
            │            else await Task.Delay(500 * attempt);  // Exponential backoff
            │        }
            │    }
            │    │
            │    └─► [PersistentWhisperService.cs:321]
            │         │
            │         PersistentWhisperService.TranscribeAsync(string audioFilePath)
            │         │
            │         ├─► Line 345: await transcriptionSemaphore.WaitAsync()
            │         │    semaphoreAcquired = true;
            │         │    // CRITICAL: Only 1 transcription at a time
            │         │
            │         ├─► Line 362: if (!isWarmedUp) await WarmUpWhisperAsync()
            │         │    // First-run warmup (loads model into OS cache)
            │         │
            │         ├─► Line 372: Build command arguments
            │         │    arguments = "-m \"{model}\" -f \"{audio}\" " +
            │         │               "--no-timestamps --language {lang} " +
            │         │               "--beam-size {beam} --best-of {best}"
            │         │
            │         ├─► Line 393: process = new Process { StartInfo = ... }
            │         │    process.Start()
            │         │
            │         ├─► Line 417: Track process ID (zombie detection)
            │         │    lock (processLock) {
            │         │        activeProcessIds.Add(process.Id);
            │         │    }
            │         │
            │         ├─► Line 427: process.PriorityClass = ProcessPriorityClass.Normal
            │         │    // PERFORMANCE FIX: Prevent UI thread starvation
            │         │
            │         ├─► Line 438: Smart timeout calculation
            │         │    if (!isWarmedUp) {
            │         │        timeoutSeconds = 180;  // First run: 3 minutes
            │         │    } else {
            │         │        // Based on file size + model multiplier
            │         │        estimatedAudioSeconds = fileInfo.Length / 32000.0;
            │         │        processingMultiplier = settings.WhisperModel switch {
            │         │            "ggml-tiny.bin" => 2.0,
            │         │            "ggml-small.bin" => 5.0,  // Default
            │         │            "ggml-large-v3.bin" => 20.0,
            │         │            _ => 5.0
            │         │        };
            │         │        timeoutSeconds = Math.Max(10, estimatedAudioSeconds * processingMultiplier + 5);
            │         │        timeoutSeconds = (int)(timeoutSeconds * settings.WhisperTimeoutMultiplier);
            │         │        timeoutSeconds = Math.Min(timeoutSeconds, 600);  // Max 10 minutes
            │         │    }
            │         │
            │         ├─► Line 477: Wait for process with timeout
            │         │    bool exited = await Task.Run(() => process.WaitForExit(timeoutSeconds * 1000))
            │         │
            │         ├─► Line 479: if (!exited) {
            │         │        // TIMEOUT: Kill process tree
            │         │        process.Kill(entireProcessTree: true);
            │         │
            │         │        // CRIT-005 FIX: Non-blocking termination
            │         │        var waitTask = Task.Run(() => process.WaitForExit(5000));
            │         │        if (!waitTask.Wait(6000)) {
            │         │            // Last resort: taskkill.exe (fire-and-forget)
            │         │            Process.Start("taskkill", $"/F /T /PID {process.Id}");
            │         │        }
            │         │
            │         │        throw new TimeoutException(...);
            │         │    }
            │         │
            │         ├─► Line 593: Parse output
            │         │    var result = outputBuilder.ToString();
            │         │    // Remove [whisper] system messages
            │         │    foreach (var line in lines) {
            │         │        if (!line.StartsWith("[") && !line.Contains("whisper_")) {
            │         │            cleanedResult.AppendLine(line.Trim());
            │         │        }
            │         │    }
            │         │
            │         ├─► Line 611: Post-process transcription
            │         │    result = TranscriptionPostProcessor.ProcessTranscription(
            │         │        result,
            │         │        settings.UseEnhancedDictionary,
            │         │        customDict,
            │         │        settings.PostProcessing
            │         │    );
            │         │    │
            │         │    └─► [TranscriptionPostProcessor.cs:189]
            │         │         │
            │         │         TranscriptionPostProcessor.ProcessTranscription(...)
            │         │         │
            │         │         ├─► Line 215: Custom dictionary replacements
            │         │         │    // VoiceShortcuts: User-defined text replacements
            │         │         │
            │         │         ├─► Line 232: Enhanced dictionary (70+ tech terms)
            │         │         │    result = GetHubRegex.Replace(result, "GitHub");
            │         │         │    result = YouStateRegex.Replace(result, "useState");
            │         │         │    result = PackageJasonRegex.Replace(result, "package.json");
            │         │         │    // ... 60+ more regex replacements
            │         │         │
            │         │         ├─► Line 330: Capitalization
            │         │         │    if (settings.CapitalizeFirstLetter) {
            │         │         │        result = char.ToUpper(result[0]) + result.Substring(1);
            │         │         │    }
            │         │         │
            │         │         ├─► Line 350: Filler word removal
            │         │         │    // 5 intensity levels: None/Light/Moderate/Aggressive/Custom
            │         │         │    // Categories: Hesitations, Verbal tics, Qualifiers, Intensifiers, Transitions
            │         │         │
            │         │         └─► Line 400: Return processed text
            │         │
            │         └─► Line 620: return result
            │              // Back to RecordingCoordinator
            │
            ├─► Line 451: Track analytics (if enabled)
            │    var wordCount = TextAnalyzer.CountWords(transcription);
            │    await analyticsService.TrackTranscriptionAsync(settings.WhisperModel, wordCount);
            │
            ├─► Line 469: Create history item
            │    historyItem = new TranscriptionHistoryItem {
            │        Timestamp = DateTime.Now,
            │        Text = transcription,
            │        WordCount = wordCount,
            │        DurationSeconds = (DateTime.Now - recordingStartTime).TotalSeconds,
            │        ModelUsed = settings.WhisperModel
            │    };
            │    historyService?.AddToHistory(historyItem);
            │
            ├─► Line 489: State transition Transcribing → Injecting
            │    stateMachine.TryTransition(RecordingState.Injecting);
            │
            ├─► Line 495: Text injection
            │    textInjector.AutoPaste = settings.AutoPaste;
            │    await Task.Run(() => textInjector.InjectText(transcription))
            │    │
            │    └─► [TextInjector.cs:51]
            │         │
            │         TextInjector.InjectText(string text)
            │         │
            │         ├─► Line 68: Decide method
            │         │    if (ShouldUseTyping(text)) {
            │         │        InjectViaTyping(text);
            │         │    } else {
            │         │        InjectViaClipboard(text);
            │         │    }
            │         │    │
            │         │    └─► Line 104: ShouldUseTyping(text)
            │         │         switch (settings.TextInjectionMode) {
            │         │             case SmartAuto:
            │         │                 // Use typing for short text (<50 chars)
            │         │                 // Use typing for passwords (IsInSecureField())
            │         │                 return text.Length < 50 || IsInSecureField();
            │         │         }
            │         │
            │         └─► Clipboard paste (most common)
            │              // 1. Save current clipboard
            │              // 2. Set new clipboard text
            │              // 3. Simulate Ctrl+V
            │              // 4. Restore old clipboard
            │
            ├─► Line 513: State transition Injecting → Complete
            │    stateMachine.TryTransition(RecordingState.Complete);
            │
            ├─► Line 555: Fire TranscriptionCompleted event
            │    TranscriptionCompleted?.Invoke(this, eventArgs);
            │    // MainWindow subscribes to this event and updates UI
            │
            └─► Line 571: State transition Complete → Idle
                 stateMachine.TryTransition(RecordingState.Idle);
                 // Ready for next recording
```

---

## Path 5: Web Backend Authentication (Magic Link)

**Trigger**: User clicks "Login" in desktop app

**Entry**: [LoginWindow.xaml.cs:RequestMagicLink()](VoiceLite/VoiceLite/LoginWindow.xaml.cs)

```
LoginWindow.RequestMagicLink(string email)
  │
  └─► [AuthenticationService.cs:RequestMagicLinkAsync()]
       │
       └─► [ApiClient.cs:PostAsync()]
            │
            └─► POST https://voicelite.app/api/auth/request
                 Body: { email: "user@example.com" }
                 │
                 └─► [voicelite-web/app/api/auth/request/route.ts]
                      │
                      ├─► Line 15: Validate email format
                      │
                      ├─► Line 20: Find or create user
                      │    const user = await prisma.user.upsert({
                      │        where: { email },
                      │        create: { email },
                      │        update: {}
                      │    });
                      │
                      ├─► Line 30: Generate 6-digit OTP
                      │    const otp = Math.floor(100000 + Math.random() * 900000);
                      │    const expiresAt = new Date(Date.now() + 10 * 60 * 1000);  // 10 minutes
                      │
                      ├─► Line 40: Store OTP in database
                      │    await prisma.otp.create({
                      │        data: {
                      │            userId: user.id,
                      │            code: otp.toString(),
                      │            expiresAt
                      │        }
                      │    });
                      │
                      └─► Line 50: Send email via Resend
                           await resend.emails.send({
                               to: email,
                               from: 'VoiceLite <noreply@voicelite.app>',
                               subject: 'Your VoiceLite Login Code',
                               html: `<p>Your code is: <strong>${otp}</strong></p>`
                           });
```

**User receives email with OTP code**

```
LoginWindow.VerifyOTP(string otp)
  │
  └─► [AuthenticationService.cs:VerifyOTPAsync()]
       │
       └─► POST https://voicelite.app/api/auth/otp
            Body: { email, otp }
            │
            └─► [voicelite-web/app/api/auth/otp/route.ts]
                 │
                 ├─► Line 20: Find OTP in database
                 │    const otpRecord = await prisma.otp.findFirst({
                 │        where: {
                 │            user: { email },
                 │            code: otp,
                 │            expiresAt: { gt: new Date() },  // Not expired
                 │            usedAt: null  // Not already used
                 │        }
                 │    });
                 │
                 ├─► Line 35: Mark OTP as used
                 │    await prisma.otp.update({
                 │        where: { id: otpRecord.id },
                 │        data: { usedAt: new Date() }
                 │    });
                 │
                 ├─► Line 45: Create JWT session (httpOnly cookie)
                 │    const session = jwt.sign({
                 │        userId: otpRecord.userId,
                 │        email: otpRecord.user.email
                 │    }, process.env.JWT_SECRET!, {
                 │        expiresIn: '30d'
                 │    });
                 │
                 └─► Line 55: Set cookie and return user data
                      Response.json({
                          user: { id, email },
                          session
                      });
```

**Desktop app is now authenticated, can fetch Pro licenses**

---

## Path 6: License Validation (Ed25519 Signatures)

**Trigger**: Desktop app starts and checks for Pro license

**Entry**: [MainWindow.xaml.cs:LoadLicense()](VoiceLite/VoiceLite/MainWindow.xaml.cs)

```
MainWindow.LoadLicense()
  │
  └─► [LicenseService.cs:LoadLicense()]
       │
       ├─► Line 145: Check local license file
       │    string licensePath = Path.Combine(AppData, "license.dat");
       │    if (!File.Exists(licensePath)) return LicenseStatus.NoLicense;
       │
       ├─► Line 160: If no local license, fetch from backend
       │    var response = await ApiClient.PostAsync<LicensePayload>(
       │        "/api/licenses/issue",
       │        new { DeviceFingerprint = GetDeviceFingerprint() }
       │    );
       │    │
       │    └─► [voicelite-web/app/api/licenses/issue/route.ts]
       │         │
       │         ├─► Line 20: Verify user is authenticated (JWT)
       │         │
       │         ├─► Line 30: Find active license
       │         │    const license = await prisma.license.findFirst({
       │         │        where: {
       │         │            userId: session.userId,
       │         │            status: 'ACTIVE',
       │         │            OR: [
       │         │                { expiresAt: null },  // Lifetime
       │         │                { expiresAt: { gt: new Date() } }  // Not expired
       │         │            ]
       │         │        }
       │         │    });
       │         │
       │         ├─► Line 50: Create license payload
       │         │    const payload = {
       │         │        email: user.email,
       │         │        tier: license.tier,  // 'FREE' | 'PRO'
       │         │        expiresAt: license.expiresAt?.toISOString(),
       │         │        deviceFingerprint: request.deviceFingerprint,
       │         │        issuedAt: new Date().toISOString()
       │         │    };
       │         │
       │         ├─► Line 70: Sign with Ed25519 private key
       │         │    const message = JSON.stringify(payload);
       │         │    const signature = await sign(
       │         │        Buffer.from(message),
       │         │        Buffer.from(process.env.LICENSE_PRIVATE_KEY!, 'hex')
       │         │    );
       │         │
       │         └─► Line 80: Return signed license
       │              Response.json({
       │                  ...payload,
       │                  signature: Buffer.from(signature).toString('base64')
       │              });
       │
       ├─► Line 180: Save license to local file
       │    await File.WriteAllTextAsync(
       │        licensePath,
       │        JsonSerializer.Serialize(licensePayload)
       │    );
       │
       └─► Line 198: Verify signature using embedded public key
            │
            └─► [LicenseService.cs:VerifySignature()]
                 │
                 ├─► Line 203: Load public key from environment or fallback
                 │    var publicKeyBytes = Convert.FromBase64String(ResolvedLicensePublicKey);
                 │    // Public key is embedded in desktop app code (can't be changed)
                 │
                 ├─► Line 212: Deserialize signature
                 │    var signatureBytes = Convert.FromBase64String(license.Signature);
                 │
                 ├─► Line 217: Verify using BouncyCastle Ed25519
                 │    var result = Ed25519.Verify(
                 │        signatureBytes,
                 │        messageBytes,
                 │        publicKeyBytes
                 │    );
                 │
                 └─► Return result (true = valid, false = forged)
                      // If signature valid → License is authentic
                      // If signature invalid → License was tampered with or forged
```

**Result**: Desktop app can now use Pro features (premium models, advanced settings)

---

## Critical State Machine Transitions

**File**: [RecordingStateMachine.cs](VoiceLite/VoiceLite/Services/RecordingStateMachine.cs)

```
State Diagram:

    ┌─────┐
    │Idle │
    └──┬──┘
       │ StartRecording()
       ↓
   ┌──────────┐
   │Recording │
   └────┬─────┘
        │ StopRecording(cancel=false)
        ↓
   ┌────────┐
   │Stopping│
   └────┬───┘
        │ AudioFileReady event
        ↓
  ┌────────────┐
  │Transcribing│
  └─────┬──────┘
        │ Whisper completes
        ↓
   ┌─────────┐
   │Injecting│
   └────┬────┘
        │ Text pasted/typed
        ↓
   ┌────────┐
   │Complete│
   └────┬───┘
        │ Auto-transition
        ↓
    ┌─────┐
    │Idle │
    └─────┘

Error Paths:
  Any State → Error → Idle

Cancel Path:
  Recording → Cancelled → Idle
```

**Validation Rules** (Line 45-80):
```csharp
Valid transitions:
  Idle → Recording
  Recording → Stopping | Cancelled | Error
  Stopping → Transcribing | Error
  Transcribing → Injecting | Error
  Injecting → Complete | Error
  Complete → Idle
  Error → Idle
  Cancelled → Idle

Invalid transitions (rejected):
  Recording → Recording  // Prevents double-start
  Transcribing → Recording  // Can't start while transcribing
  Idle → Transcribing  // Must go through Recording first
```

---

## Watchdog & Recovery Mechanisms

### 1. Transcription Watchdog (120-second timeout)

**File**: [RecordingCoordinator.cs:590](VoiceLite/VoiceLite/Services/RecordingCoordinator.cs#L590)

```
StartTranscriptionWatchdog()
  │
  ├─► Line 593: Interlocked.Exchange(ref transcriptionCompletedFlag, 0)
  │    // Atomic reset (prevents race with normal completion)
  │
  └─► Line 597: new Timer(WatchdogCallback, null, 10s, 10s)
       │
       └─► Line 672: WatchdogCallback()
            │
            ├─► Line 677: if (stateMachine.CurrentState != Transcribing) return;
            │
            ├─► Line 681: var elapsed = DateTime.Now - transcriptionStartTime
            │
            └─► Line 683: if (elapsed.TotalSeconds > 120) {
                     // TIMEOUT!
                     if (Interlocked.CompareExchange(ref transcriptionCompletedFlag, 1, 0) == 0) {
                         // We won the race - fire timeout event
                         TranscriptionCompleted?.Invoke(this, new TranscriptionCompleteEventArgs {
                             Success = false,
                             ErrorMessage = "Transcription timed out..."
                         });
                     }
                 }
```

**CRITICAL**: Atomic flag prevents double-fire (both watchdog and normal completion trying to fire event).

---

### 2. Stopping Timeout (10-second recovery)

**File**: [RecordingCoordinator.cs:728](VoiceLite/VoiceLite/Services/RecordingCoordinator.cs#L728)

```
StartStoppingTimeoutTimer()
  │
  └─► Line 731: new Timer(StoppingTimeoutCallback, null, 10s, Infinite)
       │
       └─► Line 771: StoppingTimeoutCallback()
            │
            ├─► Line 776: if (stateMachine.CurrentState == Stopping) {
            │        // AudioFileReady never fired!
            │        stateMachine.TryTransition(RecordingState.Error);
            │        stateMachine.TryTransition(RecordingState.Idle);
            │
            │        ErrorOccurred?.Invoke(this, "Recording failed - audio file was not ready in time");
            │    }
            │
            └─► Line 791: StopStoppingTimeoutTimer()
```

**Purpose**: Prevents state machine from getting stuck in `Stopping` state if `AudioFileReady` event never fires (disk I/O failure, antivirus blocking, etc.).

---

### 3. Stuck State Watchdog (30-second interval)

**File**: [RecordingCoordinator.cs:640](VoiceLite/VoiceLite/Services/RecordingCoordinator.cs#L640)

```
CheckForStuckStates()  // Runs every 30 seconds
  │
  └─► Line 647: bool wasStuck = stateMachine.CheckForStuckState()
       │
       └─► [RecordingStateMachine.cs:CheckForStuckState()]
            │
            ├─► Line 95: if (CurrentState == Transcribing && ...) {
            │        // Force reset to Idle
            │        Reset();
            │        return true;
            │    }
            │
            └─► Return: true if reset occurred, false otherwise
```

**Purpose**: Periodic health check to detect and recover from stuck states that watchdogs missed.

---

## Memory & Resource Cleanup Paths

### Disposal Chain (App Close)

```
App.OnExit()
  │
  └─► MainWindow.OnClosed()
       │
       ├─► Line 2435: StopRecording(true)  // Cancel any active recording
       │
       ├─► Line 2440: Unsubscribe event handlers
       │    ├─ recordingCoordinator.TranscriptionCompleted -= ...
       │    ├─ recordingCoordinator.StatusChanged -= ...
       │    ├─ recordingCoordinator.ErrorOccurred -= ...
       │    ├─ hotkeyManager.HotkeyPressed -= ...
       │    └─ hotkeyManager.HotkeyReleased -= ...
       │
       ├─► Line 2460: Dispose services
       │    ├─ recordingCoordinator?.Dispose()
       │    │  │
       │    │  └─► [RecordingCoordinator.cs:845]
       │    │       │
       │    │       ├─► Line 852: isDisposed = true
       │    │       │
       │    │       ├─► Line 858: audioRecorder.AudioFileReady -= OnAudioFileReady
       │    │       │    // CRITICAL: Unsubscribe FIRST to prevent new events
       │    │       │
       │    │       ├─► Line 867: if (!transcriptionComplete.Wait(30000))
       │    │       │    // Wait max 30 seconds for transcription to complete
       │    │       │    // WEEK1-DAY2: Efficient signaling vs spin-wait
       │    │       │
       │    │       ├─► Line 877: StopTranscriptionWatchdog()
       │    │       ├─► Line 878: StopStoppingTimeoutTimer()
       │    │       ├─► Line 884: stuckStateWatchdog?.Dispose()
       │    │       │
       │    │       └─► Line 893: transcriptionComplete?.Dispose()
       │    │
       │    ├─ audioRecorder?.Dispose()
       │    │  │
       │    │  └─► [AudioRecorder.cs:617]
       │    │       │
       │    │       ├─► Line 621: cleanupTimer?.Dispose()
       │    │       │    // Stop cleanup timer BEFORE setting isDisposed
       │    │       │    // Prevents race condition
       │    │       │
       │    │       ├─► Line 637: isDisposed = true
       │    │       │
       │    │       ├─► Line 642: if (isRecording) waveIn?.StopRecording()
       │    │       │
       │    │       └─► Line 661: waveIn?.Dispose()
       │    │
       │    ├─ whisperService?.Dispose()
       │    │  │
       │    │  └─► [PersistentWhisperService.cs:690]
       │    │       │
       │    │       ├─► Line 695: isDisposed = true
       │    │       │
       │    │       ├─► Line 700: warmupTimer?.Dispose()
       │    │       │    // PERFORMANCE: Stop periodic warmup
       │    │       │
       │    │       ├─► Line 709: disposeCts.Cancel()
       │    │       │    // Cancel all background tasks
       │    │       │
       │    │       └─► Line 721: Check for zombie processes
       │    │            lock (processLock) {
       │    │                if (activeProcessIds.Count > 0) {
       │    │                    ErrorLogger.LogError("ZOMBIE PROCESSES DETECTED");
       │    │                    foreach (var pid in activeProcessIds) {
       │    │                        Process.GetProcessById(pid).Kill(entireProcessTree: true);
       │    │                    }
       │    │                }
       │    │            }
       │    │
       │    ├─ hotkeyManager?.Dispose()
       │    ├─ textInjector? (no dispose needed)
       │    ├─ systemTrayManager?.Dispose()
       │    ├─ soundService?.Dispose()
       │    ├─ analyticsService? (no dispose needed)
       │    └─ historyService? (no dispose needed)
       │
       └─► App terminates
```

**CRITICAL**: Disposal order matters. Unsubscribe events BEFORE disposing services.

---

## Next Steps

See [DANGER_ZONES.md](DANGER_ZONES.md) for:
- Memory leak suspects
- Static resource leaks
- Event subscription audits
- Timer disposal issues
- Process zombie tracking
